using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper184Tests
    {
        [Test]
        public void ExpansionRegisterSelectsBothFourKilobyteChrBanks()
        {
            var mapper = CreateMapper();

            mapper.CpuWrite(0x6000, 0x53);

            Assert.That(mapper.LowerChrBank, Is.EqualTo(3));
            Assert.That(mapper.UpperChrBank, Is.EqualTo(5));
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x45));
        }

        [Test]
        public void PrgRomIsFixedAndSixteenKilobyteImagesMirror()
        {
            var prg = new byte[16384];
            prg[0] = 0x12;
            prg[0x3FFF] = 0x34;
            var mapper = new Mapper184(prg, CreateChr());

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xFFFF), Is.EqualTo(0x34));
        }

        private static Mapper184 CreateMapper() => new Mapper184(new byte[32768], CreateChr());

        private static byte[] CreateChr()
        {
            var data = new byte[8 * 4096];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 4096; i++) data[bank * 4096 + i] = (byte)(0x40 + bank);
            return data;
        }
    }
}
