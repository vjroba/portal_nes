using System;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>UxROM: switchable 16KB PRG bank at $8000 and fixed last bank at $C000.</summary>
    public sealed class Mapper002 : IMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chr;
        private readonly bool chrRam;
        private readonly int prgBankCount;
        private int selectedPrgBank;
        public PortalNes.Emulator.Cartridge.MirroringMode? MirroringOverride => null;
        public ushort CpuAddressStart => 0x8000;
        public bool IrqPending => false;
        public void ClockScanline() { }

        public Mapper002(byte[] prgRom, byte[] chr, bool chrRam)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chr = chr ?? throw new ArgumentNullException(nameof(chr));
            if (prgRom.Length < PrgBankSize * 2 || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("UxROM PRG ROM must contain at least two complete 16KB banks.", nameof(prgRom));
            if (chr.Length != 8192) throw new ArgumentException("UxROM CHR memory must be 8KB.", nameof(chr));
            this.chrRam = chrRam;
            prgBankCount = prgRom.Length / PrgBankSize;
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
            selectedPrgBank = value % prgBankCount;
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return chr[address];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            if (chrRam) chr[address] = value;
        }
    }
}
