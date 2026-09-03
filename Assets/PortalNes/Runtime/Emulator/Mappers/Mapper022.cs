using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Konami VRC2a. Provides two switchable 8KB PRG banks, eight
    /// switchable 1KB CHR banks, and horizontal/vertical mirroring.
    /// </summary>
    public sealed class Mapper022 : IMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly byte[] chrRegisters = new byte[8];
        private byte prgBank0;
        private byte prgBank1;
        private byte latch;
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => false;

        public Mapper022(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException(
                    "Mapper 22 PRG ROM must contain at least four complete 8KB banks.",
                    nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException(
                    "Mapper 22 CHR ROM must contain at least eight complete 1KB banks.",
                    nameof(chrRom));

            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            mirroring = initialMirroring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x7000) return latch;
            if (address < 0x8000) return 0;

            int slot = (address - 0x8000) / PrgBankSize;
            int bank;
            switch (slot)
            {
                case 0: bank = prgBank0 % prgBankCount; break;
                case 1: bank = prgBank1 % prgBankCount; break;
                case 2: bank = prgBankCount - 2; break;
                default: bank = prgBankCount - 1; break;
            }
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x7000)
            {
                latch = (byte)(value & 1);
                return;
            }
            if (address < 0x8000) return;

            switch (address & 0xF000)
            {
                case 0x8000:
                    prgBank0 = (byte)(value & 0x1F);
                    return;
                case 0x9000:
                    mirroring = (value & 1) == 0
                        ? MirroringMode.Vertical
                        : MirroringMode.Horizontal;
                    return;
                case 0xA000:
                    prgBank1 = (byte)(value & 0x1F);
                    return;
                case 0xB000:
                case 0xC000:
                case 0xD000:
                case 0xE000:
                    WriteChrRegister(address, value);
                    return;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address / ChrBankSize;
            // VRC2a does not connect CHR A10 to the mapper's low output bit.
            int bank = (chrRegisters[slot] >> 1) % chrBankCount;
            return chrRom[bank * ChrBankSize + (address & 0x03FF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }

        private void WriteChrRegister(ushort address, byte value)
        {
            // VRC2a swaps CPU A0/A1 before they reach the mapper.
            int register = ((address >> 1) & 1) | ((address & 1) << 1);
            int pair = ((address >> 12) - 0x0B) * 2;
            int slot = pair + (register >> 1);
            if ((register & 1) == 0)
                chrRegisters[slot] = (byte)((chrRegisters[slot] & 0xF0) | (value & 0x0F));
            else
                chrRegisters[slot] = (byte)((chrRegisters[slot] & 0x0F) | ((value & 0x0F) << 4));
        }
    }
}
