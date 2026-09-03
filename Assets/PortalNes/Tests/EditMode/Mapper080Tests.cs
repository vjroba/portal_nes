using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper080Tests
    {
        [Test]
        public void SelectsPrgChrBanksAndMirroring()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x7EFA, 3);
            mapper.CpuWrite(0x7EFC, 4);
            mapper.CpuWrite(0x7EFE, 5);
            mapper.CpuWrite(0x7EF0, 10);
            mapper.CpuWrite(0x7EF1, 14);
            mapper.CpuWrite(0x7EF2, 20);
            mapper.CpuWrite(0x7EF5, 23);
            mapper.CpuWrite(0x7EF6, 1);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x23));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x24));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x25));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x2F));
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x4A));
            Assert.That(mapper.PpuRead(0x0400), Is.EqualTo(0x4B));
            Assert.That(mapper.PpuRead(0x0800), Is.EqualTo(0x4E));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x54));
            Assert.That(mapper.PpuRead(0x1C00), Is.EqualTo(0x57));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Vertical));
        }

        [Test]
        public void ProtectsAndMirrorsInternalRam()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x7F00, 0x55);
            Assert.That(mapper.CpuRead(0x7F00), Is.Zero);

            mapper.CpuWrite(0x7EF8, 0xA3);
            mapper.CpuWrite(0x7F00, 0x55);
            Assert.That(mapper.CpuRead(0x7F80), Is.EqualTo(0x55));

            mapper.CpuWrite(0x7EF9, 0);
            Assert.That(mapper.CpuRead(0x7F00), Is.Zero);
        }

        [Test]
        public void A7LowRegisterMirrorWorks()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x7E7A, 6);
            mapper.CpuWrite(0x7E76, 0);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x26));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        private static Mapper080 CreateMapper()
        {
            var prg = new byte[16 * 8192];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 8192; i++) prg[bank * 8192 + i] = (byte)(0x20 + bank);
            var chr = new byte[64 * 1024];
            for (int bank = 0; bank < 64; bank++)
                for (int i = 0; i < 1024; i++) chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper080(prg, chr, MirroringMode.Vertical);
        }
    }
}
