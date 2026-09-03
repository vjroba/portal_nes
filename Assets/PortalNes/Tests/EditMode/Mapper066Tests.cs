using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper066Tests
    {
        [Test]
        public void SwitchesPrgAndChrBanksFromOneRegister()
        {
            var prg = new byte[4 * 32768];
            var chr = new byte[4 * 8192];
            for (int bank = 0; bank < 4; bank++)
            {
                prg[bank * 32768] = (byte)(0x30 + bank);
                chr[bank * 8192] = (byte)(0x50 + bank);
            }
            var mapper = new Mapper066(prg, chr, false);

            mapper.CpuWrite(0x8000, 0x32);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x33));
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x52));
        }

        [Test]
        public void ChrRomIgnoresWrites()
        {
            var chr = new byte[8192];
            chr[0] = 0x44;
            var mapper = new Mapper066(new byte[32768], chr, false);
            mapper.PpuWrite(0, 0x99);
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x44));
        }
    }
}
