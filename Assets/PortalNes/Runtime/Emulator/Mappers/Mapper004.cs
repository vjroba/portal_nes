using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>MMC3: 8KB PRG and 1/2KB CHR banking with scanline IRQs.</summary>
    public sealed class Mapper004 : IMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chr;
        private readonly byte[] prgRam = new byte[8 * 1024];
        private readonly bool chrRam;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly byte[] registers = new byte[8];
        private byte bankSelect;
        private bool prgRamEnabled = true;
        private bool prgRamWriteProtected;
        private MirroringMode mirroring = MirroringMode.Vertical;
        private byte irqLatch;
        private byte irqCounter;
        private bool irqReload;
        private bool irqEnabled;
        private bool irqPending;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => irqPending;

        public Mapper004(byte[] prgRom, byte[] chr, bool chrRam)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chr = chr ?? throw new ArgumentNullException(nameof(chr));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("MMC3 PRG ROM must contain at least four complete 8KB banks.", nameof(prgRom));
            if (chr.Length < 8 * ChrBankSize || chr.Length % ChrBankSize != 0)
                throw new ArgumentException("MMC3 CHR memory must contain complete 1KB banks.", nameof(chr));
            this.chrRam = chrRam;
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chr.Length / ChrBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
                return prgRamEnabled ? prgRam[address - 0x6000] : (byte)0;
            int slot = (address - 0x8000) / PrgBankSize;
            int last = prgBankCount - 1;
            int secondLast = prgBankCount - 2;
            bool prgMode = (bankSelect & 0x40) != 0;
            int bank;
            switch (slot)
            {
                case 0: bank = prgMode ? secondLast : registers[6] % prgBankCount; break;
                case 1: bank = registers[7] % prgBankCount; break;
                case 2: bank = prgMode ? registers[6] % prgBankCount : secondLast; break;
                default: bank = last; break;
            }
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                if (prgRamEnabled && !prgRamWriteProtected) prgRam[address - 0x6000] = value;
                return;
            }
            bool odd = (address & 1) != 0;
            if (address < 0xA000)
            {
                if (!odd) bankSelect = value;
                else
                {
                    int target = bankSelect & 7;
                    registers[target] = target <= 1 ? (byte)(value & 0xFE) : value;
                }
            }
            else if (address < 0xC000)
            {
                if (!odd) mirroring = (value & 1) == 0 ? MirroringMode.Vertical : MirroringMode.Horizontal;
                else
                {
                    prgRamEnabled = (value & 0x80) != 0;
                    prgRamWriteProtected = (value & 0x40) != 0;
                }
            }
            else if (address < 0xE000)
            {
                if (!odd) irqLatch = value;
                else irqReload = true;
            }
            else if (!odd)
            {
                irqEnabled = false;
                irqPending = false;
            }
            else irqEnabled = true;
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return chr[MapChrAddress(address)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            if (chrRam) chr[MapChrAddress(address)] = value;
        }

        public void ClockScanline()
        {
            if (irqCounter == 0 || irqReload)
            {
                irqCounter = irqLatch;
                irqReload = false;
            }
            else irqCounter--;
            if (irqCounter == 0 && irqEnabled) irqPending = true;
        }

        private int MapChrAddress(ushort address)
        {
            int slot = address / ChrBankSize;
            bool inverted = (bankSelect & 0x80) != 0;
            int bank;
            if (!inverted)
            {
                if (slot < 2) bank = registers[0] + slot;
                else if (slot < 4) bank = registers[1] + slot - 2;
                else bank = registers[slot - 2];
            }
            else
            {
                if (slot < 4) bank = registers[slot + 2];
                else if (slot < 6) bank = registers[0] + slot - 4;
                else bank = registers[1] + slot - 6;
            }
            bank %= chrBankCount;
            return bank * ChrBankSize + (address & 0x03FF);
        }
    }
}
