using System;
using PortalNes.Emulator.Bus;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Cpu;
using PortalNes.Emulator.Ppu;
using PortalNes.Emulator.Input;
using PortalNes.Emulator.Apu;
using PortalNes.Emulator.Mappers;
using PortalNes.Emulator.State;
using System.IO;

namespace PortalNes.Emulator
{
    public sealed class NesMachine
    {
        private const int SaveStateMagic = 0x31534E50; // PNS1
        private const int SaveStateVersion = 4;
        private const int MaximumPendingApuCycles = 32;
        public Cartridge.Cartridge Cartridge { get; private set; }
        public CpuBus Bus { get; private set; }
        public Cpu6502 Cpu { get; private set; }
        public Ppu2C02 Ppu { get; private set; }
        public Apu2A03 Apu { get; private set; }
        public bool AudioEnabled { get; set; }
        public NesController Controller1 { get; } = new NesController();
        public NesController Controller2 { get; } = new NesController();
        private int pendingApuCycles;
        private int pendingDmcStallCycles;
        private int ppuClockRemainder;
        private string lastNmiRequestDiagnostics = "none";
        private string lastNmiServiceDiagnostics = "none";

        public string NmiTimingDiagnostics =>
            $"NMIReq=[{lastNmiRequestDiagnostics}] NMISvc=[{lastNmiServiceDiagnostics}]";

        public void LoadRom(byte[] data, NesRegion? regionOverride = null)
        {
            Cartridge = INesLoader.Load(data, regionOverride);
            Ppu = new Ppu2C02(Cartridge.Mapper, Cartridge.Mirroring, Cartridge.Region);
            Apu = new Apu2A03(Cartridge.Region);
            Bus = new CpuBus(Cartridge.Mapper, Ppu, Apu, Controller1, Controller2, FlushApu);
            Cpu = new Cpu6502(Bus.Read, Bus.Write);
            Apu.ConfigureDmc(Cartridge.Mapper.CpuRead, StallCpuForDmc);
            if (Cartridge.Mapper is IExpansionAudioMapper expansionAudio)
            {
                var frameAudio = Cartridge.Mapper as IFrameSequencedExpansionAudioMapper;
                Action quarterFrame = frameAudio == null ? null : frameAudio.ClockAudioQuarterFrame;
                Action halfFrame = frameAudio == null ? null : frameAudio.ClockAudioHalfFrame;
                Apu.ConfigureExpansionAudio(expansionAudio.ClockAudio,
                    () => expansionAudio.ExpansionAudioSample,
                    quarterFrame,
                    halfFrame);
            }
        }

        public void Reset()
        {
            if (Cpu == null) throw new InvalidOperationException("Load a ROM before resetting the machine.");
            Ppu.Reset();
            Apu.Reset();
            pendingApuCycles = 0;
            pendingDmcStallCycles = 0;
            ppuClockRemainder = 0;
            lastNmiRequestDiagnostics = "none";
            lastNmiServiceDiagnostics = "none";
            Cpu.Reset();
        }

        public void RunFrame()
        {
            if (Cpu == null) throw new InvalidOperationException("Load a ROM before running the machine.");
            // Discard a previously observed latch before beginning the next frame.
            Ppu.ConsumeFrameComplete();
            while (true)
            {
                int cpuCycles;
                bool executedInstruction = pendingDmcStallCycles == 0;
                long nmiServicesBefore = Cpu.NmiServiceCount;
                ushort pcBefore = Cpu.Registers.ProgramCounter;
                if (executedInstruction) cpuCycles = Cpu.Step();
                else
                {
                    cpuCycles = pendingDmcStallCycles;
                    pendingDmcStallCycles = 0;
                }
                if (Cpu.NmiServiceCount != nmiServicesBefore)
                {
                    lastNmiServiceDiagnostics =
                        $"{Ppu.Scanline}:{Ppu.Dot} PC=${pcBefore:X4} " +
                        $"Q={Bus.ReadRam(0x37):X2}{Bus.ReadRam(0x36):X2}/" +
                        $"{Bus.ReadRam(0x39):X2}{Bus.ReadRam(0x38):X2} " +
                        $"Count={Bus.ReadRam(0x3E):X2} CTRL=${Ppu.Registers.Control:X2}";
                }
                if (executedInstruction && Bus.ExecutePendingDma())
                {
                    int dmaCycles = (Cpu.TotalCycles & 1) == 0 ? 513 : 514;
                    Cpu.AddStallCycles(dmaCycles);
                    cpuCycles += dmaCycles;
                }
                if (AudioEnabled)
                {
                    pendingApuCycles += cpuCycles;
                    // Keep waveform sampling close to the CPU/APU timer edges.
                    // Large batches turn several adjacent output samples into
                    // the same value and are audible as coarse distortion.
                    if (pendingApuCycles >= MaximumPendingApuCycles) FlushApu();
                }
                if (Cartridge.Mapper is ICpuClockedMapper cpuClockedMapper)
                    cpuClockedMapper.ClockCpu(cpuCycles);
                int ppuCycles;
                if (Cartridge.Region == NesRegion.Pal)
                {
                    int scaledCycles = ppuClockRemainder + cpuCycles * 16;
                    ppuCycles = scaledCycles / 5;
                    ppuClockRemainder = scaledCycles % 5;
                }
                else ppuCycles = cpuCycles * 3;
                for (int i = 0; i < ppuCycles; i++)
                {
                    Ppu.Clock();
                    if (Ppu.NmiRequested)
                    {
                        lastNmiRequestDiagnostics =
                            $"{Ppu.Scanline}:{Ppu.Dot} PC=${Cpu.Registers.ProgramCounter:X4} " +
                            $"Q={Bus.ReadRam(0x37):X2}{Bus.ReadRam(0x36):X2}/" +
                            $"{Bus.ReadRam(0x39):X2}{Bus.ReadRam(0x38):X2} " +
                            $"Count={Bus.ReadRam(0x3E):X2} CTRL=${Ppu.Registers.Control:X2}";
                        Cpu.RequestNmi(Ppu.DelayNmiOneCpuInstruction);
                        Ppu.ClearNmiRequest();
                    }
                }
                Cpu.SetIrqLine(Apu.IrqPending || Cartridge.Mapper.IrqPending);
                if (Ppu.ConsumeFrameComplete()) { FlushApu(); return; }
            }
        }

        private void FlushApu()
        {
            if (!AudioEnabled) { pendingApuCycles = 0; return; }
            if (Apu == null || pendingApuCycles <= 0) return;
            Apu.Clock(pendingApuCycles);
            pendingApuCycles = 0;
            if (Cpu != null) Cpu.SetIrqLine(Apu.IrqPending || Cartridge.Mapper.IrqPending);
        }

        private void StallCpuForDmc(int cycles)
        {
            if (cycles <= 0 || Cpu == null) return;
            Cpu.AddStallCycles(cycles);
            pendingDmcStallCycles += cycles;
        }

        public ReadOnlySpan<uint> GetFrameBuffer()
        {
            if (Ppu == null) return ReadOnlySpan<uint>.Empty;
            return Ppu.FrameBuffer.Pixels;
        }

        public uint[] GetFrameBufferArray()
        {
            if (Ppu == null) return Array.Empty<uint>();
            return Ppu.FrameBuffer.Pixels;
        }

        public PpuSceneSnapshot GetSceneSnapshot()
        {
            return Ppu?.SceneSnapshot;
        }

        public void SetControllerState(int controllerIndex, byte state)
        {
            if (controllerIndex == 0) Controller1.State = state;
            else if (controllerIndex == 1) Controller2.State = state;
            else throw new ArgumentOutOfRangeException(nameof(controllerIndex), "NES supports controller indices 0 and 1.");
        }

        public byte[] SaveState()
        {
            if (Cpu == null) throw new InvalidOperationException("Load a ROM before saving state.");
            FlushApu();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(SaveStateMagic);
                writer.Write(SaveStateVersion);
                writer.Write(Cartridge.MapperNumber);
                WriteSection(writer, Cpu.CaptureState());
                WriteSection(writer, ReflectionStateCodec.Capture(Bus));
                WriteSection(writer, ReflectionStateCodec.Capture(Ppu));
                WriteSection(writer, ReflectionStateCodec.Capture(Apu));
                WriteSection(writer, ReflectionStateCodec.Capture(Cartridge.Mapper));
                WriteSection(writer, ReflectionStateCodec.Capture(Controller1));
                WriteSection(writer, ReflectionStateCodec.Capture(Controller2));
                writer.Write(pendingApuCycles);
                writer.Write(pendingDmcStallCycles);
                writer.Write(ppuClockRemainder);
                return stream.ToArray();
            }
        }

        public void LoadState(byte[] data)
        {
            if (Cpu == null) throw new InvalidOperationException("Load a ROM before loading state.");
            // Loading mutates several connected components. Keep a compact
            // rollback state so a malformed or non-exact state cannot leave the
            // running machine half-restored.
            byte[] rollbackState = SaveState();
            try
            {
                LoadStateCore(data);
            }
            catch
            {
                try { LoadStateCore(rollbackState); }
                catch { /* Preserve the original load error. */ }
                throw;
            }
        }

        private void LoadStateCore(byte[] data)
        {
            using (var stream = new MemoryStream(data ?? throw new ArgumentNullException(nameof(data)), false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != SaveStateMagic)
                    throw new InvalidDataException("Unsupported PortalNes save-state format.");
                int version = reader.ReadInt32();
                if (version != SaveStateVersion)
                    throw new InvalidDataException(version == 3
                        ? "This save state uses the old Version 3 format. Create a new quick save."
                        : $"Unsupported PortalNes save-state version {version}.");
                int mapper = reader.ReadInt32();
                if (mapper != Cartridge.MapperNumber)
                    throw new InvalidDataException($"Save state uses Mapper {mapper}, current ROM uses Mapper {Cartridge.MapperNumber}.");
                byte[] cpuState = ReadSection(reader);
                byte[] busState = ReadSection(reader);
                byte[] ppuState = ReadSection(reader);
                byte[] apuState = ReadSection(reader);
                byte[] mapperState = ReadSection(reader);
                byte[] controller1State = ReadSection(reader);
                byte[] controller2State = ReadSection(reader);
                Cpu.RestoreState(cpuState);
                VerifyBytes("CPU", cpuState, Cpu.CaptureState());
                RestoreAndVerify("CPU bus", Bus, busState);
                RestoreAndVerify("PPU", Ppu, ppuState);
                RestoreAndVerify("APU", Apu, apuState);
                RestoreAndVerify("mapper", Cartridge.Mapper, mapperState);
                RestoreAndVerify("controller 1", Controller1, controller1State);
                RestoreAndVerify("controller 2", Controller2, controller2State);
                pendingApuCycles = reader.ReadInt32();
                pendingDmcStallCycles = reader.ReadInt32();
                ppuClockRemainder = reader.ReadInt32();
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Save state contains unexpected trailing data.");
            }
            Apu.DiscardBufferedSamples();
            Cpu.SetIrqLine(Apu.IrqPending || Cartridge.Mapper.IrqPending);
        }

        private static void WriteSection(BinaryWriter writer, byte[] data)
        {
            writer.Write(data.Length);
            writer.Write(data);
        }

        private static byte[] ReadSection(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > 64 * 1024 * 1024)
                throw new InvalidDataException("Invalid save-state section length.");
            byte[] data = reader.ReadBytes(length);
            if (data.Length != length)
                throw new EndOfStreamException("Save-state section is truncated.");
            return data;
        }

        private static void RestoreAndVerify(string name, object target, byte[] state)
        {
            ReflectionStateCodec.Restore(target, state);
            VerifyBytes(name, state, ReflectionStateCodec.Capture(target));
        }

        private static void VerifyBytes(string name, byte[] state, byte[] restored)
        {
            if (restored.Length != state.Length)
                throw new InvalidDataException($"Save-state {name} length did not restore exactly.");
            for (int i = 0; i < state.Length; i++)
                if (state[i] != restored[i])
                    throw new InvalidDataException(
                        $"Save-state {name} did not restore exactly at byte {i}.");
        }

    }
}
