using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Sunsoft-3: one switchable 16KB PRG bank, four 2KB CHR banks,
    /// mapper-controlled mirroring and a one-shot CPU-cycle IRQ counter.
    /// </summary>
    public sealed class Mapper067 : IMapper, ICpuClockedMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private const int ChrBankSize = 2 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly byte[] chrBanks = new byte[4];
        private byte selectedPrgBank;
        private ushort irqCounter;
        private bool irqHighByteNext = true;
        private bool irqEnabled;
        private bool irqPending;
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => irqPending;
        public byte SelectedPrgBank => selectedPrgBank;
        public ushort IrqCounter => irqCounter;
        public bool IrqEnabled => irqEnabled;
        public byte GetChrBank(int index) => chrBanks[index];

        public Mapper067(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 2 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("Mapper 67 requires complete 16KB PRG ROM banks.", nameof(prgRom));
            if (chrRom.Length < 4 * ChrBankSize || chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("Mapper 67 requires complete 2KB CHR ROM banks.", nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            mirroring = initialMirroring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            int bank = address < 0xC000 ? selectedPrgBank : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x3FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            switch (address & 0xF800)
            {
                case 0x8000:
                    irqPending = false;
                    break;
                case 0x8800:
                case 0x9800:
                case 0xA800:
                case 0xB800:
                    int slot = ((address >> 12) - 8);
                    chrBanks[slot] = (byte)((value & 0x3F) % chrBankCount);
                    break;
                case 0xC800:
                    if (irqHighByteNext)
                        irqCounter = (ushort)((irqCounter & 0x00FF) | (value << 8));
                    else
                        irqCounter = (ushort)((irqCounter & 0xFF00) | value);
                    irqHighByteNext = !irqHighByteNext;
                    break;
                case 0xD800:
                    irqEnabled = (value & 0x10) != 0;
                    irqHighByteNext = true;
                    break;
                case 0xE800:
                    switch (value & 3)
                    {
                        case 0: mirroring = MirroringMode.Vertical; break;
                        case 1: mirroring = MirroringMode.Horizontal; break;
                        case 2: mirroring = MirroringMode.SingleScreenLower; break;
                        default: mirroring = MirroringMode.SingleScreenUpper; break;
                    }
                    break;
                case 0xF800:
                    selectedPrgBank = (byte)((value & 0x0F) % prgBankCount);
                    break;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address / ChrBankSize;
            return chrRom[chrBanks[slot] * ChrBankSize + (address & (ChrBankSize - 1))];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }

        public void ClockCpu(int cycles)
        {
            if (cycles <= 0 || !irqEnabled) return;
            int untilWrap = irqCounter + 1;
            if (cycles < untilWrap)
            {
                irqCounter -= (ushort)cycles;
                return;
            }

            irqCounter = 0xFFFF;
            irqEnabled = false;
            irqPending = true;
        }
    }
}
