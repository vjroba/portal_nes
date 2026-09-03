using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>GxROM: switchable 32KB PRG and 8KB CHR banks.</summary>
    public sealed class Mapper066 : IMapper
    {
        private const int PrgBankSize = 32 * 1024;
        private const int ChrBankSize = 8 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chr;
        private readonly bool chrRam;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private int selectedPrgBank;
        private int selectedChrBank;

        public MirroringMode? MirroringOverride => null;
        public ushort CpuAddressStart => 0x8000;
        public bool IrqPending => false;
        public void ClockScanline() { }

        public Mapper066(byte[] prgRom, byte[] chr, bool chrRam)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chr = chr ?? throw new ArgumentNullException(nameof(chr));
            if (prgRom.Length < PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("GxROM PRG ROM must contain complete 32KB banks.", nameof(prgRom));
            if (chr.Length < ChrBankSize || chr.Length % ChrBankSize != 0)
                throw new ArgumentException("GxROM CHR memory must contain complete 8KB banks.", nameof(chr));
            this.chrRam = chrRam;
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chr.Length / ChrBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            return prgRom[selectedPrgBank * PrgBankSize + (address & 0x7FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            selectedPrgBank = ((value >> 4) & 0x03) % prgBankCount;
            selectedChrBank = (value & 0x03) % chrBankCount;
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return chr[selectedChrBank * ChrBankSize + address];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            if (chrRam) chr[selectedChrBank * ChrBankSize + address] = value;
        }
    }
}
