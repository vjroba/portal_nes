using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>Taito TC0690: mapper 33-style banking with scanline IRQs.</summary>
    public sealed class Mapper048 : IMapper, IPpuPeekMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly byte[] prgBanks = new byte[2];
        private readonly ushort[] chrBanks = new ushort[8];
        private MirroringMode mirroring;
        private byte irqLatch;
        private byte irqCounter;
        private bool irqReload;
        private bool irqEnabled;
        private bool irqPending;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => irqPending;
        public byte GetPrgBank(int index) => prgBanks[index];
        public ushort GetChrBank(int index) => chrBanks[index];
        public byte IrqLatch => irqLatch;
        public byte IrqCounter => irqCounter;
        public bool IrqEnabled => irqEnabled;

        public Mapper048(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length > 512 * 1024 ||
                prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("Mapper 48 PRG ROM must contain 32KB to 512KB in complete 8KB banks.", nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length > 512 * 1024 ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("Mapper 48 CHR ROM must contain 8KB to 512KB in complete 1KB banks.", nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            mirroring = initialMirroring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = (address - 0x8000) / PrgBankSize;
            int bank = slot < 2 ? prgBanks[slot] : prgBankCount - (4 - slot);
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            int register = address & 0xE003;
            switch (register)
            {
                case 0x8000: prgBanks[0] = (byte)(value % prgBankCount); break;
                case 0x8001: prgBanks[1] = (byte)(value % prgBankCount); break;
                case 0x8002: SetTwoKilobyteChrBank(0, value); break;
                case 0x8003: SetTwoKilobyteChrBank(2, value); break;
                case 0xA000:
                case 0xA001:
                case 0xA002:
                case 0xA003:
                    chrBanks[4 + (register & 3)] = (ushort)(value % chrBankCount);
                    break;
                case 0xC000: irqLatch = (byte)(value ^ 0xFF); break;
                case 0xC001: irqReload = true; break;
                case 0xC002: irqEnabled = true; break;
                case 0xC003: irqEnabled = false; irqPending = false; break;
                case 0xE000:
                    mirroring = (value & 0x40) != 0
                        ? MirroringMode.Horizontal
                        : MirroringMode.Vertical;
                    break;
            }
        }

        private void SetTwoKilobyteChrBank(int slot, byte value)
        {
            int firstBank = value * 2 % chrBankCount;
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
    }
}
