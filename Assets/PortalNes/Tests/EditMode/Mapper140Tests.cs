using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper140Tests
    {
        [Test]
        public void SwitchesPrgAndChrTogetherFromLowCpuRegisterRange()
        {
            var prg = new byte[4 * 32768];
            var chr = new byte[16 * 8192];
            for (int bank = 0; bank < 4; bank++) prg[bank * 32768] = (byte)(0x20 + bank);
            for (int bank = 0; bank < 16; bank++) chr[bank * 8192] = (byte)(0x40 + bank);
            var mapper = new Mapper140(prg, chr);

            mapper.CpuWrite(0x6000, 0x3A);

            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(3));
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(10));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x23));
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x4A));
        }

        [Test]
        public void IgnoresWritesInPrgRomRange()
        {
            var mapper = new Mapper140(new byte[2 * 32768], new byte[2 * 8192]);
            mapper.CpuWrite(0x6000, 0x11);
            mapper.CpuWrite(0x8000, 0x00);

            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(1));
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(1));
        }

        [Test]
        public void RegisterReadRangeDoesNotExposePrgRom()
        {
            var mapper = new Mapper140(new byte[32768], new byte[8192]);
            Assert.That(mapper.CpuRead(0x6000), Is.Zero);
            Assert.That(mapper.CpuRead(0x7FFF), Is.Zero);
        }
    }
}
