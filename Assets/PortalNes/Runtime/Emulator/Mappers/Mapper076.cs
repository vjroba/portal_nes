using System;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Namcot 3446. Uses the Namcot 108 PRG banking core but rewires
    /// registers 2-5 as four independent 2KB CHR banks.
    /// </summary>
    public sealed class Mapper076 : IMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 2 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly byte[] registers = new byte[8];
        private byte selectedRegister;

        public ushort CpuAddressStart => 0x8000;
        public Cartridge.MirroringMode? MirroringOverride => null;
        public bool IrqPending => false;
        public byte SelectedRegister => selectedRegister;
        public byte GetBankRegister(int index) => registers[index & 7];

        public Mapper076(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException(
                    "Mapper 76 PRG ROM must contain at least four complete 8KB banks.",
                    nameof(prgRom));
            if (chrRom.Length < 4 * ChrBankSize || chrRom.Length > 128 * 1024 ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException(
                    "Mapper 76 CHR ROM must contain 8KB to 128KB in complete 2KB banks.",
                    nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = (address - 0x8000) / PrgBankSize;
            int bank;
            switch (slot)
            {
                case 0: bank = registers[6] % prgBankCount; break;
                case 1: bank = registers[7] % prgBankCount; break;
                case 2: bank = prgBankCount - 2; break;
                default: bank = prgBankCount - 1; break;
            }
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address >= 0xA000) return;
            if ((address & 1) == 0)
            {
                selectedRegister = (byte)(value & 7);
                return;
            }

            registers[selectedRegister] = (byte)(value & 0x3F);
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address / ChrBankSize;
            int bank = registers[slot + 2] % chrBankCount;
            return chrRom[bank * ChrBankSize + (address & 0x07FF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }
    }
}
