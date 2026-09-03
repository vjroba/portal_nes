using System;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Namcot 3425. Namcot 108 ROM banking with CHR bank bit 5 wired to
    /// CIRAM A10, allowing each horizontal nametable pair to select CIRAM A/B.
    /// </summary>
    public sealed class Mapper095 : IMapper, INametableMappingMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
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

        public Mapper095(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException(
                    "Mapper 95 PRG ROM must contain at least four complete 8KB banks.",
                    nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length > 64 * 1024 ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException(
                    "Mapper 95 CHR ROM must contain 8KB to 64KB in complete 1KB banks.",
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

            registers[selectedRegister] = selectedRegister <= 1
                ? (byte)(value & 0x3E)
                : (byte)(value & 0x3F);
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return chrRom[MapChrAddress(address)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public int MapNametableAddress(ushort address)
        {
            int offset = (address - 0x2000) & 0x0FFF;
            int table = offset / 0x400;
            int register = table < 2 ? 0 : 1;
            int physical = (registers[register] >> 5) & 1;
            return physical * 0x400 + (offset & 0x03FF);
        }

        public void ClockScanline() { }

        private int MapChrAddress(ushort address)
        {
            int slot = address / ChrBankSize;
            int bank;
            if (slot < 2) bank = registers[0] + slot;
            else if (slot < 4) bank = registers[1] + slot - 2;
            else bank = registers[slot - 2];
            bank %= chrBankCount;
            return bank * ChrBankSize + (address & 0x03FF);
        }
    }
}
