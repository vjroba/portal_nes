using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>MMC4 (FxROM): 16KB PRG banking, PRG RAM, and latch-selected 4KB CHR banks.</summary>
    public sealed class Mapper010 : IMapper, IPpuPeekMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private const int ChrBankSize = 4 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly byte[] prgRam = new byte[8 * 1024];
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly byte[] chrBanks = new byte[4];
        private int selectedPrgBank;
        private byte latch0 = 0xFE, latch1 = 0xFE;
        private MirroringMode mirroring = MirroringMode.Vertical;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => false;
        public byte Latch0 => latch0;
        public byte Latch1 => latch1;
        public int SelectedPrgBank => selectedPrgBank;

        public Mapper010(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 2 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("MMC4 PRG ROM must contain at least two complete 16KB banks.", nameof(prgRom));
            if (chrRom.Length < 4 * ChrBankSize || chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("MMC4 CHR ROM must contain complete 4KB banks.", nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000) return prgRam[address - 0x6000];
            int bank = address < 0xC000 ? selectedPrgBank : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x3FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                prgRam[address - 0x6000] = value;
                return;
            }
            switch (address >> 12)
            {
                case 0xA: selectedPrgBank = (value & 0x0F) % prgBankCount; break;
                case 0xB: chrBanks[0] = (byte)((value & 0x1F) % chrBankCount); break;
                case 0xC: chrBanks[1] = (byte)((value & 0x1F) % chrBankCount); break;
                case 0xD: chrBanks[2] = (byte)((value & 0x1F) % chrBankCount); break;
                case 0xE: chrBanks[3] = (byte)((value & 0x1F) % chrBankCount); break;
                case 0xF: mirroring = (value & 1) == 0
                    ? MirroringMode.Vertical : MirroringMode.Horizontal; break;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            byte result = PpuPeek(address);
            UpdateLatch(address);
            return result;
        }

        public byte PpuPeek(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int half = address >> 12;
            int register = half * 2 + ((half == 0 ? latch0 : latch1) == 0xFE ? 1 : 0);
            return chrRom[chrBanks[register] * ChrBankSize + (address & 0x0FFF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }

        private void UpdateLatch(ushort address)
        {
            if ((address & 0x1FF8) == 0x0FD8) latch0 = 0xFD;
            else if ((address & 0x1FF8) == 0x0FE8) latch0 = 0xFE;
            else if ((address & 0x1FF8) == 0x1FD8) latch1 = 0xFD;
            else if ((address & 0x1FF8) == 0x1FE8) latch1 = 0xFE;
        }
    }
}
