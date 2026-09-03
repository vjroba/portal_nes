using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>Jaleco SS88006 mapper.</summary>
    public sealed class Mapper018 : IMapper, ICpuClockedMapper
    {
        private const int PrgBankSize = 8192;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly byte[] prgRam = new byte[8192];
        private readonly byte[] prgBanks = new byte[3];
        private readonly byte[] chrBanks = new byte[8];
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private ushort irqReload;
        private ushort irqCounter;
        private byte irqControl;
        private bool irqPending;
        private bool prgRamEnabled;
        private bool prgRamWritable;
        private MirroringMode mirroring;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => irqPending;
        public ushort IrqReload => irqReload;
        public ushort IrqCounter => irqCounter;
        public byte IrqControl => irqControl;
        public byte GetPrgBank(int index) => prgBanks[index % 3];
        public byte GetChrBank(int index) => chrBanks[index & 7];

        public Mapper018(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length > 512 * 1024 ||
                prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("Mapper 18 requires 32KB to 512KB PRG ROM.", nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length > 256 * 1024 ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException("Mapper 18 requires 8KB to 256KB CHR ROM.", nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            mirroring = initialMirroring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
                return prgRamEnabled ? prgRam[address - 0x6000] : (byte)0;
            int slot = (address - 0x8000) / PrgBankSize;
            int bank = slot < 3 ? prgBanks[slot] % prgBankCount : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                if (prgRamEnabled && prgRamWritable) prgRam[address - 0x6000] = value;
                return;
            }

            ushort register = (ushort)(address & 0xF003);
            if (register >= 0x8000 && register <= 0x9001)
            {
                int slot = register < 0x9000 ? (register & 2) >> 1 : 2;
                WriteBankNibble(prgBanks, slot, (register & 1) != 0, value);
                return;
            }
            if (register == 0x9002)
            {
                prgRamEnabled = (value & 1) != 0;
                prgRamWritable = (value & 2) != 0;
                return;
            }
            if (register >= 0xA000 && register <= 0xD003)
            {
                int slot = ((register >> 12) - 0x0A) * 2 + ((register & 2) >> 1);
                WriteBankNibble(chrBanks, slot, (register & 1) != 0, value);
                return;
            }
            if (register >= 0xE000 && register <= 0xE003)
            {
                int shift = (register & 3) * 4;
                irqReload = (ushort)((irqReload & ~(0xF << shift)) | ((value & 0x0F) << shift));
                return;
            }
            switch (register)
            {
                case 0xF000:
                    irqCounter = irqReload;
                    irqPending = false;
                    break;
                case 0xF001:
                    irqControl = (byte)(value & 0x0F);
                    irqPending = false;
                    break;
                case 0xF002:
                    switch (value & 3)
                    {
                        case 0: mirroring = MirroringMode.Horizontal; break;
                        case 1: mirroring = MirroringMode.Vertical; break;
                        case 2: mirroring = MirroringMode.SingleScreenLower; break;
                        default: mirroring = MirroringMode.SingleScreenUpper; break;
                    }
                    break;
                // $F003 controls an optional external ADPCM chip.
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address >> 10;
            int bank = chrBanks[slot] % chrBankCount;
            return chrRom[bank * ChrBankSize + (address & 0x03FF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
        }

        public void ClockScanline() { }

        public void ClockCpu(int cycles)
        {
            if (cycles <= 0 || (irqControl & 1) == 0) return;
            int mask = (irqControl & 8) != 0 ? 0x000F :
                (irqControl & 4) != 0 ? 0x00FF :
                (irqControl & 2) != 0 ? 0x0FFF : 0xFFFF;
            int preserved = irqCounter & ~mask;
            int counter = irqCounter & mask;
            for (int i = 0; i < cycles; i++)
            {
                if (counter == 0)
                {
                    counter = mask;
                    irqPending = true;
                }
                else counter--;
            }
            irqCounter = (ushort)(preserved | counter);
        }

        private static void WriteBankNibble(byte[] banks, int index, bool high, byte value)
        {
            banks[index] = high
                ? (byte)((banks[index] & 0x0F) | ((value & 0x0F) << 4))
                : (byte)((banks[index] & 0xF0) | (value & 0x0F));
        }
    }
}
