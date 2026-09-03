using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Mapper 78 discrete logic. Holy Diver uses H/V mirroring while
    /// Uchuusen: Cosmo Carrier uses the same latch bit for one-screen mirroring.
    /// </summary>
    public sealed class Mapper078 : IMapper, IPpuPeekMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private const int ChrBankSize = 8 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly bool holyDiverBoard;
        private byte selectedPrgBank;
        private byte selectedChrBank;
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => false;
        public byte SelectedPrgBank => selectedPrgBank;
        public byte SelectedChrBank => selectedChrBank;
        public bool UsesHolyDiverMirroring => holyDiverBoard;

        public Mapper078(byte[] prgRom, byte[] chrRom, bool holyDiverBoard)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length != 8 * PrgBankSize)
                throw new ArgumentException("Mapper 78 requires 128KB PRG ROM.", nameof(prgRom));
            if (chrRom.Length != 16 * ChrBankSize)
                throw new ArgumentException("Mapper 78 requires 128KB CHR ROM.", nameof(chrRom));
            this.holyDiverBoard = holyDiverBoard;
            mirroring = holyDiverBoard ? MirroringMode.Horizontal : MirroringMode.SingleScreenLower;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            int bank = address < 0xC000 ? selectedPrgBank : 7;
            return prgRom[bank * PrgBankSize + (address & 0x3FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));

            // Both original boards leave PRG ROM enabled during writes.
            byte effectiveValue = (byte)(value & CpuRead(address));
            selectedPrgBank = (byte)(effectiveValue & 0x07);
            selectedChrBank = (byte)(effectiveValue >> 4);
            bool alternate = (effectiveValue & 0x08) != 0;
            if (holyDiverBoard)
                mirroring = alternate ? MirroringMode.Vertical : MirroringMode.Horizontal;
            else
                mirroring = alternate ? MirroringMode.SingleScreenUpper : MirroringMode.SingleScreenLower;
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return PpuPeek(address);
        }

        public byte PpuPeek(ushort address)
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
