using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Sunsoft-2 on the Sunsoft-3R board. Switches one 16KB PRG bank and
    /// controls access to a fixed 8KB CHR RAM.
    /// </summary>
    public sealed class Mapper093 : IMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRam;
        private readonly int prgBankCount;
        private byte selectedPrgBank;
        private bool chrRamEnabled;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => null;
        public bool IrqPending => false;
        public byte SelectedPrgBank => selectedPrgBank;
        public bool ChrRamEnabled => chrRamEnabled;

        public Mapper093(byte[] prgRom, byte[] chrRam)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRam = chrRam ?? throw new ArgumentNullException(nameof(chrRam));
            if (prgRom.Length != 8 * PrgBankSize)
                throw new ArgumentException("Mapper 93 requires 128KB PRG ROM.", nameof(prgRom));
            if (chrRam.Length != 8 * 1024)
                throw new ArgumentException("Mapper 93 requires 8KB CHR RAM.", nameof(chrRam));
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
            // The CPU and PRG ROM both drive the data bus on writes.
            byte effectiveValue = (byte)(value & CpuRead(address));
            selectedPrgBank = (byte)(((effectiveValue >> 4) & 0x07) % prgBankCount);
            chrRamEnabled = (effectiveValue & 0x01) != 0;
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return chrRamEnabled ? chrRam[address] : (byte)0;
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            if (chrRamEnabled) chrRam[address] = value;
        }

        public void ClockScanline() { }
    }
}
