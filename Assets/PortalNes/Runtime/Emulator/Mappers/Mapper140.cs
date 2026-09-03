using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Jaleco JF-11/JF-14: GNROM-style 32KB PRG and 8KB CHR banking,
    /// with its bank register decoded at $6000-$7FFF.
    /// </summary>
    public sealed class Mapper140 : IMapper
    {
        private const int PrgBankSize = 32 * 1024;
        private const int ChrBankSize = 8 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private byte selectedPrgBank;
        private byte selectedChrBank;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => null;
        public bool IrqPending => false;
        public byte SelectedPrgBank => selectedPrgBank;
        public byte SelectedChrBank => selectedChrBank;

        public Mapper140(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < PrgBankSize || prgRom.Length > 4 * PrgBankSize ||
                prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("Mapper 140 requires 32KB to 128KB PRG ROM in complete 32KB banks.", nameof(prgRom));
            if (chrRom.Length < ChrBankSize || chrRom.Length > 16 * ChrBankSize ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("Mapper 140 requires 8KB to 128KB CHR ROM in complete 8KB banks.", nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000) return 0;
            return prgRom[selectedPrgBank * PrgBankSize + (address & 0x7FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address >= 0x8000) return;
            selectedPrgBank = (byte)(((value >> 4) & 0x03) % prgBankCount);
            selectedChrBank = (byte)((value & 0x0F) % chrBankCount);
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
