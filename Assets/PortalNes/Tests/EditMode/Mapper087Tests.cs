using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper087Tests
    {
        [Test]
        public void ReversesChrBankSelectBits()
        {
            var mapper = CreateMapper(32768);
            mapper.CpuWrite(0x6000, 1);
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(2));
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x42));
            mapper.CpuWrite(0x7FFF, 2);
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(1));
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x41));
        }

        [Test]
        public void MirrorsSixteenKilobytePrgRom()
        {
            var mapper = CreateMapper(16384);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x20));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x20));
        }

        [Test]
        public void MapsThirtyTwoKilobytePrgRomDirectly()
        {
            var mapper = CreateMapper(32768);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x20));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x21));
        }

        private static Mapper087 CreateMapper(int prgSize)
        {
            var prg = new byte[prgSize];
            var chr = new byte[32768];
            for (int bank = 0; bank < prgSize / 16384; bank++)
                for (int i = 0; i < 16384; i++) prg[bank * 16384 + i] = (byte)(0x20 + bank);
            for (int bank = 0; bank < 4; bank++)
                for (int i = 0; i < 8192; i++) chr[bank * 8192 + i] = (byte)(0x40 + bank);
            return new Mapper087(prg, chr);
        }
    }
}
