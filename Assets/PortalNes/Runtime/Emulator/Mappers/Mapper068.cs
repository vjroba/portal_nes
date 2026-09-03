using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Sunsoft-4 mapper. In addition to conventional PRG/CHR banking it can
    /// replace CIRAM nametables with 1KB pages from CHR ROM.
    /// </summary>
    public sealed class Mapper068 : IMapper, INametableMemoryMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private const int ChrBankSize = 2 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly byte[] prgRam = new byte[8 * 1024];
        private readonly byte[] ciram = new byte[2 * 1024];
        private readonly int prgBankCount;
        private readonly int chr2KBankCount;
        private readonly int chr1KBankCount;
        private readonly byte[] chrBanks = new byte[4];
        private readonly byte[] nametableBanks = new byte[2];
        private byte nametableControl;
        private byte selectedPrgBank;
        private bool prgRamEnabled;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => DecodeMirroring(nametableControl & 0x03);
        public bool IrqPending => false;
        public byte SelectedPrgBank => selectedPrgBank;
        public bool UsesChrRomNametables => (nametableControl & 0x10) != 0;
        public byte GetChrBank(int slot) => chrBanks[slot & 3];
        public byte GetNametableBank(int slot) => nametableBanks[slot & 1];

        public Mapper068(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 2 * PrgBankSize || prgRom.Length > 16 * PrgBankSize ||
                prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("Mapper 68 requires 32KB to 256KB PRG ROM in complete 16KB banks.", nameof(prgRom));
            if (chrRom.Length < 8 * 1024 || chrRom.Length > 256 * 1024 ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("Mapper 68 requires 8KB to 256KB CHR ROM in complete 2KB banks.", nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chr2KBankCount = chrRom.Length / ChrBankSize;
            chr1KBankCount = chrRom.Length / 1024;
            nametableControl = initialMirroring == MirroringMode.Horizontal ? (byte)1 : (byte)0;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000) return prgRamEnabled ? prgRam[address - 0x6000] : (byte)0;
            int bank = address < 0xC000 ? selectedPrgBank : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x3FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                if (prgRamEnabled) prgRam[address - 0x6000] = value;
                return;
            }

            switch (address & 0xF000)
            {
                case 0x8000: chrBanks[0] = (byte)(value % chr2KBankCount); break;
                case 0x9000: chrBanks[1] = (byte)(value % chr2KBankCount); break;
                case 0xA000: chrBanks[2] = (byte)(value % chr2KBankCount); break;
                case 0xB000: chrBanks[3] = (byte)(value % chr2KBankCount); break;
                case 0xC000: nametableBanks[0] = (byte)(value & 0x7F); break;
                case 0xD000: nametableBanks[1] = (byte)(value & 0x7F); break;
                case 0xE000: nametableControl = value; break;
                case 0xF000:
                    selectedPrgBank = (byte)((value & 0x0F) % prgBankCount);
                    prgRamEnabled = (value & 0x10) != 0;
                    break;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address / ChrBankSize;
            return chrRom[chrBanks[slot] * ChrBankSize + (address & 0x07FF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public byte ReadNametable(ushort address)
        {
            int offset = (address - 0x2000) & 0x0FFF;
            int page = MapNametablePage(offset >> 10);
            int within = offset & 0x03FF;
            if (!UsesChrRomNametables) return ciram[page * 0x400 + within];
            int bank = (0x80 | nametableBanks[page]) % chr1KBankCount;
            return chrRom[bank * 0x400 + within];
        }

        public void WriteNametable(ushort address, byte value)
        {
            if (UsesChrRomNametables) return;
            int offset = (address - 0x2000) & 0x0FFF;
            int page = MapNametablePage(offset >> 10);
            ciram[page * 0x400 + (offset & 0x03FF)] = value;
        }

        public void ClockScanline() { }

        private int MapNametablePage(int table)
        {
            switch (nametableControl & 0x03)
            {
                case 0: return table & 1;
                case 1: return (table >> 1) & 1;
                case 2: return 0;
                default: return 1;
            }
        }

        private static MirroringMode DecodeMirroring(int mode)
        {
            switch (mode)
            {
                case 0: return MirroringMode.Vertical;
                case 1: return MirroringMode.Horizontal;
                case 2: return MirroringMode.SingleScreenLower;
                default: return MirroringMode.SingleScreenUpper;
            }
        }
    }
}
