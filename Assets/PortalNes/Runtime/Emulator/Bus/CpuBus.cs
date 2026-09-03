using System;
using PortalNes.Emulator.Mappers;
using PortalNes.Emulator.Ppu;
using PortalNes.Emulator.Input;
using PortalNes.Emulator.Apu;

namespace PortalNes.Emulator.Bus
{
    public sealed class CpuBus
    {
        private readonly byte[] ram = new byte[2048];
        private readonly IMapper mapper;
        private readonly Ppu2C02 ppu;
        private readonly Apu2A03 apu;
        private readonly Action synchronizeApu;
        private bool dmaPending;
        private byte dmaPage;
        private readonly NesController controller1;
        private readonly NesController controller2;

        public CpuBus(IMapper mapper, Ppu2C02 ppu, Apu2A03 apu = null,
            NesController controller1 = null, NesController controller2 = null, Action synchronizeApu = null)
        {
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            this.ppu = ppu ?? throw new ArgumentNullException(nameof(ppu));
            this.apu = apu;
            this.synchronizeApu = synchronizeApu;
            this.controller1 = controller1 ?? new NesController();
            this.controller2 = controller2 ?? new NesController();
        }

        public byte Read(ushort address)
        {
            if (address <= 0x1FFF) return ram[address & 0x07FF];
            if (address <= 0x3FFF) return ppu.CpuReadRegister(address);
            if (address == 0x4015) { synchronizeApu?.Invoke(); return apu?.ReadStatus() ?? 0; }
            if (address == 0x4016) return (byte)(0x40 | controller1.Read());
            if (address == 0x4017) return (byte)(0x40 | controller2.Read());
            if (address >= mapper.CpuAddressStart) return mapper.CpuRead(address);
            // PPU, APU and controller registers are connected in later milestones.
            return 0;
        }

        public void Write(ushort address, byte value)
        {
            if (address <= 0x1FFF) ram[address & 0x07FF] = value;
            else if (address <= 0x3FFF) ppu.CpuWriteRegister(address, value);
            else if (address == 0x4014) { dmaPage = value; dmaPending = true; }
            else if ((address >= 0x4000 && address <= 0x4013) || address == 0x4015 || address == 0x4017)
            { synchronizeApu?.Invoke(); apu?.WriteRegister(address, value); }
            else if (address == 0x4016) { controller1.WriteStrobe(value); controller2.WriteStrobe(value); }
            else if (address >= mapper.CpuAddressStart) mapper.CpuWrite(address, value);
        }

        public bool ExecutePendingDma()
        {
            if (!dmaPending) return false;
            Span<byte> data = stackalloc byte[256];
            ushort start = (ushort)(dmaPage << 8);
            for (int i = 0; i < 256; i++) data[i] = Read((ushort)(start + i));
            ppu.WriteOamDma(data);
            dmaPending = false;
            return true;
        }

        public byte ReadRam(ushort address) => ram[address & 0x07FF];
        public void WriteRam(ushort address, byte value) => ram[address & 0x07FF] = value;
    }
}
