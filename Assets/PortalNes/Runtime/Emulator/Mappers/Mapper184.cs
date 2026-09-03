using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Sunsoft-1. PRG is fixed while a register in the cartridge expansion
    /// area independently selects the lower and upper 4KB CHR banks.
    /// </summary>
    public sealed class Mapper184 : IMapper
    {
        private const int ChrBankSize = 4 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int chrBankCount;
        private byte lowerChrBank;
        private byte upperChrBank;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => null;
        public bool IrqPending => false;
        public byte LowerChrBank => lowerChrBank;
        public byte UpperChrBank => upperChrBank;

        public Mapper184(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length != 16 * 1024 && prgRom.Length != 32 * 1024)
                throw new ArgumentException("Mapper 184 requires 16KB or 32KB PRG ROM.", nameof(prgRom));
            if (chrRom.Length < 2 * ChrBankSize || chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("Mapper 184 requires complete 4KB CHR ROM banks.", nameof(chrRom));
            chrBankCount = chrRom.Length / ChrBankSize;
            upperChrBank = (byte)(1 % chrBankCount);
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000) return 0;
            return prgRom[(address - 0x8000) % prgRom.Length];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address >= 0x8000) return;
            lowerChrBank = (byte)((value & 0x07) % chrBankCount);
            upperChrBank = (byte)(((value >> 4) & 0x07) % chrBankCount);
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int bank = address < 0x1000 ? lowerChrBank : upperChrBank;
            return chrRom[bank * ChrBankSize + (address & 0x0FFF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }
    }
}
