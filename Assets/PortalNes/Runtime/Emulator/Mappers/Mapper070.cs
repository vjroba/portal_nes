using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>Bandai 74161/7432: 16KB PRG and 8KB CHR banking.</summary>
    public sealed class Mapper070 : IMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private const int ChrBankSize = 8 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly bool hasBusConflicts;
        private byte selectedPrgBank;
        private byte selectedChrBank;
        private bool mirroringControlEnabled;
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => false;
        public byte SelectedPrgBank => selectedPrgBank;
        public byte SelectedChrBank => selectedChrBank;
        public bool MirroringControlEnabled => mirroringControlEnabled;
        public bool HasBusConflicts => hasBusConflicts;

        public Mapper070(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring,
            bool hasBusConflicts = true)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 2 * PrgBankSize || prgRom.Length > 8 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("Mapper 70 PRG ROM must contain 32KB to 128KB in complete 16KB banks.", nameof(prgRom));
            if (chrRom.Length < ChrBankSize || chrRom.Length > 16 * ChrBankSize || chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("Mapper 70 CHR ROM must contain 8KB to 128KB in complete 8KB banks.", nameof(chrRom));
            this.hasBusConflicts = hasBusConflicts;
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            mirroringControlEnabled = initialMirroring == MirroringMode.FourScreen;
            mirroring = mirroringControlEnabled ? MirroringMode.SingleScreenLower : initialMirroring;
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
            byte effectiveValue = hasBusConflicts ? (byte)(value & CpuRead(address)) : value;
            selectedPrgBank = (byte)(((effectiveValue >> 4) & 7) % prgBankCount);
            selectedChrBank = (byte)((effectiveValue & 15) % chrBankCount);
            if ((effectiveValue & 0x80) != 0) mirroringControlEnabled = true;
            if (mirroringControlEnabled)
                mirroring = (effectiveValue & 0x80) != 0
                    ? MirroringMode.SingleScreenUpper : MirroringMode.SingleScreenLower;
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
