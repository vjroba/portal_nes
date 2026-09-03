using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>MMC2 (PxROM): 8KB PRG banking and latch-selected 4KB CHR banks.</summary>
    public sealed class Mapper009 : IMapper, IPpuPeekMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 4 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly byte[] chrBanks = new byte[4];
        private int selectedPrgBank;
        private byte latch0 = 0xFE, latch1 = 0xFE;
        private long latch0Transitions, latch1Transitions;
        private long latch0FdTriggers, latch0FeTriggers;
        private long latch1FdTriggers, latch1FeTriggers;
        private long mirroringWrites;
        private MirroringMode mirroring = MirroringMode.Vertical;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => false;
        public byte Latch0 => latch0;
        public byte Latch1 => latch1;
        public int SelectedPrgBank => selectedPrgBank;
        public byte ChrFd0 => chrBanks[0];
        public byte ChrFe0 => chrBanks[1];
        public byte ChrFd1 => chrBanks[2];
        public byte ChrFe1 => chrBanks[3];
        public long Latch0Transitions => latch0Transitions;
        public long Latch1Transitions => latch1Transitions;
        public long Latch0FdTriggers => latch0FdTriggers;
        public long Latch0FeTriggers => latch0FeTriggers;
        public long Latch1FdTriggers => latch1FdTriggers;
        public long Latch1FeTriggers => latch1FeTriggers;
        public long MirroringWrites => mirroringWrites;

        public Mapper009(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("MMC2 PRG ROM must contain at least four complete 8KB banks.", nameof(prgRom));
            if (chrRom.Length < 4 * ChrBankSize || chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("MMC2 CHR ROM must contain complete 4KB banks.", nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = (address - 0x8000) / PrgBankSize;
            int bank = slot == 0 ? selectedPrgBank : prgBankCount - 4 + slot;
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            switch (address >> 12)
            {
                case 0xA: selectedPrgBank = (value & 0x0F) % prgBankCount; break;
                case 0xB: chrBanks[0] = (byte)((value & 0x1F) % chrBankCount); break;
                case 0xC: chrBanks[1] = (byte)((value & 0x1F) % chrBankCount); break;
                case 0xD: chrBanks[2] = (byte)((value & 0x1F) % chrBankCount); break;
                case 0xE: chrBanks[3] = (byte)((value & 0x1F) % chrBankCount); break;
                case 0xF:
                    mirroringWrites++;
                    mirroring = (value & 1) == 0
                        ? MirroringMode.Vertical : MirroringMode.Horizontal;
                    break;
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
            // MMC2 decodes only $0FD8 for the low FD trigger; its other
            // low-table trigger is likewise the exact $0FE8 address.
            // The high pattern table uses eight-address trigger ranges.
            byte old0 = latch0, old1 = latch1;
            if (address == 0x0FD8)
            {
                latch0FdTriggers++;
                latch0 = 0xFD;
            }
            else if (address == 0x0FE8)
            {
                latch0FeTriggers++;
                latch0 = 0xFE;
            }
            else if ((address & 0x1FF8) == 0x1FD8)
            {
                latch1FdTriggers++;
                latch1 = 0xFD;
            }
            else if ((address & 0x1FF8) == 0x1FE8)
            {
                latch1FeTriggers++;
                latch1 = 0xFE;
            }
            if (latch0 != old0) latch0Transitions++;
            if (latch1 != old1) latch1Transitions++;
        }
    }
}
