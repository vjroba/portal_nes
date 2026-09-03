using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Namco 175/340, cost-reduced derivatives of the Namco 163.
    /// Both provide eight 1KB CHR banks and three switchable 8KB PRG banks.
    /// The 175 optionally provides WRAM; the 340 provides mirroring control.
    /// </summary>
    public sealed class Mapper210 : IMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly byte[] prgRam = new byte[8 * 1024];
        private readonly byte[] chrBanks = new byte[8];
        private readonly byte[] prgBanks = new byte[3];
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly bool namco340;
        private MirroringMode mirroring;
        private bool prgRamEnabled;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => namco340 ? mirroring : null;
        public bool IrqPending => false;
        public bool IsNamco340 => namco340;
        public bool PrgRamEnabled => prgRamEnabled;
        public byte GetChrBank(int index) => chrBanks[index & 7];
        public byte GetPrgBank(int index) => prgBanks[index % 3];

        public Mapper210(byte[] prgRom, byte[] chrRom, MirroringMode cartridgeMirroring, bool useNamco340)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException(
                    "Mapper 210 PRG ROM must contain at least four complete 8KB banks.",
                    nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length > 256 * 1024 ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException(
                    "Mapper 210 CHR ROM must contain 8KB to 256KB in complete 1KB banks.",
                    nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            namco340 = useNamco340;
            mirroring = cartridgeMirroring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
                return !namco340 && prgRamEnabled ? prgRam[(address - 0x6000) & 0x1FFF] : (byte)0;

            int slot = (address - 0x8000) / PrgBankSize;
            int bank = slot < 3 ? prgBanks[slot] % prgBankCount : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                if (!namco340 && prgRamEnabled)
                    prgRam[(address - 0x6000) & 0x1FFF] = value;
                return;
            }

            if (address < 0xC000)
            {
                int slot = (address - 0x8000) / 0x0800;
                chrBanks[slot] = value;
                return;
            }

            if (address < 0xC800)
            {
                if (!namco340) prgRamEnabled = (value & 1) != 0;
                return;
            }
            if (address < 0xE000) return;

            if (address < 0xE800)
            {
                prgBanks[0] = (byte)(value & 0x3F);
                if (namco340)
                {
                    switch (value >> 6)
                    {
                        case 0: mirroring = MirroringMode.SingleScreenLower; break;
                        case 1: mirroring = MirroringMode.Vertical; break;
                        case 2: mirroring = MirroringMode.SingleScreenUpper; break;
                        default: mirroring = MirroringMode.Horizontal; break;
                    }
                }
            }
            else if (address < 0xF000)
            {
                prgBanks[1] = (byte)(value & 0x3F);
            }
            else if (address < 0xF800)
            {
                prgBanks[2] = (byte)(value & 0x3F);
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address / ChrBankSize;
            int bank = chrBanks[slot] % chrBankCount;
            return chrRom[bank * ChrBankSize + (address & 0x03FF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }
    }
}
