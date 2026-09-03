using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Sunsoft-2 on the Sunsoft-3 board. A single bus-conflicted register
    /// selects a 16KB PRG bank, an 8KB CHR bank and one-screen mirroring.
    /// </summary>
    public sealed class Mapper089 : IMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private const int ChrBankSize = 8 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private byte selectedPrgBank;
        private byte selectedChrBank;
        private MirroringMode mirroring = MirroringMode.SingleScreenLower;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => false;
        public byte SelectedPrgBank => selectedPrgBank;
        public byte SelectedChrBank => selectedChrBank;

        public Mapper089(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 2 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("Mapper 89 requires complete 16KB PRG ROM banks.", nameof(prgRom));
            if (chrRom.Length < ChrBankSize || chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("Mapper 89 requires complete 8KB CHR ROM banks.", nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
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
            byte effectiveValue = (byte)(value & CpuRead(address));
            selectedPrgBank = (byte)(((effectiveValue >> 4) & 0x07) % prgBankCount);
            int chrBank = (effectiveValue & 0x07) | ((effectiveValue >> 4) & 0x08);
            selectedChrBank = (byte)(chrBank % chrBankCount);
            mirroring = (effectiveValue & 0x08) != 0
                ? MirroringMode.SingleScreenUpper
                : MirroringMode.SingleScreenLower;
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return chrRom[selectedChrBank * ChrBankSize + address];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }
    }
}
