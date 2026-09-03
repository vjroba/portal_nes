using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper004Tests
    {
        [Test]
        public void MapsPrgBanksInBothModes()
        {
            var mapper = CreateMapper();
            WriteBank(mapper, 6, 2);
            WriteBank(mapper, 7, 3);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x13));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x16));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x17));

            mapper.CpuWrite(0x8000, 0x46);
            mapper.CpuWrite(0x8001, 2);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x16));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x12));
        }

        [Test]
        public void MapsChrBanksWithInversion()
        {
            var mapper = CreateMapper();
            for (int register = 0; register < 6; register++)
                WriteBank(mapper, register, (byte)(register * 2));
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x40));
            Assert.That(mapper.PpuRead(0x0400), Is.EqualTo(0x41));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x44));

            mapper.CpuWrite(0x8000, 0x82);
            mapper.CpuWrite(0x8001, 4);
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x44));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x40));
        }

        [Test]
        public void ControlsMirroringAndPrgRamProtection()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xA000, 1);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
            mapper.CpuWrite(0x6000, 0x55);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0x55));
            mapper.CpuWrite(0xA001, 0xC0);
            mapper.CpuWrite(0x6000, 0xAA);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0x55));
            mapper.CpuWrite(0xA001, 0);
            Assert.That(mapper.CpuRead(0x6000), Is.Zero);
        }

        [Test]
        public void RaisesAndAcknowledgesScanlineIrq()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xC000, 2);
            mapper.CpuWrite(0xC001, 0);
            mapper.CpuWrite(0xE001, 0);
            mapper.ClockScanline();
            Assert.That(mapper.IrqPending, Is.False);
            mapper.ClockScanline();
            Assert.That(mapper.IrqPending, Is.False);
            mapper.ClockScanline();
            Assert.That(mapper.IrqPending, Is.True);
            mapper.CpuWrite(0xE000, 0);
            Assert.That(mapper.IrqPending, Is.False);
        }

        private static Mapper004 CreateMapper()
        {
            var prg = new byte[8 * 8192];
            var chr = new byte[16 * 1024];
            for (int i = 0; i < 8; i++) prg[i * 8192] = (byte)(0x10 + i);
            for (int i = 0; i < 16; i++) chr[i * 1024] = (byte)(0x40 + i);
            return new Mapper004(prg, chr, false);
        }

        private static void WriteBank(Mapper004 mapper, int register, byte value)
        {
            mapper.CpuWrite(0x8000, (byte)register);
            mapper.CpuWrite(0x8001, value);
        }
    }
}
