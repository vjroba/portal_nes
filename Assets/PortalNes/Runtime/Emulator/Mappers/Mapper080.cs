using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>Taito X1-005 with 128 bytes of protected internal RAM.</summary>
    public sealed class Mapper080 : IMapper, IPpuPeekMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly byte[] internalRam = new byte[128];
        private readonly byte[] prgBanks = new byte[3];
        private readonly ushort[] chrBanks = new ushort[8];
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private bool internalRamEnabled;
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => false;
        public bool InternalRamEnabled => internalRamEnabled;
        public byte GetPrgBank(int index) => prgBanks[index];
        public ushort GetChrBank(int index) => chrBanks[index];

        public Mapper080(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length > 256 * 1024 ||
                prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("Mapper 80 PRG ROM must contain 32KB to 256KB in complete 8KB banks.", nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length > 256 * 1024 ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("Mapper 80 CHR ROM must contain 8KB to 256KB in complete 1KB banks.", nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            mirroring = initialMirroring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x7F00) return 0;
            if (address < 0x8000)
                return internalRamEnabled ? internalRam[address & 0x7F] : (byte)0;
            int slot = (address - 0x8000) / PrgBankSize;
            int bank = slot < 3 ? prgBanks[slot] : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address >= 0x7F00 && address < 0x8000)
            {
                if (internalRamEnabled) internalRam[address & 0x7F] = value;
                return;
            }
            if ((address & 0xFF70) != 0x7E70) return;

            // CPU A7 is not decoded: $7E70-$7E7F mirrors $7EF0-$7EFF.
            int register = 0x7E80 | (address & 0x7F);
            switch (register)
            {
                case 0x7EF0: SetTwoKilobyteChrBank(0, value); break;
                case 0x7EF1: SetTwoKilobyteChrBank(2, value); break;
                case 0x7EF2:
                case 0x7EF3:
                case 0x7EF4:
                case 0x7EF5:
                    chrBanks[4 + register - 0x7EF2] = (ushort)(value % chrBankCount);
                    break;
                case 0x7EF6:
                    mirroring = (value & 1) != 0
                        ? MirroringMode.Vertical
                        : MirroringMode.Horizontal;
                    break;
                case 0x7EF8:
                case 0x7EF9:
                    internalRamEnabled = value == 0xA3;
                    break;
                case 0x7EFA:
                case 0x7EFB:
                    prgBanks[0] = (byte)(value % prgBankCount);
                    break;
                case 0x7EFC:
                case 0x7EFD:
                    prgBanks[1] = (byte)(value % prgBankCount);
                    break;
                case 0x7EFE:
                case 0x7EFF:
                    prgBanks[2] = (byte)(value % prgBankCount);
                    break;
            }
        }

        private void SetTwoKilobyteChrBank(int slot, byte value)
        {
            int firstBank = (value & 0xFE) % chrBankCount;
            chrBanks[slot] = (ushort)firstBank;
            chrBanks[slot + 1] = (ushort)((firstBank + 1) % chrBankCount);
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return PpuPeek(address);
        }

        public byte PpuPeek(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int bank = chrBanks[address / ChrBankSize];
            return chrRom[bank * ChrBankSize + (address & 0x03FF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }
    }
}
