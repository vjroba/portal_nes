using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper210Tests
    {
        [Test]
        public void Namco340MapsEightChrAndThreePrgBanks()
        {
            var mapper = CreateMapper(true);
            mapper.CpuWrite(0x8000, 3);
            mapper.CpuWrite(0xB800, 11);
            mapper.CpuWrite(0xE000, 2);
            mapper.CpuWrite(0xE800, 3);
            mapper.CpuWrite(0xF000, 4);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x1C00), Is.EqualTo(0x4B));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x13));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x14));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x1F));
        }

        [Test]
        public void Namco340PrgSelectOneAlsoControlsMirroring()
        {
            var mapper = CreateMapper(true);

            mapper.CpuWrite(0xE000, 0x00);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenLower));
            mapper.CpuWrite(0xE000, 0x40);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Vertical));
            mapper.CpuWrite(0xE000, 0x80);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
            mapper.CpuWrite(0xE000, 0xC0);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        [Test]
        public void Namco175ProvidesEnableControlledPrgRamAndFixedMirroring()
        {
            var mapper = CreateMapper(false);
            mapper.CpuWrite(0x6000, 0x55);
            Assert.That(mapper.CpuRead(0x6000), Is.Zero);

            mapper.CpuWrite(0xC000, 1);
            mapper.CpuWrite(0x6000, 0x55);

            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0x55));
            Assert.That(mapper.MirroringOverride, Is.Null);
        }

        private static Mapper210 CreateMapper(bool namco340)
        {
            var prg = new byte[16 * 8192];
            var chr = new byte[32 * 1024];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 32; bank++)
                for (int i = 0; i < 1024; i++)
                    chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper210(prg, chr, MirroringMode.Vertical, namco340);
        }
    }
}
