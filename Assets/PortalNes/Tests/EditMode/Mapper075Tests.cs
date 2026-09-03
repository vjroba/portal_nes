using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper075Tests
    {
        [Test]
        public void MapsThreeSelectedAndOneFixedPrgBank()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 2);
            mapper.CpuWrite(0xA000, 3);
            mapper.CpuWrite(0xC000, 4);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x13));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x14));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x1F));
        }

        [Test]
        public void CombinesLowAndHighChrBankBits()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xE000, 2);
            mapper.CpuWrite(0xF000, 3);
            mapper.CpuWrite(0x9000, 0x06);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x52));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x53));
        }

        [Test]
        public void SwitchesHorizontalAndVerticalMirroring()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x9000, 0);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Vertical));
            mapper.CpuWrite(0x9000, 1);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        private static Mapper075 CreateMapper()
        {
            var prg = new byte[16 * 8192];
            var chr = new byte[32 * 4096];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 32; bank++)
                for (int i = 0; i < 4096; i++)
                    chr[bank * 4096 + i] = (byte)(0x40 + bank);
            return new Mapper075(prg, chr, MirroringMode.Horizontal);
        }
    }
}
