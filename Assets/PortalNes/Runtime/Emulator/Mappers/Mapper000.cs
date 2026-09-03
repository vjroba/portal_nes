using System;
namespace PortalNes.Emulator.Mappers
{
    public sealed class Mapper000 : IMapper
    {
        private readonly byte[] prgRom, chrRom;
        public PortalNes.Emulator.Cartridge.MirroringMode? MirroringOverride => null;
        public ushort CpuAddressStart => 0x8000;
        public bool IrqPending => false;
        public void ClockScanline() { }
        public Mapper000(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom)); this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length != 16384 && prgRom.Length != 32768) throw new ArgumentException("NROM PRG ROM must be 16KB or 32KB.", nameof(prgRom));
            if (chrRom.Length != 8192) throw new ArgumentException("NROM CHR ROM must be 8KB.", nameof(chrRom));
        }
        public byte CpuRead(ushort address) { if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address)); return prgRom[(address - 0x8000) % prgRom.Length]; }
        public void CpuWrite(ushort address, byte value) { if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address)); }
        public byte PpuRead(ushort address) { if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address)); return chrRom[address]; }
        public void PpuWrite(ushort address, byte value) { if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address)); }
    }
}
