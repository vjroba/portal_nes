using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>Irem G-101 (iNES mapper 32).</summary>
    public sealed class Mapper032 : IMapper, IPpuPeekMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly byte[] workRam = new byte[8 * 1024];
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly byte[] chrBanks = new byte[8];
        private readonly bool majorLeagueBoard;
        private byte prgBank0;
        private byte prgBank1;
        private bool swapPrg;
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => false;
        public byte PrgBank0 => prgBank0;
        public byte PrgBank1 => prgBank1;
        public bool SwapPrg => swapPrg && !majorLeagueBoard;
        public byte GetChrBank(int index) => chrBanks[index & 7];

        public Mapper032(byte[] prgRom, byte[] chrRom, MirroringMode cartridgeMirroring,
            bool majorLeagueBoard = false)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length > 256 * 1024 ||
                prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException(
                    "Mapper 32 PRG ROM must contain 32KB to 256KB in complete 8KB banks.",
                    nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length > 256 * 1024 ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException(
                    "Mapper 32 CHR ROM must contain 8KB to 256KB in complete 1KB banks.",
                    nameof(chrRom));

            this.majorLeagueBoard = majorLeagueBoard;
            mirroring = majorLeagueBoard ? MirroringMode.SingleScreenUpper : cartridgeMirroring;
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000) return workRam[address - 0x6000];
            int slot = (address - 0x8000) / PrgBankSize;
            int bank;
            switch (slot)
            {
                case 0: bank = swapPrg && !majorLeagueBoard ? prgBankCount - 2 : prgBank0; break;
                case 1: bank = prgBank1; break;
                case 2: bank = swapPrg && !majorLeagueBoard ? prgBank0 : prgBankCount - 2; break;
                default: bank = prgBankCount - 1; break;
            }
            bank %= prgBankCount;
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                workRam[address - 0x6000] = value;
                return;
            }
            switch (address & 0xF000)
            {
                case 0x8000:
                    prgBank0 = (byte)(value % prgBankCount);
                    break;
                case 0x9000:
                    if (majorLeagueBoard) break;
                    mirroring = (value & 0x01) != 0
                        ? MirroringMode.Horizontal
                        : MirroringMode.Vertical;
                    swapPrg = (value & 0x02) != 0;
                    break;
                case 0xA000:
                    prgBank1 = (byte)(value % prgBankCount);
                    break;
                case 0xB000:
                    chrBanks[address & 7] = (byte)(value % chrBankCount);
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
            int bank = chrBanks[address / ChrBankSize] % chrBankCount;
            return chrRom[bank * ChrBankSize + (address & 0x03FF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }
    }
}
