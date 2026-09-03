using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>AxROM: switchable 32KB PRG bank and one-screen nametable selection.</summary>
    public sealed class Mapper007 : IMapper
    {
        private const int PrgBankSize = 32 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chr;
        private readonly bool chrRam;
        private readonly int prgBankCount;
        private int selectedPrgBank;
        private MirroringMode mirroring = MirroringMode.SingleScreenLower;

        public MirroringMode? MirroringOverride => mirroring;
        public ushort CpuAddressStart => 0x8000;
        public bool IrqPending => false;
        public void ClockScanline() { }
        public int SelectedPrgBank => selectedPrgBank;

        public Mapper007(byte[] prgRom, byte[] chr, bool chrRam)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chr = chr ?? throw new ArgumentNullException(nameof(chr));
            if (prgRom.Length < PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("AxROM PRG ROM must contain complete 32KB banks.", nameof(prgRom));
            if (chr.Length != 8192)
                throw new ArgumentException("AxROM CHR memory must be 8KB.", nameof(chr));
            this.chrRam = chrRam;
            prgBankCount = prgRom.Length / PrgBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            return prgRom[selectedPrgBank * PrgBankSize + (address & 0x7FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            selectedPrgBank = (value & 0x07) % prgBankCount;
            mirroring = (value & 0x10) == 0
                ? MirroringMode.SingleScreenLower
                : MirroringMode.SingleScreenUpper;
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
