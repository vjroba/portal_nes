using System;
using System.Threading;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Apu
{
    public sealed class Apu2A03
    {
        public const double NtscCpuFrequency = 1789773.0;
        public const double PalCpuFrequency = 1662607.0;
        private static readonly byte[] LengthTable = { 10,254,20,2,40,4,80,6,160,8,60,10,14,12,26,14,12,16,24,18,48,20,96,22,192,24,72,26,16,28,32,30 };
        private readonly Pulse pulse1 = new Pulse(true), pulse2 = new Pulse(false);
        private readonly Triangle triangle = new Triangle();
        private readonly Noise noise;
        private readonly Dmc dmc;
        private readonly double cpuFrequency;
        private readonly int quarter1, half1, quarter2, sequence4End, sequence5End;
        private readonly float[] samples = new float[32768];
        private int writeIndex, readIndex, frameCycle;
        private long apuCycle;
        private double sampleRate = 48000, samplePhase;
        private bool fiveStep, irqInhibit, frameIrq;
        private float hp90Input, hp90Output, hp440Input, hp440Output, lowPassOutput;
        private Action<int> clockExpansionAudio;
        private Func<float> readExpansionAudio;
        private Action clockExpansionQuarterFrame;
        private Action clockExpansionHalfFrame;

        public bool IrqPending => frameIrq || dmc.IrqPending;

        public Apu2A03(NesRegion region = NesRegion.Ntsc)
        {
            bool pal = region == NesRegion.Pal;
            cpuFrequency = pal ? PalCpuFrequency : NtscCpuFrequency;
            noise = new Noise(pal
                ? new[] { 4,8,14,30,60,88,118,148,188,236,354,472,708,944,1890,3778 }
                : new[] { 4,8,16,32,64,96,128,160,202,254,380,508,762,1016,2034,4068 });
            dmc = new Dmc(pal
                ? new[] { 398,354,316,298,276,236,210,198,176,148,132,118,98,78,66,50 }
                : new[] { 428,380,340,320,286,254,226,214,190,160,142,128,106,85,72,54 });
            quarter1 = pal ? 8313 : 7457;
            half1 = pal ? 16627 : 14913;
            quarter2 = pal ? 24939 : 22371;
            sequence4End = pal ? 33253 : 29829;
            sequence5End = pal ? 41565 : 37281;
        }

        public void ConfigureDmc(Func<ushort, byte> memoryRead, Action<int> stallCpu)
        {
            dmc.Configure(memoryRead, stallCpu);
        }

        public void ConfigureExpansionAudio(Action<int> clock, Func<float> readSample,
            Action clockQuarterFrame = null, Action clockHalfFrame = null)
        {
            clockExpansionAudio = clock;
            readExpansionAudio = readSample;
            clockExpansionQuarterFrame = clockQuarterFrame;
            clockExpansionHalfFrame = clockHalfFrame;
        }
        public int BufferedSampleCount
        {
            get
            {
                int read = Volatile.Read(ref readIndex);
                int write = Volatile.Read(ref writeIndex);
                return (write - read) & (samples.Length - 1);
            }
        }

        public void Reset()
        {
            pulse1.Reset(); pulse2.Reset(); triangle.Reset(); noise.Reset(); dmc.Reset();
            frameCycle = 0; apuCycle = 0; samplePhase = 0; frameIrq = false; fiveStep = irqInhibit = false;
            readIndex = writeIndex = 0;
            hp90Input = hp90Output = hp440Input = hp440Output = lowPassOutput = 0;
        }

        public void SetSampleRate(int value) => sampleRate = Math.Max(8000, value);

        public void DiscardBufferedSamples()
        {
            readIndex = writeIndex = 0;
            Array.Clear(samples, 0, samples.Length);
        }

        public byte ReadStatus()
        {
            byte value = 0;
            if (pulse1.LengthCounter > 0) value |= 0x01;
            if (pulse2.LengthCounter > 0) value |= 0x02;
            if (triangle.LengthCounter > 0) value |= 0x04;
            if (noise.LengthCounter > 0) value |= 0x08;
            if (dmc.BytesRemaining > 0) value |= 0x10;
            if (frameIrq) value |= 0x40;
            if (dmc.IrqPending) value |= 0x80;
            frameIrq = false;
            return value;
        }

        public void WriteRegister(ushort address, byte value)
        {
            if (address >= 0x4000 && address <= 0x4003) pulse1.Write(address & 3, value);
            else if (address >= 0x4004 && address <= 0x4007) pulse2.Write(address & 3, value);
            else if (address >= 0x4008 && address <= 0x400B) triangle.Write(address & 3, value);
            else if (address >= 0x400C && address <= 0x400F) noise.Write(address & 3, value);
            else if (address >= 0x4010 && address <= 0x4013) dmc.Write(address & 3, value);
            else if (address == 0x4015)
            {
                pulse1.SetEnabled((value & 1) != 0); pulse2.SetEnabled((value & 2) != 0);
                triangle.SetEnabled((value & 4) != 0); noise.SetEnabled((value & 8) != 0);
                dmc.SetEnabled((value & 0x10) != 0);
            }
            else if (address == 0x4017)
            {
                fiveStep = (value & 0x80) != 0; irqInhibit = (value & 0x40) != 0;
                if (irqInhibit) frameIrq = false;
                frameCycle = 0;
                if (fiveStep) { ClockQuarterFrame(); ClockHalfFrame(); }
            }
        }

        public void Clock(int cpuCycles)
        {
            if (cpuCycles <= 0) return;
            long startCycle = apuCycle;
            int pulseClocks = cpuCycles / 2;
            if ((cpuCycles & 1) != 0 && (startCycle & 1) == 0) pulseClocks++;
            apuCycle += cpuCycles;
            pulse1.ClockTimer(pulseClocks); pulse2.ClockTimer(pulseClocks);
            triangle.ClockTimer(cpuCycles); noise.ClockTimer(cpuCycles);
            dmc.ClockTimer(cpuCycles);
            clockExpansionAudio?.Invoke(cpuCycles);
            AdvanceFrameSequencer(cpuCycles);
            samplePhase += sampleRate * cpuCycles;
            while (samplePhase >= cpuFrequency)
            {
                samplePhase -= cpuFrequency;
                Enqueue(Filter(Mix()));
            }
        }

        public int ReadSamples(float[] destination, int offset, int count)
        {
            int read = Volatile.Read(ref readIndex), write = Volatile.Read(ref writeIndex), copied = 0;
            while (copied < count && read != write)
            {
                destination[offset + copied++] = samples[read];
                read = (read + 1) & (samples.Length - 1);
            }
            Volatile.Write(ref readIndex, read);
            return copied;
        }

        private void AdvanceFrameSequencer(int cycles)
        {
            while (cycles > 0)
            {
                int next = frameCycle < quarter1 ? quarter1 : frameCycle < half1 ? half1 :
                    frameCycle < quarter2 ? quarter2 : fiveStep ? sequence5End : sequence4End;
                int distance = next - frameCycle;
                if (cycles < distance) { frameCycle += cycles; return; }
                cycles -= distance;
                frameCycle = next;
                if (next == quarter1 || next == quarter2) ClockQuarterFrame();
                else
                {
                    ClockQuarterFrame(); ClockHalfFrame();
                    if (!fiveStep && next == sequence4End && !irqInhibit) frameIrq = true;
                }
                if ((!fiveStep && next == sequence4End) || (fiveStep && next == sequence5End))
                {
                    frameCycle = 0;
                }
            }
        }

        private void ClockQuarterFrame()
        {
            pulse1.ClockEnvelope(); pulse2.ClockEnvelope(); triangle.ClockLinear(); noise.ClockEnvelope();
            clockExpansionQuarterFrame?.Invoke();
        }
        private void ClockHalfFrame()
        {
            pulse1.ClockLengthAndSweep(); pulse2.ClockLengthAndSweep(); triangle.ClockLength(); noise.ClockLength();
            clockExpansionHalfFrame?.Invoke();
        }

        private float Mix()
        {
            int p = pulse1.Output + pulse2.Output;
            float pulse = p == 0 ? 0 : 95.88f / (8128f / p + 100f);
            float tndInput = triangle.Output / 8227f + noise.Output / 12241f + dmc.Output / 22638f;
            float tnd = tndInput == 0 ? 0 : 159.79f / (1f / tndInput + 100f);
            // VRC6 and similar cartridge DACs are linear. This gain puts the
            // maximum VRC6 output close to the two internal pulse channels.
            float expansion = readExpansionAudio?.Invoke() ?? 0;
            return pulse + tnd + expansion * 0.004f;
        }

        private float Filter(float input)
        {
            float hp90 = 0.9883f * (hp90Output + input - hp90Input); hp90Input = input; hp90Output = hp90;
            float hp440 = 0.9413f * (hp440Output + hp90 - hp440Input); hp440Input = hp90; hp440Output = hp440;
            lowPassOutput += 0.8157f * (hp440 - lowPassOutput);
            return lowPassOutput;
        }

        private void Enqueue(float sample)
        {
            int write = writeIndex, next = (write + 1) & (samples.Length - 1);
            int read = Volatile.Read(ref readIndex);
            if (next == read) Volatile.Write(ref readIndex, (read + 1) & (samples.Length - 1));
            samples[write] = sample;
            Volatile.Write(ref writeIndex, next);
        }

        private sealed class Pulse
        {
            private static readonly byte[] DutyPatterns = { 0x02, 0x06, 0x1E, 0xF9 };
            private readonly bool first;
            private bool enabled, lengthHalt, constantVolume, envelopeStart, sweepEnabled, sweepNegate, sweepReload;
            private int duty, volume, envelopeDivider, envelopeDecay, timerPeriod, timerCounter, sequence;
            private int sweepPeriod, sweepShift, sweepDivider;
            public int LengthCounter { get; private set; }
            public int Output
            {
                get
                {
                    int target = SweepTarget();
                    if (!enabled || LengthCounter == 0 || timerPeriod < 8 || target > 0x7FF ||
                        ((DutyPatterns[duty] >> sequence) & 1) == 0) return 0;
                    return constantVolume ? volume : envelopeDecay;
                }
            }
            public Pulse(bool first) { this.first = first; }
            public void Reset() { enabled = false; LengthCounter = timerPeriod = timerCounter = sequence = 0; envelopeDecay = 0; }
            public void SetEnabled(bool value) { enabled = value; if (!value) LengthCounter = 0; }
            public void Write(int register, byte value)
            {
                if (register == 0) { duty = value >> 6; lengthHalt = (value & 0x20) != 0; constantVolume = (value & 0x10) != 0; volume = value & 15; }
                else if (register == 1) { sweepEnabled = (value & 0x80) != 0; sweepPeriod = ((value >> 4) & 7) + 1; sweepNegate = (value & 8) != 0; sweepShift = value & 7; sweepReload = true; }
                else if (register == 2) timerPeriod = (timerPeriod & 0x700) | value;
                else { timerPeriod = (timerPeriod & 0xFF) | ((value & 7) << 8); if (enabled) LengthCounter = LengthTable[value >> 3]; sequence = 0; envelopeStart = true; }
            }
            public void ClockTimer(int clocks)
            {
                if (!enabled || LengthCounter == 0) return;
                if (clocks <= timerCounter) { timerCounter -= clocks; return; }
                clocks -= timerCounter + 1;
                int divider = timerPeriod + 1;
                int steps = 1 + clocks / divider;
                sequence = (sequence + steps) & 7;
                timerCounter = timerPeriod - clocks % divider;
            }
            public void ClockEnvelope()
            {
                if (envelopeStart) { envelopeStart = false; envelopeDecay = 15; envelopeDivider = volume; }
                else if (envelopeDivider == 0) { envelopeDivider = volume; if (envelopeDecay > 0) envelopeDecay--; else if (lengthHalt) envelopeDecay = 15; }
                else envelopeDivider--;
            }
            public void ClockLengthAndSweep()
            {
                if (!lengthHalt && LengthCounter > 0) LengthCounter--;
                if (sweepDivider == 0)
                {
                    if (sweepEnabled && sweepShift > 0 && timerPeriod >= 8 && SweepTarget() <= 0x7FF) timerPeriod = SweepTarget();
                    sweepDivider = sweepPeriod;
                }
                else sweepDivider--;
                if (sweepReload) { sweepReload = false; sweepDivider = sweepPeriod; }
            }
            private int SweepTarget()
            {
                if (sweepShift == 0) return timerPeriod;
                int change = timerPeriod >> sweepShift;
                return sweepNegate ? timerPeriod - change - (first ? 1 : 0) : timerPeriod + change;
            }
        }

        private sealed class Triangle
        {
            private static readonly byte[] Sequence = { 15,14,13,12,11,10,9,8,7,6,5,4,3,2,1,0,0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15 };
            private bool enabled, control, linearReloadFlag;
            private int linearReload, linearCounter, timerPeriod, timerCounter, sequence;
            public int LengthCounter { get; private set; }
            public int Output => enabled && LengthCounter > 0 && linearCounter > 0 ? Sequence[sequence] : 0;
            public void Reset() { enabled = false; LengthCounter = linearCounter = timerPeriod = timerCounter = sequence = 0; }
            public void SetEnabled(bool value) { enabled = value; if (!value) LengthCounter = 0; }
            public void Write(int register, byte value)
            {
                if (register == 0) { control = (value & 0x80) != 0; linearReload = value & 0x7F; }
                else if (register == 2) timerPeriod = (timerPeriod & 0x700) | value;
                else if (register == 3) { timerPeriod = (timerPeriod & 0xFF) | ((value & 7) << 8); if (enabled) LengthCounter = LengthTable[value >> 3]; linearReloadFlag = true; }
            }
            public void ClockTimer(int clocks)
            {
                if (!enabled || LengthCounter == 0 || linearCounter == 0) return;
                if (clocks <= timerCounter) { timerCounter -= clocks; return; }
                clocks -= timerCounter + 1;
                int divider = timerPeriod + 1;
                int steps = 1 + clocks / divider;
                if (timerPeriod > 1) sequence = (sequence + steps) & 31;
                timerCounter = timerPeriod - clocks % divider;
            }
            public void ClockLinear() { if (linearReloadFlag) linearCounter = linearReload; else if (linearCounter > 0) linearCounter--; if (!control) linearReloadFlag = false; }
            public void ClockLength() { if (!control && LengthCounter > 0) LengthCounter--; }
        }

        private sealed class Noise
        {
            private readonly int[] periods;
            private bool enabled, lengthHalt, constantVolume, envelopeStart, mode;
            private int volume, envelopeDivider, envelopeDecay, timerPeriod = 4, timerCounter;
            private ushort shiftRegister = 1;
            public int LengthCounter { get; private set; }
            public int Output => enabled && LengthCounter > 0 && (shiftRegister & 1) == 0 ? (constantVolume ? volume : envelopeDecay) : 0;
            public Noise(int[] periods) { this.periods = periods; }
            public void Reset() { enabled = false; LengthCounter = 0; shiftRegister = 1; envelopeDecay = 0; }
            public void SetEnabled(bool value) { enabled = value; if (!value) LengthCounter = 0; }
            public void Write(int register, byte value)
            {
                if (register == 0) { lengthHalt = (value & 0x20) != 0; constantVolume = (value & 0x10) != 0; volume = value & 15; }
                else if (register == 2) { mode = (value & 0x80) != 0; timerPeriod = periods[value & 15]; }
                else if (register == 3) { if (enabled) LengthCounter = LengthTable[value >> 3]; envelopeStart = true; }
            }
            public void ClockTimer(int clocks)
            {
                if (!enabled || LengthCounter == 0) return;
                if (clocks <= timerCounter) { timerCounter -= clocks; return; }
                clocks -= timerCounter + 1;
                int divider = timerPeriod + 1;
                int steps = 1 + clocks / divider;
                for (int i = 0; i < steps; i++) ClockShiftRegister();
                timerCounter = timerPeriod - clocks % divider;
            }
            private void ClockShiftRegister() { int tap = mode ? 6 : 1; int feedback = (shiftRegister & 1) ^ ((shiftRegister >> tap) & 1); shiftRegister = (ushort)((shiftRegister >> 1) | (feedback << 14)); }
            public void ClockEnvelope()
            {
                if (envelopeStart) { envelopeStart = false; envelopeDecay = 15; envelopeDivider = volume; }
                else if (envelopeDivider == 0) { envelopeDivider = volume; if (envelopeDecay > 0) envelopeDecay--; else if (lengthHalt) envelopeDecay = 15; }
                else envelopeDivider--;
            }
            public void ClockLength() { if (!lengthHalt && LengthCounter > 0) LengthCounter--; }
        }

        private sealed class Dmc
        {
            private readonly int[] periods;
            private Func<ushort, byte> memoryRead;
            private Action<int> stallCpu;
            private bool irqEnabled, loop, silence = true, sampleBufferEmpty = true;
            private int timerPeriod, timerCounter, outputLevel, bitsRemaining = 8;
            private byte sampleBuffer, shiftRegister;
            private ushort sampleAddress = 0xC000, currentAddress;
            private int sampleLength = 1;

            public int BytesRemaining { get; private set; }
            public bool IrqPending { get; private set; }
            public int Output => outputLevel;

            public Dmc(int[] periods)
            {
                this.periods = periods;
                timerPeriod = periods[0];
            }

            public void Configure(Func<ushort, byte> read, Action<int> stall)
            {
                memoryRead = read;
                stallCpu = stall;
            }

            public void Reset()
            {
                irqEnabled = loop = IrqPending = false;
                silence = sampleBufferEmpty = true;
                timerPeriod = periods[0];
                timerCounter = 0;
                outputLevel = 0;
                bitsRemaining = 8;
                sampleBuffer = shiftRegister = 0;
                sampleAddress = currentAddress = 0xC000;
                sampleLength = 1;
                BytesRemaining = 0;
            }

            public void Write(int register, byte value)
            {
                if (register == 0)
                {
                    irqEnabled = (value & 0x80) != 0;
                    loop = (value & 0x40) != 0;
                    timerPeriod = periods[value & 0x0F];
                    if (!irqEnabled) IrqPending = false;
                }
                else if (register == 1) outputLevel = value & 0x7F;
                else if (register == 2) sampleAddress = (ushort)(0xC000 | (value << 6));
                else sampleLength = (value << 4) | 1;
            }

            public void SetEnabled(bool enabled)
            {
                IrqPending = false;
                if (!enabled) BytesRemaining = 0;
                else if (BytesRemaining == 0) RestartSample();
                FillSampleBuffer();
            }

            public void ClockTimer(int clocks)
            {
                FillSampleBuffer();
                while (clocks > timerCounter)
                {
                    clocks -= timerCounter + 1;
                    timerCounter = timerPeriod - 1;
                    ClockOutput();
                    FillSampleBuffer();
                }
                timerCounter -= clocks;
            }

            private void ClockOutput()
            {
                if (!silence)
                {
                    if ((shiftRegister & 1) != 0)
                    {
                        if (outputLevel <= 125) outputLevel += 2;
                    }
                    else if (outputLevel >= 2) outputLevel -= 2;
                }
                shiftRegister >>= 1;
                if (--bitsRemaining != 0) return;
                bitsRemaining = 8;
                if (sampleBufferEmpty) silence = true;
                else
                {
                    silence = false;
                    shiftRegister = sampleBuffer;
                    sampleBufferEmpty = true;
                }
            }

            private void FillSampleBuffer()
            {
                if (!sampleBufferEmpty || BytesRemaining == 0 || memoryRead == null) return;
                stallCpu?.Invoke(4);
                sampleBuffer = memoryRead(currentAddress);
                sampleBufferEmpty = false;
                currentAddress = currentAddress == 0xFFFF ? (ushort)0x8000 : (ushort)(currentAddress + 1);
                BytesRemaining--;
                if (BytesRemaining != 0) return;
                if (loop) RestartSample();
                else if (irqEnabled) IrqPending = true;
            }

            private void RestartSample()
            {
                currentAddress = sampleAddress;
                BytesRemaining = sampleLength;
            }
        }
    }
}
