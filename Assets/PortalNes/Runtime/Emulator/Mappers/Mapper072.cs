using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Jaleco JF-17: edge-triggered 16KB PRG and 8KB CHR bank latches.
    /// The optional external sample player is cartridge hardware and is not
    /// represented here.
    /// </summary>
    public sealed class Mapper072 : IMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private const int ChrBankSize = 8 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private byte previousCommand;
        private byte selectedPrgBank;
        private byte selectedChrBank;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => null;
        public bool IrqPending => false;
        public byte SelectedPrgBank => selectedPrgBank;
        public byte SelectedChrBank => selectedChrBank;

        public Mapper072(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length != 8 * PrgBankSize)
                throw new ArgumentException("Mapper 72 requires 128KB PRG ROM.", nameof(prgRom));
            if (chrRom.Length != 16 * ChrBankSize)
                throw new ArgumentException("Mapper 72 requires 128KB CHR ROM.", nameof(chrRom));
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

            // JF-17 has bus conflicts: ROM and CPU both drive the data bus.
            byte effectiveValue = (byte)(value & CpuRead(address));
            byte risingEdges = (byte)(effectiveValue & ~previousCommand);
            if ((risingEdges & 0x80) != 0)
                selectedPrgBank = (byte)((effectiveValue & 0x0F) % prgBankCount);
            if ((risingEdges & 0x40) != 0)
                selectedChrBank = (byte)((effectiveValue & 0x0F) % chrBankCount);
            previousCommand = effectiveValue;
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
