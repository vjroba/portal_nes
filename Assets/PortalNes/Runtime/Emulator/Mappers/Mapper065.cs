using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>Irem H3001 (iNES mapper 65).</summary>
    public sealed class Mapper065 : IMapper, ICpuClockedMapper, IPpuPeekMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly byte[] prgBanks = new byte[3];
        private readonly byte[] chrBanks = new byte[8];
        private ushort irqReload;
        private ushort irqCounter;
        private bool irqEnabled;
        private bool irqPending;
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => irqPending;
        public ushort IrqReload => irqReload;
        public ushort IrqCounter => irqCounter;
        public bool IrqEnabled => irqEnabled;
        public byte GetPrgBank(int index) => prgBanks[index];
        public byte GetChrBank(int index) => chrBanks[index];

        public Mapper065(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length > 256 * 1024 ||
                prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException(
                    "Mapper 65 PRG ROM must contain 32KB to 256KB in complete 8KB banks.",
                    nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length > 256 * 1024 ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException(
                    "Mapper 65 CHR ROM must contain 8KB to 256KB in complete 1KB banks.",
                    nameof(chrRom));

            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            prgBanks[0] = 0;
            prgBanks[1] = 1;
            prgBanks[2] = (byte)(prgBankCount - 2);
            mirroring = initialMirroring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = (address - 0x8000) / PrgBankSize;
            int bank = slot < 3 ? prgBanks[slot] : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            switch (address)
            {
                case 0x8000:
                    prgBanks[0] = (byte)(value % prgBankCount);
                    break;
                case 0x9001:
                    mirroring = (value & 0x80) != 0
                        ? MirroringMode.Horizontal
                        : MirroringMode.Vertical;
                    break;
                case 0x9003:
                    irqEnabled = (value & 0x80) != 0;
                    irqPending = false;
                    break;
                case 0x9004:
                    irqCounter = irqReload;
                    irqPending = false;
                    break;
                case 0x9005:
                    irqReload = (ushort)((irqReload & 0x00FF) | (value << 8));
                    break;
                case 0x9006:
                    irqReload = (ushort)((irqReload & 0xFF00) | value);
                    break;
                case 0xA000:
                    prgBanks[1] = (byte)(value % prgBankCount);
                    break;
                case 0xB000:
                case 0xB001:
                case 0xB002:
                case 0xB003:
                case 0xB004:
                case 0xB005:
                case 0xB006:
                case 0xB007:
                    chrBanks[address & 7] = (byte)(value % chrBankCount);
                    break;
                case 0xC000:
                    prgBanks[2] = (byte)(value % prgBankCount);
                    break;
            }
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

        public void ClockCpu(int cycles)
        {
            if (cycles <= 0 || !irqEnabled) return;
            if (irqCounter == 0 || cycles < irqCounter)
            {
                irqCounter = unchecked((ushort)(irqCounter - cycles));
                return;
            }

            irqCounter = 0;
            irqEnabled = false;
            irqPending = true;
        }
    }
}
