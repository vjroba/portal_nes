using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Jaleco JF-13 used by Moero!! Pro Yakyuu. The external NEC D7756C
    /// sample ROM is not part of the iNES image, so audio commands are retained
    /// for diagnostics but do not synthesize speech.
    /// </summary>
    public sealed class Mapper086 : IMapper
    {
        private const int PrgBankSize = 32 * 1024;
        private const int ChrBankSize = 8 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private byte selectedPrgBank;
        private byte selectedChrBank;
        private byte audioControl;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => null;
        public bool IrqPending => false;
        public byte SelectedPrgBank => selectedPrgBank;
        public byte SelectedChrBank => selectedChrBank;
        public byte AudioTrack => (byte)(audioControl & 0x0F);
        public bool AudioResetReleased => (audioControl & 0x20) != 0;
        public bool AudioStartReleased => (audioControl & 0x10) != 0;

        public Mapper086(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length != 4 * PrgBankSize)
                throw new ArgumentException("Mapper 86 requires 128KB PRG ROM.", nameof(prgRom));
            if (chrRom.Length != 8 * ChrBankSize)
                throw new ArgumentException("Mapper 86 requires 64KB CHR ROM.", nameof(chrRom));
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
            int decoded = address & 0x7000;
            if (decoded == 0x6000)
            {
                selectedPrgBank = (byte)((value >> 4) & 0x03);
                selectedChrBank = (byte)((value & 0x03) | ((value & 0x40) >> 4));
            }
            else if (decoded == 0x7000)
            {
                audioControl = value;
            }
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
