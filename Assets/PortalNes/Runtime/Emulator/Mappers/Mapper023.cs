using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>Shared Konami VRC2/VRC4 implementation.</summary>
    public abstract class Vrc2Vrc4Mapper : IMapper, ICpuClockedMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly byte[] prgRam = new byte[8 * 1024];
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private readonly int addressWiring;
        private readonly ushort[] chrRegisters = new ushort[8];
        private byte prgBank0;
        private byte prgBank1;
        private bool prgSwapMode;
        private bool prgRamEnabled;
        private byte vrc2Latch;
        private MirroringMode mirroring;
        private byte irqLatch;
        private byte irqCounter;
        private int irqPrescaler = 341;
        private bool irqEnableAfterAcknowledgement;
        private bool irqEnabled;
        private bool irqCycleMode;
        private bool irqPending;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => irqPending;

        protected Vrc2Vrc4Mapper(
            byte[] prgRom,
            byte[] chrRom,
            MirroringMode initialMirroring,
            int addressWiring)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException(
                    "VRC2/VRC4 PRG ROM must contain at least four complete 8KB banks.",
                    nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException(
                    "VRC2/VRC4 CHR ROM must contain at least eight complete 1KB banks.",
                    nameof(chrRom));

            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            mirroring = initialMirroring;
            this.addressWiring = addressWiring;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                if (prgRamEnabled) return prgRam[address - 0x6000];
                return address < 0x7000 ? vrc2Latch : (byte)0;
            }

            int slot = (address - 0x8000) / PrgBankSize;
            int secondLast = prgBankCount - 2;
            int bank;
            switch (slot)
            {
                case 0: bank = prgSwapMode ? secondLast : prgBank0 % prgBankCount; break;
                case 1: bank = prgBank1 % prgBankCount; break;
                case 2: bank = prgSwapMode ? prgBank0 % prgBankCount : secondLast; break;
                default: bank = prgBankCount - 1; break;
            }
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000)
            {
                if (prgRamEnabled) prgRam[address - 0x6000] = value;
                else if (address < 0x7000) vrc2Latch = (byte)(value & 1);
                return;
            }

            int group = address & 0xF000;
            int register = DecodeRegister(address);
            switch (group)
            {
                case 0x8000:
                    prgBank0 = (byte)(value & 0x1F);
                    return;
                case 0x9000:
                    WriteControl(register, value);
                    return;
                case 0xA000:
                    prgBank1 = (byte)(value & 0x1F);
                    return;
                case 0xB000:
                case 0xC000:
                case 0xD000:
                case 0xE000:
                    WriteChrRegister(group, register, value);
                    return;
                case 0xF000:
                    WriteIrqRegister(register, value);
                    return;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address / ChrBankSize;
            int bank = chrRegisters[slot] % chrBankCount;
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
            if (irqCycleMode)
            {
                for (int i = 0; i < cycles; i++) ClockIrqCounter();
                return;
            }

            irqPrescaler -= cycles * 3;
            while (irqPrescaler <= 0)
            {
                irqPrescaler += 341;
                ClockIrqCounter();
            }
        }

        private int DecodeRegister(ushort address)
        {
            if (addressWiring == 0)
            {
                // Mapper 23: VRC2b/VRC4f use A0/A1, VRC4e uses A2/A3.
                int direct = address & 3;
                return direct != 0 ? direct : (address >> 2) & 3;
            }

            if (addressWiring == 1)
            {
                // Mapper 25: VRC2c/VRC4b swap A0/A1. VRC4d uses A3/A2,
                // producing x000, x008, x004, x00C.
                int low = address & 3;
                if (low != 0) return ((low >> 1) & 1) | ((low & 1) << 1);
                return ((address >> 3) & 1) | ((address >> 1) & 2);
            }

            // Mapper 21: VRC4a uses A1/A2 (x000, x002, x004, x006);
            // VRC4c uses A6/A7 (x000, x040, x080, x0C0).
            int vrc4a = (address >> 1) & 3;
            return vrc4a != 0 ? vrc4a : (address >> 6) & 3;
        }

        private void WriteControl(int register, byte value)
        {
            if (register == 0)
            {
                switch (value & 3)
                {
                    case 0: mirroring = MirroringMode.Vertical; break;
                    case 1: mirroring = MirroringMode.Horizontal; break;
                    case 2: mirroring = MirroringMode.SingleScreenLower; break;
                    default: mirroring = MirroringMode.SingleScreenUpper; break;
                }
            }
            else if (register == 2)
            {
                prgRamEnabled = (value & 1) != 0;
                prgSwapMode = (value & 2) != 0;
            }
        }

        private void WriteChrRegister(int group, int register, byte value)
        {
            int pair = ((group >> 12) - 0x0B) * 2;
            int slot = pair + (register >> 1);
            if ((register & 1) == 0)
                chrRegisters[slot] = (ushort)((chrRegisters[slot] & 0x1F0) | (value & 0x0F));
            else
                chrRegisters[slot] = (ushort)((chrRegisters[slot] & 0x00F) | ((value & 0x1F) << 4));
        }

        private void WriteIrqRegister(int register, byte value)
        {
            switch (register)
            {
                case 0:
                    irqLatch = (byte)((irqLatch & 0xF0) | (value & 0x0F));
                    break;
                case 1:
                    irqLatch = (byte)((irqLatch & 0x0F) | ((value & 0x0F) << 4));
                    break;
                case 2:
                    irqEnableAfterAcknowledgement = (value & 1) != 0;
                    irqEnabled = (value & 2) != 0;
                    irqCycleMode = (value & 4) != 0;
                    irqPending = false;
                    irqPrescaler = 341;
                    if (irqEnabled) irqCounter = irqLatch;
                    break;
                case 3:
                    irqPending = false;
                    irqEnabled = irqEnableAfterAcknowledgement;
                    break;
            }
        }

        private void ClockIrqCounter()
        {
            if (irqCounter == 0xFF)
            {
                irqCounter = irqLatch;
                irqPending = true;
            }
            else irqCounter++;
        }
    }

    /// <summary>
    /// Konami VRC2b/VRC4e-compatible mapper. Legacy iNES mapper 23 ROMs
    /// are treated as VRC4 and accept both VRC4e and VRC4f register wiring.
    /// </summary>
    public sealed class Mapper023 : Vrc2Vrc4Mapper
    {
        public Mapper023(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
            : base(prgRom, chrRom, initialMirroring, 0)
        {
        }
    }
}
