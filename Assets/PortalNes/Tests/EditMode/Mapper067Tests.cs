using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper067Tests
    {
        [Test]
        public void SelectsPrgChrAndMirroring()
        {
            var mapper = CreateMapper();

            mapper.CpuWrite(0x8800, 3);
            mapper.CpuWrite(0x9800, 4);
            mapper.CpuWrite(0xA800, 5);
            mapper.CpuWrite(0xB800, 6);
            mapper.CpuWrite(0xE800, 3);
            mapper.CpuWrite(0xF800, 5);

            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(5));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x25));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x27));
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x0800), Is.EqualTo(0x44));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x45));
            Assert.That(mapper.PpuRead(0x1800), Is.EqualTo(0x46));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
        }

        [Test]
        public void IrqLoadsHighThenLowAndFiresOnCounterWrap()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xC800, 0x00);
            mapper.CpuWrite(0xC800, 0x02);
            mapper.CpuWrite(0xD800, 0x10);

            mapper.ClockCpu(2);
            Assert.That(mapper.IrqCounter, Is.Zero);
            Assert.That(mapper.IrqPending, Is.False);

            mapper.ClockCpu(1);
            Assert.That(mapper.IrqPending, Is.True);
            Assert.That(mapper.IrqEnabled, Is.False);
            Assert.That(mapper.IrqCounter, Is.EqualTo(0xFFFF));

            mapper.CpuWrite(0x8000, 0);
            Assert.That(mapper.IrqPending, Is.False);
        }

        [Test]
        public void EnableWriteResetsIrqLoadToggle()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xC800, 0x12);
            mapper.CpuWrite(0xD800, 0);
            mapper.CpuWrite(0xC800, 0x34);

            Assert.That(mapper.IrqCounter, Is.EqualTo(0x3400));
        }

        private static Mapper067 CreateMapper()
        {
            var prg = new byte[8 * 16384];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 16384; i++) prg[bank * 16384 + i] = (byte)(0x20 + bank);
            var chr = new byte[16 * 2048];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 2048; i++) chr[bank * 2048 + i] = (byte)(0x40 + bank);
            return new Mapper067(prg, chr, MirroringMode.Horizontal);
        }
    }
}
