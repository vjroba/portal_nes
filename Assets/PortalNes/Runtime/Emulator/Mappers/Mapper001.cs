using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>MMC1: serial control register, PRG/CHR banking, and 8KB PRG RAM.</summary>
    public sealed class Mapper001 : IMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private const int ChrBankSize = 4 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chr;
        private readonly byte[] prgRam = new byte[8 * 1024];
        private readonly bool chrRam;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private byte shift = 0x10;
        private byte control = 0x0C;
        private byte chrBank0;
        private byte chrBank1;
        private byte prgBank;

        public ushort CpuAddressStart => 0x6000;
        public bool IrqPending => false;
        public void ClockScanline() { }
        public MirroringMode? MirroringOverride
        {
            get
            {
                switch (control & 3)
                {
                    case 0: return MirroringMode.SingleScreenLower;
                    case 1: return MirroringMode.SingleScreenUpper;
                    case 2: return MirroringMode.Vertical;
                    default: return MirroringMode.Horizontal;
                }
            }
        }

        public Mapper001(byte[] prgRom, byte[] chr, bool chrRam)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chr = chr ?? throw new ArgumentNullException(nameof(chr));
            if (prgRom.Length < 2 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("MMC1 PRG ROM must contain complete 16KB banks.", nameof(prgRom));
            if (chr.Length < 2 * ChrBankSize || chr.Length % ChrBankSize != 0)
                throw new ArgumentException("MMC1 CHR memory must contain complete 4KB banks.", nameof(chr));
            this.chrRam = chrRam;
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chr.Length / ChrBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
                return PrgRamEnabled ? prgRam[address - 0x6000] : (byte)0;

            int mode = (control >> 2) & 3;
            int bank;
            if (mode <= 1)
            {
                int first = (prgBank & 0x0E) % prgBankCount;
                bank = (first + ((address - 0x8000) / PrgBankSize)) % prgBankCount;
            }
            else if (mode == 2)
                bank = address < 0xC000 ? 0 : (prgBank & 0x0F) % prgBankCount;
            else
                bank = address < 0xC000 ? (prgBank & 0x0F) % prgBankCount : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x3FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                if (PrgRamEnabled) prgRam[address - 0x6000] = value;
                return;
            }
            if ((value & 0x80) != 0)
            {
                shift = 0x10;
                control |= 0x0C;
                return;
            }
            bool complete = (shift & 1) != 0;
            shift = (byte)((shift >> 1) | ((value & 1) << 4));
            if (!complete) return;
            if (address < 0xA000) control = shift;
            else if (address < 0xC000) chrBank0 = shift;
            else if (address < 0xE000) chrBank1 = shift;
            else prgBank = shift;
            shift = 0x10;
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return chr[MapChrAddress(address)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            if (chrRam) chr[MapChrAddress(address)] = value;
        }

        private bool PrgRamEnabled => (prgBank & 0x10) == 0;

        private int MapChrAddress(ushort address)
        {
            if ((control & 0x10) == 0)
            {
                int first = (chrBank0 & 0x1E) % chrBankCount;
                return ((first + address / ChrBankSize) % chrBankCount) * ChrBankSize +
                    (address & 0x0FFF);
            }
            int bank = (address < 0x1000 ? chrBank0 : chrBank1) % chrBankCount;
            return bank * ChrBankSize + (address & 0x0FFF);
        }
    }
}
