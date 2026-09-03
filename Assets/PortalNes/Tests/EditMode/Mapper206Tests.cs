using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper206Tests
    {
        [Test]
        public void MapsTwoSelectedAndTwoFixedPrgBanks()
        {
            var mapper = CreateMapper();
            WriteBank(mapper, 6, 2);
            WriteBank(mapper, 7, 3);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x13));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x16));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x17));
        }

        [Test]
        public void MapsTwoTwoKilobyteAndFourOneKilobyteChrBanks()
        {
            var mapper = CreateMapper();
            WriteBank(mapper, 0, 3);
            WriteBank(mapper, 1, 7);
            WriteBank(mapper, 2, 10);
            WriteBank(mapper, 5, 13);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x42));
            Assert.That(mapper.PpuRead(0x0400), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x0800), Is.EqualTo(0x46));
            Assert.That(mapper.PpuRead(0x0C00), Is.EqualTo(0x47));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x4A));
            Assert.That(mapper.PpuRead(0x1C00), Is.EqualTo(0x4D));
        }

        [Test]
        public void IgnoresMmc3ModeIrqAndMirroringRegisters()
        {
            var mapper = CreateMapper();
            WriteBank(mapper, 6, 2);
            mapper.CpuWrite(0xA000, 1);
            mapper.CpuWrite(0xC000, 1);
            mapper.CpuWrite(0xE001, 1);
            for (int i = 0; i < 10; i++) mapper.ClockScanline();

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.MirroringOverride, Is.Null);
            Assert.That(mapper.IrqPending, Is.False);
        }

        private static Mapper206 CreateMapper()
        {
            var prg = new byte[8 * 8192];
            var chr = new byte[16 * 1024];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 1024; i++)
                    chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper206(prg, chr);
        }

        private static void WriteBank(Mapper206 mapper, int register, byte value)
        {
            mapper.CpuWrite(0x8000, (byte)register);
            mapper.CpuWrite(0x8001, value);
        }
    }
}
