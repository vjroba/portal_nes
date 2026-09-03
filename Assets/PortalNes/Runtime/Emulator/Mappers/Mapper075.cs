using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Konami VRC1: three switchable 8KB PRG banks, two switchable
    /// 4KB CHR banks, and horizontal/vertical mirroring.
    /// </summary>
    public sealed class Mapper075 : IMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 4 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly byte[] prgBanks = new byte[3];
        private readonly byte[] chrBanks = new byte[2];
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => false;

        public Mapper075(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException(
                    "VRC1 PRG ROM must contain at least four complete 8KB banks.",
                    nameof(prgRom));
            if (chrRom.Length < 2 * ChrBankSize || chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException(
                    "VRC1 CHR ROM must contain at least two complete 4KB banks.",
                    nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            mirroring = initialMirroring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = (address - 0x8000) / PrgBankSize;
            int bank = slot < 3 ? prgBanks[slot] % prgBankCount : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            switch (address & 0xF000)
            {
                case 0x8000:
                    prgBanks[0] = (byte)(value & 0x0F);
                    break;
                case 0x9000:
                    mirroring = (value & 1) == 0
                        ? MirroringMode.Vertical
                        : MirroringMode.Horizontal;
                    chrBanks[0] = (byte)((chrBanks[0] & 0x0F) | ((value & 2) << 3));
                    chrBanks[1] = (byte)((chrBanks[1] & 0x0F) | ((value & 4) << 2));
                    break;
                case 0xA000:
                    prgBanks[1] = (byte)(value & 0x0F);
                    break;
                case 0xC000:
                    prgBanks[2] = (byte)(value & 0x0F);
                    break;
                case 0xE000:
                    chrBanks[0] = (byte)((chrBanks[0] & 0x10) | (value & 0x0F));
                    break;
                case 0xF000:
                    chrBanks[1] = (byte)((chrBanks[1] & 0x10) | (value & 0x0F));
                    break;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address / ChrBankSize;
            int bank = chrBanks[slot] % chrBankCount;
            return chrRom[bank * ChrBankSize + (address & 0x0FFF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }
    }
}
