using System;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>CNROM: fixed NROM-style PRG mapping and switchable 8KB CHR bank.</summary>
    public sealed class Mapper003 : IMapper
    {
        private const int ChrBankSize = 8 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int chrBankCount;
        private readonly bool emulateBusConflicts;
        private int selectedChrBank;
        public PortalNes.Emulator.Cartridge.MirroringMode? MirroringOverride => null;
        public ushort CpuAddressStart => 0x8000;
        public bool IrqPending => false;
        public void ClockScanline() { }

        public Mapper003(byte[] prgRom, byte[] chrRom, bool emulateBusConflicts = true)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length != 16384 && prgRom.Length != 32768)
                throw new ArgumentException("CNROM PRG ROM must be 16KB or 32KB.", nameof(prgRom));
            if (chrRom.Length < ChrBankSize || chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("CNROM CHR ROM must contain complete 8KB banks.", nameof(chrRom));
            chrBankCount = chrRom.Length / ChrBankSize;
            this.emulateBusConflicts = emulateBusConflicts;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            return prgRom[(address - 0x8000) % prgRom.Length];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            // Original CNROM boards do not disable PRG-ROM output while the
            // CPU writes the bank latch. Zero bits driven by either side win,
            // so the latch observes the bitwise AND of both values.
            byte effectiveValue = emulateBusConflicts ? (byte)(value & CpuRead(address)) : value;
            selectedChrBank = effectiveValue % chrBankCount;
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
    }
}
