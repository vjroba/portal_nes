using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Konami VRC3: one switchable 16KB PRG bank, fixed 8KB CHR RAM,
    /// 8KB PRG RAM, and a CPU-cycle-driven 8/16-bit IRQ counter.
    /// </summary>
    public sealed class Mapper073 : IMapper, ICpuClockedMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRam;
        private readonly byte[] prgRam = new byte[8 * 1024];
        private readonly int prgBankCount;
        private byte selectedPrgBank;
        private ushort irqLatch;
        private ushort irqCounter;
        private bool irqEnableAfterAcknowledgement;
        private bool irqEnabled;
        private bool irqEightBitMode;
        private bool irqPending;

        public ushort CpuAddressStart => 0x6000;
        public MirroringMode? MirroringOverride => null;
        public bool IrqPending => irqPending;

        public Mapper073(byte[] prgRom, byte[] chrRam)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRam = chrRam ?? throw new ArgumentNullException(nameof(chrRam));
            if (prgRom.Length < 2 * PrgBankSize || prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException(
                    "VRC3 PRG ROM must contain at least two complete 16KB banks.",
                    nameof(prgRom));
            if (chrRam.Length != 8 * 1024)
                throw new ArgumentException("VRC3 requires exactly 8KB CHR RAM.", nameof(chrRam));
            prgBankCount = prgRom.Length / PrgBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x6000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x8000) return prgRam[address - 0x6000];
            int bank = address < 0xC000
                ? selectedPrgBank % prgBankCount
                : prgBankCount - 1;
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

            switch (address & 0xF000)
            {
                case 0x8000: WriteIrqLatchNibble(0, value); break;
                case 0x9000: WriteIrqLatchNibble(4, value); break;
                case 0xA000: WriteIrqLatchNibble(8, value); break;
                case 0xB000: WriteIrqLatchNibble(12, value); break;
                case 0xC000:
                    irqEnableAfterAcknowledgement = (value & 1) != 0;
                    irqEnabled = (value & 2) != 0;
                    irqEightBitMode = (value & 4) != 0;
                    irqPending = false;
                    if (irqEnabled) irqCounter = irqLatch;
                    break;
                case 0xD000:
                    irqPending = false;
                    irqEnabled = irqEnableAfterAcknowledgement;
                    break;
                case 0xF000:
                    selectedPrgBank = (byte)(value & 7);
                    break;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return chrRam[address];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            chrRam[address] = value;
        }

        public void ClockScanline() { }

        public void ClockCpu(int cycles)
        {
            if (cycles <= 0 || !irqEnabled) return;
            while (cycles > 0)
            {
                int current = irqEightBitMode ? irqCounter & 0xFF : irqCounter;
                int period = irqEightBitMode ? 0x100 : 0x10000;
                int untilOverflow = period - current;
                if (cycles < untilOverflow)
                {
                    if (irqEightBitMode)
                        irqCounter = (ushort)((irqCounter & 0xFF00) | ((current + cycles) & 0xFF));
                    else
                        irqCounter = (ushort)(current + cycles);
                    return;
                }

                cycles -= untilOverflow;
                if (irqEightBitMode)
                    irqCounter = (ushort)((irqCounter & 0xFF00) | (irqLatch & 0x00FF));
                else
                    irqCounter = irqLatch;
                irqPending = true;
            }
        }

        private void WriteIrqLatchNibble(int shift, byte value)
        {
            int mask = 0xF << shift;
            irqLatch = (ushort)((irqLatch & ~mask) | ((value & 0x0F) << shift));
        }
    }
}
