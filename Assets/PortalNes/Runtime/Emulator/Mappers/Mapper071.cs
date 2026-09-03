using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Camerica/Codemasters BF909x: switchable 16KB PRG at $8000 and a fixed
    /// final bank at $C000. Fire Hawk's BF9097 variant also controls
    /// single-screen mirroring through writes at $9000-$9FFF.
    /// </summary>
    public sealed class Mapper071 : IMapper
    {
        private const int PrgBankSize = 16 * 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRam;
        private readonly int prgBankCount;
        private byte selectedPrgBank;
        private MirroringMode? mirroringOverride;

        public ushort CpuAddressStart => 0x8000;
        public MirroringMode? MirroringOverride => mirroringOverride;
        public bool IrqPending => false;
        public byte SelectedPrgBank => selectedPrgBank;

        public Mapper071(byte[] prgRom, byte[] chrRam)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRam = chrRam ?? throw new ArgumentNullException(nameof(chrRam));
            if (prgRom.Length < 2 * PrgBankSize || prgRom.Length > 16 * PrgBankSize ||
                prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException(
                    "Mapper 71 PRG ROM must contain 32KB to 256KB in complete 16KB banks.",
                    nameof(prgRom));
            if (chrRam.Length != 8192)
                throw new ArgumentException("Mapper 71 CHR RAM must be 8KB.", nameof(chrRam));
            prgBankCount = prgRom.Length / PrgBankSize;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));
            int bank = address < 0xC000 ? selectedPrgBank : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x3FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000) throw new ArgumentOutOfRangeException(nameof(address));

            // Old iNES headers cannot distinguish the normal BF9093 board
            // from Fire Hawk's BF9097. Ignore $8000-$8FFF writes (used by
            // ordinary games during startup), and enable the BF9097 behavior
            // only after the range Fire Hawk actually writes has been seen.
            if (address < 0xA000)
            {
                if (address >= 0x9000)
                    mirroringOverride = (value & 0x10) != 0
                        ? MirroringMode.SingleScreenUpper
                        : MirroringMode.SingleScreenLower;
                return;
            }

            if (address >= 0xC000)
                selectedPrgBank = (byte)((value & 0x0F) % prgBankCount);
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
    }
}
