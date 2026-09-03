using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Namcot 3453: mapper 88 banking with bit 6 of every mapper write
    /// connected to one-screen nametable selection.
    /// </summary>
    public sealed class Mapper154 : IMapper
    {
        private readonly Mapper088 banking;
        private MirroringMode mirroring = MirroringMode.SingleScreenLower;
        private ushort lastWriteAddress;
        private byte lastWriteValue;
        private int mirroringWrites;

        public ushort CpuAddressStart => banking.CpuAddressStart;
        public MirroringMode? MirroringOverride => mirroring;
        public bool IrqPending => false;
        public byte SelectedRegister => banking.SelectedRegister;
        public ushort LastWriteAddress => lastWriteAddress;
        public byte LastWriteValue => lastWriteValue;
        public int MirroringWrites => mirroringWrites;
        public byte GetBankRegister(int index) => banking.GetBankRegister(index);

        public Mapper154(byte[] prgRom, byte[] chrRom)
        {
            banking = new Mapper088(prgRom, chrRom);
        }

        public byte CpuRead(ushort address) => banking.CpuRead(address);

        public void CpuWrite(ushort address, byte value)
        {
            lastWriteAddress = address;
            lastWriteValue = value;
            mirroringWrites++;
            mirroring = (value & 0x40) == 0
                ? MirroringMode.SingleScreenLower
                : MirroringMode.SingleScreenUpper;
            banking.CpuWrite(address, value);
        }

        public byte PpuRead(ushort address) => banking.PpuRead(address);
        public void PpuWrite(ushort address, byte value) => banking.PpuWrite(address, value);
        public void ClockScanline() { }
    }
}
