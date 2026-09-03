using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Shared Konami VRC6 implementation for mapper 24 (VRC6a) and
    /// mapper 26 (VRC6b), including its three-channel expansion audio.
    /// </summary>
    public abstract class Vrc6Mapper : IMapper, ICpuClockedMapper, IExpansionAudioMapper
    {
        private const int Prg16BankSize = 16 * 1024;
        private const int Prg8BankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly byte[] prgRam = new byte[8 * 1024];
        private readonly int prg16BankCount;
        private readonly int prg8BankCount;
        private readonly int chrBankCount;
        private readonly bool swappedAddressLines;
        private readonly byte[] chrBanks = new byte[8];
        private readonly Vrc6Pulse pulse1 = new Vrc6Pulse();
        private readonly Vrc6Pulse pulse2 = new Vrc6Pulse();
        private readonly Vrc6Saw saw = new Vrc6Saw();
        private byte selectedPrg16;
        private byte selectedPrg8;
        private byte bankingStyle;
        private bool prgRamEnabled;
        private bool audioHalt;
        private int audioFrequencyShift;
        private MirroringMode mirroring;
        private byte irqLatch;
        private byte irqCounter;
        private int irqPrescaler = 341;
        private bool irqEnableAfterAcknowledgement;
        private bool irqEnabled;
        private bool irqCycleMode;
        private bool irqPending;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => irqPending;
        public float ExpansionAudioSample => pulse1.Output + pulse2.Output + saw.Output;

        protected Vrc6Mapper(
            byte[] prgRom,
            byte[] chrRom,
            MirroringMode initialMirroring,
            bool swappedAddressLines)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 2 * Prg16BankSize || prgRom.Length % Prg16BankSize != 0)
                throw new ArgumentException(
                    "VRC6 PRG ROM must contain at least two complete 16KB banks.",
                    nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException(
                    "VRC6 CHR ROM must contain at least eight complete 1KB banks.",
                    nameof(chrRom));
            prg16BankCount = prgRom.Length / Prg16BankSize;
            prg8BankCount = prgRom.Length / Prg8BankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            mirroring = initialMirroring;
            this.swappedAddressLines = swappedAddressLines;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
                return prgRamEnabled ? prgRam[address - 0x6000] : (byte)0;
            if (address < 0xC000)
            {
                int bank = selectedPrg16 % prg16BankCount;
                return prgRom[bank * Prg16BankSize + (address & 0x3FFF)];
            }
            int bank8 = address < 0xE000
                ? selectedPrg8 % prg8BankCount
                : prg8BankCount - 1;
            return prgRom[bank8 * Prg8BankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                if (prgRamEnabled) prgRam[address - 0x6000] = value;
                return;
            }

            int group = address & 0xF000;
            int register = DecodeRegister(address);
            switch (group)
            {
                case 0x8000:
                    selectedPrg16 = (byte)(value & 0x0F);
                    break;
                case 0x9000:
                    if (register < 3) pulse1.Write(register, value);
                    else WriteFrequencyControl(value);
                    break;
                case 0xA000:
                    if (register < 3) pulse2.Write(register, value);
                    break;
                case 0xB000:
                    if (register < 3) saw.Write(register, value);
                    else WriteBankingStyle(value);
                    break;
                case 0xC000:
                    selectedPrg8 = (byte)(value & 0x1F);
                    break;
                case 0xD000:
                    chrBanks[register] = value;
                    break;
                case 0xE000:
                    chrBanks[4 + register] = value;
                    break;
                case 0xF000:
                    WriteIrqRegister(register, value);
                    break;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address / ChrBankSize;
            int bank = MapChrBank(slot) % chrBankCount;
            return chrRom[bank * ChrBankSize + (address & 0x03FF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }

        public void ClockCpu(int cycles)
        {
            if (cycles <= 0 || !irqEnabled) return;
            if (irqCycleMode)
            {
                for (int i = 0; i < cycles; i++) ClockIrqCounter();
                return;
            }
            irqPrescaler -= cycles * 3;
            while (irqPrescaler <= 0)
            {
                irqPrescaler += 341;
                ClockIrqCounter();
            }
        }

        public void ClockAudio(int cycles)
        {
            if (cycles <= 0 || audioHalt) return;
            pulse1.Clock(cycles, audioFrequencyShift);
            pulse2.Clock(cycles, audioFrequencyShift);
            saw.Clock(cycles, audioFrequencyShift);
        }

        private int DecodeRegister(ushort address)
        {
            int value = address & 3;
            if (!swappedAddressLines) return value;
            return ((value >> 1) & 1) | ((value & 1) << 1);
        }

        private void WriteFrequencyControl(byte value)
        {
            audioHalt = (value & 1) != 0;
            audioFrequencyShift = (value & 4) != 0 ? 8 : (value & 2) != 0 ? 4 : 0;
        }

        private void WriteBankingStyle(byte value)
        {
            bankingStyle = value;
            prgRamEnabled = (value & 0x80) != 0;
            // All licensed VRC6 games use mode 0 with CIRAM. In that mode
            // bits 2-3 select the four conventional nametable layouts.
            switch ((value >> 2) & 3)
            {
                case 0: mirroring = MirroringMode.Vertical; break;
                case 1: mirroring = MirroringMode.Horizontal; break;
                case 2: mirroring = MirroringMode.SingleScreenLower; break;
                default: mirroring = MirroringMode.SingleScreenUpper; break;
            }
        }

        private int MapChrBank(int slot)
        {
            switch (bankingStyle & 3)
            {
                case 1:
                    // Four 2KB banks, represented as adjacent 1KB slots.
                    return chrBanks[slot >> 1] + (slot & 1);
                case 2:
                case 3:
                    // Two 2KB and four 1KB banks.
                    if (slot < 4) return chrBanks[slot >> 1] + (slot & 1);
                    return chrBanks[slot - 2];
                default:
                    return chrBanks[slot];
            }
        }

        private void WriteIrqRegister(int register, byte value)
        {
            switch (register)
            {
                case 0:
                    irqLatch = value;
                    break;
                case 1:
                    irqEnableAfterAcknowledgement = (value & 1) != 0;
                    irqEnabled = (value & 2) != 0;
                    irqCycleMode = (value & 4) != 0;
                    irqPending = false;
                    irqPrescaler = 341;
                    if (irqEnabled) irqCounter = irqLatch;
                    break;
                case 2:
                    irqPending = false;
                    irqEnabled = irqEnableAfterAcknowledgement;
                    break;
            }
        }

        private void ClockIrqCounter()
        {
            if (irqCounter == 0xFF)
            {
                irqCounter = irqLatch;
                irqPending = true;
            }
            else irqCounter++;
        }

        private sealed class Vrc6Pulse
        {
            private int volume;
            private int duty;
            private int period;
            private int counter;
            private int step = 15;
            private bool mode;
            private bool enabled;

            public int Output => enabled && (mode || step <= duty) ? volume : 0;

            public void Write(int register, byte value)
            {
                if (register == 0)
                {
                    volume = value & 0x0F;
                    duty = (value >> 4) & 7;
                    mode = (value & 0x80) != 0;
                }
                else if (register == 1)
                    period = (period & 0xF00) | value;
                else
                {
                    period = (period & 0x0FF) | ((value & 0x0F) << 8);
                    enabled = (value & 0x80) != 0;
                }
            }

            public void Clock(int cycles, int shift)
            {
                int effectivePeriod = period >> shift;
                if (cycles <= counter) { counter -= cycles; return; }
                cycles -= counter + 1;
                int divider = effectivePeriod + 1;
                int steps = 1 + cycles / divider;
                step = (step - steps) & 15;
                counter = effectivePeriod - cycles % divider;
            }
        }

        private sealed class Vrc6Saw
        {
            private int rate;
            private int period;
            private int counter;
            private int step;
            private int accumulator;
            private bool enabled;

            public int Output => enabled ? accumulator >> 3 : 0;

            public void Write(int register, byte value)
            {
                if (register == 0)
                    rate = value & 0x3F;
                else if (register == 1)
                    period = (period & 0xF00) | value;
                else
                {
                    period = (period & 0x0FF) | ((value & 0x0F) << 8);
                    enabled = (value & 0x80) != 0;
                    if (!enabled) accumulator = step = 0;
                }
            }

            public void Clock(int cycles, int shift)
            {
                int effectivePeriod = period >> shift;
                if (cycles <= counter) { counter -= cycles; return; }
                cycles -= counter + 1;
                int divider = effectivePeriod + 1;
                int clocks = 1 + cycles / divider;
                counter = effectivePeriod - cycles % divider;
                for (int i = 0; i < clocks; i++)
                {
                    step++;
                    if (step >= 14)
                    {
                        step = 0;
                        accumulator = 0;
                    }
                    else if ((step & 1) == 0)
                        accumulator = (accumulator + rate) & 0xFF;
                }
            }
        }
    }

    /// <summary>Konami VRC6a used by Akumajou Densetsu.</summary>
    public sealed class Mapper024 : Vrc6Mapper
    {
        public Mapper024(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
            : base(prgRom, chrRom, initialMirroring, false)
        {
        }
    }
}
