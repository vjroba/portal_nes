using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper048Tests
    {
        [Test]
        public void SelectsPrgChrBanksAndMirroring()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 3);
            mapper.CpuWrite(0x8001, 4);
            mapper.CpuWrite(0x8002, 5);
            mapper.CpuWrite(0x8003, 7);
            mapper.CpuWrite(0xA000, 20);
            mapper.CpuWrite(0xA003, 23);
            mapper.CpuWrite(0xE000, 0x40);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x23));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x24));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x2E));
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x4A));
            Assert.That(mapper.PpuRead(0x0400), Is.EqualTo(0x4B));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x54));
            Assert.That(mapper.PpuRead(0x1C00), Is.EqualTo(0x57));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        [Test]
        public void InvertsReloadValueAndSupportsEnableAndAcknowledge()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xC000, 0xFD); // Inverted latch = 2.
            mapper.CpuWrite(0xC001, 0);
            mapper.CpuWrite(0xC002, 0);

            mapper.ClockScanline();
            Assert.That(mapper.IrqCounter, Is.EqualTo(2));
            mapper.ClockScanline();
            Assert.That(mapper.IrqPending, Is.False);
            mapper.ClockScanline();
            Assert.That(mapper.IrqPending, Is.True);

            mapper.CpuWrite(0xC003, 0);
            Assert.That(mapper.IrqPending, Is.False);
            Assert.That(mapper.IrqEnabled, Is.False);
        }

        private static Mapper048 CreateMapper()
        {
            var prg = new byte[16 * 8192];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 8192; i++) prg[bank * 8192 + i] = (byte)(0x20 + bank);
            var chr = new byte[64 * 1024];
            for (int bank = 0; bank < 64; bank++)
                for (int i = 0; i < 1024; i++) chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper048(prg, chr, MirroringMode.Vertical);
        }
    }
}
