using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>Konami VRC7 mapper with six-channel FM expansion audio.</summary>
    public sealed class Mapper085 : IMapper, ICpuClockedMapper, IExpansionAudioMapper
    {
        private const int BankSize = 8192;
        private readonly byte[] prgRom, chr;
        private readonly bool chrRam;
        private readonly byte[] ram = new byte[8192];
        private readonly byte[] prgBanks = new byte[3], chrBanks = new byte[8];
        private readonly Vrc7Audio audio = new Vrc7Audio();
        private readonly int prgCount, chrCount;
        private bool ramEnabled, irqEnabled, irqAfterAck, irqCycle, irqPending;
        private byte irqLatch, irqCounter, audioRegister;
        private int irqPrescaler = 341;
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => irqPending;
        public float ExpansionAudioSample => audio.Output;

        public Mapper085(byte[] prgRom, byte[] chr, bool chrRam, MirroringMode initialMirroring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chr = chr ?? throw new ArgumentNullException(nameof(chr));
            if (prgRom.Length < BankSize * 4 || prgRom.Length % BankSize != 0)
                throw new ArgumentException("VRC7 requires complete 8KB PRG banks.", nameof(prgRom));
            if (chr.Length < BankSize || chr.Length % 1024 != 0)
                throw new ArgumentException("VRC7 requires complete 1KB CHR banks.", nameof(chr));
            prgCount = prgRom.Length / BankSize;
            chrCount = chr.Length / 1024;
            this.chrRam = chrRam;
            mirroring = initialMirroring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000) return ramEnabled ? ram[address - 0x6000] : (byte)0;
            int slot = (address - 0x8000) / BankSize;
            int bank = slot < 3 ? prgBanks[slot] % prgCount : prgCount - 1;
            return prgRom[bank * BankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000) { if (ramEnabled) ram[address - 0x6000] = value; return; }
            if ((address & 0xF030) == 0x9010) { audioRegister = value; return; }
            if ((address & 0xF030) == 0x9030) { audio.Write(audioRegister, value); return; }

            int group = address & 0xF000;
            bool secondary = (address & 0x18) != 0;
            if (group == 0x8000) prgBanks[secondary ? 1 : 0] = (byte)(value & 0x3F);
            else if (group == 0x9000 && !secondary) prgBanks[2] = (byte)(value & 0x3F);
            else if (group >= 0xA000 && group <= 0xD000)
            {
                int pair = (group - 0xA000) >> 12;
                chrBanks[pair * 2 + (secondary ? 1 : 0)] = value;
            }
            else if (group == 0xE000)
            {
                if (!secondary)
                {
                    ramEnabled = (value & 0x80) != 0;
                    audio.ResetHeld = (value & 0x40) != 0;
                    switch (value & 3)
                    {
                        case 0: mirroring = MirroringMode.Vertical; break;
                        case 1: mirroring = MirroringMode.Horizontal; break;
                        case 2: mirroring = MirroringMode.SingleScreenLower; break;
                        default: mirroring = MirroringMode.SingleScreenUpper; break;
                    }
                }
                else irqLatch = value;
            }
            else if (group == 0xF000)
            {
                if (!secondary)
                {
                    irqAfterAck = (value & 1) != 0; irqEnabled = (value & 2) != 0;
                    irqCycle = (value & 4) != 0; irqPending = false; irqPrescaler = 341;
                    if (irqEnabled) irqCounter = irqLatch;
                }
                else { irqPending = false; irqEnabled = irqAfterAck; }
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address >> 10, bank = chrBanks[slot] % chrCount;
            return chr[bank * 1024 + (address & 0x3FF)];
        }
        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            if (!chrRam) return;
            int slot = address >> 10, bank = chrBanks[slot] % chrCount;
            chr[bank * 1024 + (address & 0x3FF)] = value;
        }
        public void ClockScanline() { }
        public void ClockAudio(int cycles) => audio.Clock(cycles);

        public void ClockCpu(int cycles)
        {
            if (!irqEnabled || cycles <= 0) return;
            if (irqCycle) { for (int i = 0; i < cycles; i++) ClockIrq(); return; }
            irqPrescaler -= cycles * 3;
            while (irqPrescaler <= 0) { irqPrescaler += 341; ClockIrq(); }
        }
        private void ClockIrq() { if (irqCounter == 0xFF) { irqCounter = irqLatch; irqPending = true; } else irqCounter++; }

        private sealed class Vrc7Audio
        {
            private const double CpuRate = 1789773.0;
            private static readonly byte[,] Patches = {
                {0,0,0,0,0,0,0,0},{0x03,0x21,0x05,0x06,0xE8,0x81,0x42,0x27},
                {0x13,0x41,0x14,0x0D,0xD8,0xF6,0x23,0x12},{0x11,0x11,0x08,0x08,0xFA,0xB2,0x20,0x12},
                {0x31,0x61,0x0C,0x07,0xA8,0x64,0x61,0x27},{0x32,0x21,0x1E,0x06,0xE1,0x76,0x01,0x28},
                {0x02,0x01,0x06,0x00,0xA3,0xE2,0xF4,0xF4},{0x21,0x61,0x1D,0x07,0x82,0x81,0x11,0x07},
                {0x23,0x21,0x22,0x17,0xA2,0x72,0x01,0x17},{0x35,0x11,0x25,0x00,0x40,0x73,0x72,0x01},
                {0xB5,0x01,0x0F,0x0F,0xA8,0xA5,0x51,0x02},{0x17,0xC1,0x24,0x07,0xF8,0xF8,0x22,0x12},
                {0x71,0x23,0x11,0x06,0x65,0x74,0x18,0x16},{0x01,0x02,0xD3,0x05,0xC9,0x95,0x03,0x02},
                {0x61,0x63,0x0C,0x00,0x94,0xC0,0x33,0xF6},{0x21,0x72,0x0D,0x00,0xC1,0xD5,0x56,0x06}
            };
            private readonly byte[] regs = new byte[0x36];
            private readonly Channel[] channels = { new Channel(),new Channel(),new Channel(),new Channel(),new Channel(),new Channel() };
            private bool resetHeld;
            public bool ResetHeld { get => resetHeld; set { resetHeld = value; if (value) Reset(); } }
            public float Output { get; private set; }
            public void Write(byte register, byte value)
            {
                if (resetHeld || register >= regs.Length) return;
                regs[register] = value;
                if (register >= 0x20 && register <= 0x25)
                {
                    int c = register - 0x20; bool key = (value & 0x10) != 0;
                    if (key && !channels[c].Key) channels[c].Envelope = 0;
                    channels[c].Key = key;
                }
            }
            public void Clock(int cycles)
            {
                if (resetHeld) { Output = 0; return; }
                double dt = cycles / CpuRate; double sum = 0;
                for (int c = 0; c < 6; c++)
                {
                    var ch = channels[c]; int control = regs[0x20 + c];
                    int fnum = regs[0x10 + c] | ((control & 1) << 8), octave = (control >> 1) & 7;
                    int instrument = regs[0x30 + c] >> 4, volume = regs[0x30 + c] & 15;
                    byte p0 = instrument == 0 ? regs[0] : Patches[instrument,0];
                    byte p1 = instrument == 0 ? regs[1] : Patches[instrument,1];
                    byte p3 = instrument == 0 ? regs[3] : Patches[instrument,3];
                    double frequency = 49716.0 * fnum / (1 << (19 - octave));
                    ch.Envelope += dt * (ch.Key ? 12.0 : -4.0); ch.Envelope = Math.Max(0, Math.Min(1, ch.Envelope));
                    ch.ModPhase += Math.PI * 2 * frequency * Multiplier(p0 & 15) * dt;
                    ch.CarPhase += Math.PI * 2 * frequency * Multiplier(p1 & 15) * dt;
                    double index = ((p3 & 7) / 7.0) * 4.0;
                    double level = ch.Envelope * Math.Pow(10, -(volume * 3.0) / 20.0);
                    sum += Math.Sin(ch.CarPhase + Math.Sin(ch.ModPhase) * index) * level;
                }
                Output = (float)(sum * 5.0);
            }
            private static double Multiplier(int v) { int[] m={1,2,4,6,8,10,12,14,16,18,20,20,24,24,30,30}; return m[v]/2.0; }
            private void Reset() { Array.Clear(regs,0,regs.Length); foreach (var c in channels) { c.Key=false;c.Envelope=0;c.ModPhase=c.CarPhase=0; } Output=0; }
            private sealed class Channel { public bool Key; public double Envelope,ModPhase,CarPhase; }
        }
    }
}
