using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper076Tests
    {
        [Test]
        public void MapsSelectedAndFixedPrgBanks()
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
        public void RegistersTwoThroughFiveMapFourTwoKilobyteChrBanks()
        {
            var mapper = CreateMapper();
            WriteBank(mapper, 2, 3);
            WriteBank(mapper, 3, 7);
            WriteBank(mapper, 4, 11);
            WriteBank(mapper, 5, 15);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x07FF), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x0800), Is.EqualTo(0x47));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x4B));
            Assert.That(mapper.PpuRead(0x1800), Is.EqualTo(0x4F));
        }

        [Test]
        public void RegistersZeroAndOneDoNotChangeChrBanks()
        {
            var mapper = CreateMapper();
            WriteBank(mapper, 2, 3);
            WriteBank(mapper, 0, 12);
            WriteBank(mapper, 1, 14);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x0800), Is.EqualTo(0x40));
        }

        private static Mapper076 CreateMapper()
        {
            var prg = new byte[8 * 8192];
            var chr = new byte[64 * 2048];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 64; bank++)
                for (int i = 0; i < 2048; i++)
                    chr[bank * 2048 + i] = (byte)(0x40 + bank);
            return new Mapper076(prg, chr);
        }

        private static void WriteBank(Mapper076 mapper, int register, byte value)
        {
            mapper.CpuWrite(0x8000, (byte)register);
            mapper.CpuWrite(0x8001, value);
        }
    }
}
