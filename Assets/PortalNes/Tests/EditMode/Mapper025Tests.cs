using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper025Tests
    {
        [Test]
        public void UsesVrc4bSwappedLowAddressLines()
        {
            var mapper = CreateMapper();
            // Slot 0 low/high are x000/x002; slot 1 low/high are x001/x003.
            mapper.CpuWrite(0xB000, 3);
            mapper.CpuWrite(0xB002, 0);
            mapper.CpuWrite(0xB001, 4);
            mapper.CpuWrite(0xB003, 0);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x0400), Is.EqualTo(0x44));
        }

        [Test]
        public void UsesVrc4dA3A2AddressLines()
        {
            var mapper = CreateMapper();
            // Slot 0 low/high are x000/x008; slot 1 low/high are x004/x00C.
            mapper.CpuWrite(0xB000, 5);
            mapper.CpuWrite(0xB008, 0);
            mapper.CpuWrite(0xB004, 6);
            mapper.CpuWrite(0xB00C, 0);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x45));
            Assert.That(mapper.PpuRead(0x0400), Is.EqualTo(0x46));
        }

        [Test]
        public void DecodesSwapControlAndIrqThroughVrc4bWiring()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 2);
            mapper.CpuWrite(0x9001, 2);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x16));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x12));

            mapper.CpuWrite(0xF000, 0x0E);
            mapper.CpuWrite(0xF002, 0x0F);
            mapper.CpuWrite(0xF001, 0x07);
            mapper.ClockCpu(2);
            Assert.That(mapper.IrqPending, Is.True);
            mapper.CpuWrite(0xF003, 0);
            Assert.That(mapper.IrqPending, Is.False);
        }

        private static Mapper025 CreateMapper()
        {
            var prg = new byte[8 * 8192];
            var chr = new byte[32 * 1024];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 32; bank++)
                for (int i = 0; i < 1024; i++)
                    chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper025(prg, chr, MirroringMode.Horizontal);
        }
    }
}
