using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Sunsoft FME-7 / 5A / 5B. Provides 1KB CHR banking, 8KB PRG banking,
    /// banked ROM/RAM at $6000, CPU-cycle IRQs and Sunsoft 5B PSG audio.
    /// </summary>
    public sealed class Mapper069 : IMapper, ICpuClockedMapper, IExpansionAudioMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly byte[] prgRam = new byte[8 * 1024];
        private readonly byte[] prgBanks = new byte[4];
        private readonly byte[] chrBanks = new byte[8];
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly Sunsoft5BAudio audio = new Sunsoft5BAudio();
        private byte command;
        private bool lowWindowRam;
        private bool lowWindowEnabled;
        private bool irqCounterEnabled;
        private bool irqEnabled;
        private bool irqPending;
        private ushort irqCounter;
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => irqPending;
        public float ExpansionAudioSample => audio.Output;
        public ushort IrqCounter => irqCounter;
        public bool IrqCounterEnabled => irqCounterEnabled;
        public bool IrqEnabled => irqEnabled;
        public byte Command => command;
        public byte GetPrgBank(int index) => prgBanks[index & 3];
        public byte GetChrBank(int index) => chrBanks[index & 7];

        public Mapper069(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length > 512 * 1024 ||
                prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("Mapper 69 requires 32KB to 512KB PRG ROM in complete 8KB banks.", nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length > 256 * 1024 ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("Mapper 69 requires 8KB to 256KB CHR ROM in complete 1KB banks.", nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            mirroring = initialMirroring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                if (!lowWindowEnabled) return 0;
                if (lowWindowRam) return prgRam[address - 0x6000];
                int lowBank = prgBanks[0] % prgBankCount;
                return prgRom[lowBank * PrgBankSize + (address & 0x1FFF)];
            }

            int slot = (address - 0x8000) / PrgBankSize;
            int bank = slot < 3 ? prgBanks[slot + 1] % prgBankCount : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                if (lowWindowEnabled && lowWindowRam) prgRam[address - 0x6000] = value;
                return;
            }

            switch (address & 0xE000)
            {
                case 0x8000:
                    command = (byte)(value & 0x0F);
                    break;
                case 0xA000:
                    ExecuteCommand(value);
                    break;
                case 0xC000:
                    audio.SelectRegister(value);
                    break;
                case 0xE000:
                    audio.Write(value);
                    break;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address >> 10;
            int bank = chrBanks[slot] % chrBankCount;
            return chrRom[bank * ChrBankSize + (address & 0x03FF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }

        public void ClockCpu(int cycles)
        {
            if (cycles <= 0 || !irqCounterEnabled) return;
            int remaining = cycles;
            while (remaining > 0)
            {
                int untilWrap = irqCounter + 1;
                if (remaining < untilWrap)
                {
                    irqCounter -= (ushort)remaining;
                    return;
                }
                remaining -= untilWrap;
                irqCounter = 0xFFFF;
                if (irqEnabled) irqPending = true;
            }
        }

        public void ClockAudio(int cycles) => audio.Clock(cycles);

        private void ExecuteCommand(byte value)
        {
            if (command < 8)
            {
                chrBanks[command] = (byte)(value % chrBankCount);
                return;
            }
            if (command == 8)
            {
                prgBanks[0] = (byte)((value & 0x3F) % prgBankCount);
                lowWindowRam = (value & 0x40) != 0;
                lowWindowEnabled = !lowWindowRam || (value & 0x80) != 0;
                return;
            }
            if (command <= 11)
            {
                prgBanks[command - 8] = (byte)((value & 0x3F) % prgBankCount);
                return;
            }
            if (command == 12)
            {
                switch (value & 3)
                {
                    case 0: mirroring = MirroringMode.Vertical; break;
                    case 1: mirroring = MirroringMode.Horizontal; break;
                    case 2: mirroring = MirroringMode.SingleScreenLower; break;
                    default: mirroring = MirroringMode.SingleScreenUpper; break;
                }
                return;
            }
            if (command == 13)
            {
                irqCounterEnabled = (value & 0x80) != 0;
                irqEnabled = (value & 0x01) != 0;
                irqPending = false;
                return;
            }
            if (command == 14)
                irqCounter = (ushort)((irqCounter & 0xFF00) | value);
            else
                irqCounter = (ushort)((irqCounter & 0x00FF) | (value << 8));
        }

        private sealed class Sunsoft5BAudio
        {
            private static readonly float[] Volume =
            {
                0.0000f, 0.0078f, 0.0110f, 0.0156f,
                0.0221f, 0.0312f, 0.0442f, 0.0625f,
                0.0884f, 0.1250f, 0.1768f, 0.2500f,
                0.3536f, 0.5000f, 0.7071f, 1.0000f
            };
            private readonly byte[] registers = new byte[16];
            private readonly int[] toneCounter = new int[3];
            private readonly bool[] toneHigh = new bool[3];
            private byte selectedRegister;
            private bool writeEnabled = true;
            private int divider;
            private int noiseCounter;
            private int envelopeCounter;
            private int envelopeLevel;
            private int envelopeDirection = -1;
            private bool envelopeHolding;
            private uint noiseShift = 0x1FFFF;

            public float Output { get; private set; }

            public void SelectRegister(byte value)
            {
                selectedRegister = (byte)(value & 0x0F);
                writeEnabled = (value & 0xF0) == 0;
            }

            public void Write(byte value)
            {
                if (!writeEnabled) return;
                registers[selectedRegister] = MaskValue(selectedRegister, value);
                if (selectedRegister == 13) ResetEnvelope();
            }

            public void Clock(int cycles)
            {
                if (cycles <= 0) return;
                divider += cycles;
                while (divider >= 16)
                {
                    divider -= 16;
                    ClockGenerators();
                }
                UpdateOutput();
            }

            private void ClockGenerators()
            {
                for (int channel = 0; channel < 3; channel++)
                {
                    int period = registers[channel * 2] | (registers[channel * 2 + 1] << 8);
                    if (period == 0) period = 1;
                    if (++toneCounter[channel] >= period)
                    {
                        toneCounter[channel] = 0;
                        toneHigh[channel] = !toneHigh[channel];
                    }
                }

                int noisePeriod = registers[6] & 0x1F;
                if (noisePeriod == 0) noisePeriod = 1;
                if (++noiseCounter >= noisePeriod)
                {
                    noiseCounter = 0;
                    uint feedback = ((noiseShift >> 16) ^ (noiseShift >> 13)) & 1;
                    noiseShift = ((noiseShift << 1) | feedback) & 0x1FFFF;
                }

                int envelopePeriod = registers[11] | (registers[12] << 8);
                if (envelopePeriod == 0) envelopePeriod = 1;
                if (++envelopeCounter >= envelopePeriod)
                {
                    envelopeCounter = 0;
                    ClockEnvelope();
                }
            }

            private void UpdateOutput()
            {
                int mixer = registers[7];
                bool noiseHigh = (noiseShift & 0x10000) != 0;
                float sum = 0;
                for (int channel = 0; channel < 3; channel++)
                {
                    bool tonePass = (mixer & (1 << channel)) != 0 || toneHigh[channel];
                    bool noisePass = (mixer & (1 << (channel + 3))) != 0 || noiseHigh;
                    if (!tonePass || !noisePass) continue;
                    byte volume = registers[8 + channel];
                    int level = (volume & 0x10) != 0 ? (envelopeLevel >> 1) : volume & 0x0F;
                    sum += Volume[level];
                }
                Output = sum * 2.0f;
            }

            private void ResetEnvelope()
            {
                int shape = registers[13] & 0x0F;
                bool attack = (shape & 4) != 0;
                envelopeLevel = attack ? 0 : 31;
                envelopeDirection = attack ? 1 : -1;
                envelopeCounter = 0;
                envelopeHolding = false;
            }

            private void ClockEnvelope()
            {
                if (envelopeHolding) return;
                envelopeLevel += envelopeDirection;
                if (envelopeLevel >= 0 && envelopeLevel <= 31) return;

                int shape = registers[13] & 0x0F;
                bool continueFlag = (shape & 8) != 0;
                bool alternate = (shape & 2) != 0;
                bool hold = (shape & 1) != 0;
                if (!continueFlag)
                {
                    envelopeLevel = 0;
                    envelopeHolding = true;
                    return;
                }
                if (alternate) envelopeDirection = -envelopeDirection;
                envelopeLevel = envelopeDirection > 0 ? 0 : 31;
                if (hold) envelopeHolding = true;
            }

            private static byte MaskValue(int register, byte value)
            {
                if (register == 1 || register == 3 || register == 5 || register == 13)
                    return (byte)(value & 0x0F);
                if (register == 6) return (byte)(value & 0x1F);
                if (register >= 8 && register <= 10) return (byte)(value & 0x1F);
                return value;
            }
        }
    }
}
