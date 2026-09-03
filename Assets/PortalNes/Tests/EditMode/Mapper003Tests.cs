using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper003Tests
    {
        [Test]
        public void SwitchesChrBankAndKeepsPrgFixed()
        {
            var prg = new byte[32768]; prg[0] = 0x12; prg[1] = 0x03; prg[16384] = 0x34;
            var chr = new byte[4 * 8192];
            for (int bank = 0; bank < 4; bank++) chr[bank * 8192] = (byte)(0x40 + bank);
            var mapper = new Mapper003(prg, chr);
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x40));
            mapper.CpuWrite(0x8001, 3);
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x43));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x34));
        }

        [Test]
        public void ChrBankWriteUsesAndTypeBusConflict()
        {
            var prg = new byte[32768];
            prg[0x1234] = 0x02;
            var chr = new byte[4 * 8192];
            for (int bank = 0; bank < 4; bank++) chr[bank * 8192] = (byte)bank;
            var mapper = new Mapper003(prg, chr);

            mapper.CpuWrite(0x9234, 0x03);

            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x02));
        }

        [Test]
        public void MirrorsSixteenKilobytePrg()
        {
            var prg = new byte[16384]; prg[0] = 0x5A;
            var mapper = new Mapper003(prg, new byte[8192]);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x5A));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x5A));
        }
    }
}
