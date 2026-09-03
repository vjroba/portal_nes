using System;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// J87 discrete mapper: fixed 16/32KB PRG ROM and one switchable
    /// 8KB CHR ROM bank with reversed bank-select bit order.
    /// </summary>
    public sealed class Mapper087 : IMapper
    {
        private const int ChrBankSize = 8 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int chrBankCount;
        private byte selectedChrBank;

        public ushort CpuAddressStart => 0x6000;
        public Cartridge.MirroringMode? MirroringOverride => null;
        public bool IrqPending => false;
        public byte SelectedChrBank => selectedChrBank;

        public Mapper087(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length != 16 * 1024 && prgRom.Length != 32 * 1024)
                throw new ArgumentException("Mapper 87 requires 16KB or 32KB PRG ROM.", nameof(prgRom));
            if (chrRom.Length < 2 * ChrBankSize || chrRom.Length > 4 * ChrBankSize ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("Mapper 87 requires 16KB to 32KB CHR ROM.", nameof(chrRom));
            chrBankCount = chrRom.Length / ChrBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000) return 0;
            int offset = prgRom.Length == 16 * 1024
                ? (address - 0x8000) & 0x3FFF
                : address - 0x8000;
            return prgRom[offset];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                // The PCB wires D1 to the low CHR bank bit and D0 to the high bit.
                selectedChrBank = (byte)(((value & 1) << 1) | ((value & 2) >> 1));
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int bank = selectedChrBank % chrBankCount;
            return chrRom[bank * ChrBankSize + address];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }
    }
}
