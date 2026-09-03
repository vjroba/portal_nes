using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>Irem TAM-S1, used by Kaiketsu Yanchamaru (iNES mapper 97).</summary>
    public sealed class Mapper097 : IMapper, IPpuPeekMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRam;
        private byte selectedPrgBank;
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => false;
        public byte SelectedPrgBank => selectedPrgBank;

        public Mapper097(byte[] prgRom, byte[] chrRam, MirroringMode initialMirroring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRam = chrRam ?? throw new ArgumentNullException(nameof(chrRam));
            if (prgRom.Length != 16 * PrgBankSize)
                throw new ArgumentException("Mapper 97 requires 256KB PRG ROM.", nameof(prgRom));
            if (chrRam.Length != 8 * 1024)
                throw new ArgumentException("Mapper 97 requires 8KB CHR RAM.", nameof(chrRam));
            selectedPrgBank = 15;
            mirroring = initialMirroring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            int bank = address < 0xC000 ? 15 : selectedPrgBank;
            return prgRom[bank * PrgBankSize + (address & 0x3FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            selectedPrgBank = (byte)(value & 0x0F);
            switch (value >> 6)
            {
                case 0: mirroring = MirroringMode.SingleScreenLower; break;
                case 1: mirroring = MirroringMode.Horizontal; break;
                case 2: mirroring = MirroringMode.Vertical; break;
                default: mirroring = MirroringMode.SingleScreenUpper; break;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return chrRam[address];
        }

        public byte PpuPeek(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return chrRam[address];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            chrRam[address] = value;
        }

        public void ClockScanline() { }
    }
}
