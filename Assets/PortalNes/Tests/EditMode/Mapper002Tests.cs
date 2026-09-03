using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper002Tests
    {
        [Test]
        public void SwitchesLowerPrgBankAndKeepsLastBankFixed()
        {
            var prg = new byte[4 * 16384];
            for (int bank = 0; bank < 4; bank++) prg[bank * 16384] = (byte)(0x10 + bank);
            var mapper = new Mapper002(prg, new byte[8192], true);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x10));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x13));
            mapper.CpuWrite(0x8000, 2);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x13));
        }

        [Test]
        public void ChrRamCanBeWritten()
        {
            var mapper = new Mapper002(new byte[32768], new byte[8192], true);
            mapper.PpuWrite(0x1FFF, 0xA5);
            Assert.That(mapper.PpuRead(0x1FFF), Is.EqualTo(0xA5));
        }

        [Test]
        public void ChrRomIgnoresWrites()
        {
            var chr = new byte[8192]; chr[0] = 0x34;
            var mapper = new Mapper002(new byte[32768], chr, false);
            mapper.PpuWrite(0, 0x99);
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x34));
        }
    }
}
