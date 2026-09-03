using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper033Tests
    {
        [Test]
        public void SelectsPrgBanksAndMirroring()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 0x43);
            mapper.CpuWrite(0x8001, 4);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x23));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x24));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x2E));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x2F));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        [Test]
        public void SelectsTwoAndOneKilobyteChrBanks()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8002, 5);
            mapper.CpuWrite(0x8003, 7);
            mapper.CpuWrite(0xA000, 20);
            mapper.CpuWrite(0xA001, 21);
            mapper.CpuWrite(0xA002, 22);
            mapper.CpuWrite(0xA003, 23);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x4A));
            Assert.That(mapper.PpuRead(0x0400), Is.EqualTo(0x4B));
            Assert.That(mapper.PpuRead(0x0800), Is.EqualTo(0x4E));
            Assert.That(mapper.PpuRead(0x0C00), Is.EqualTo(0x4F));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x54));
            Assert.That(mapper.PpuRead(0x1C00), Is.EqualTo(0x57));
        }

        [Test]
        public void RegisterAddressMaskProvidesHardwareMirrors()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x9FFE, 6);
            mapper.CpuWrite(0xBFFC, 25);
            Assert.That(mapper.GetChrBank(0), Is.EqualTo(12));
            Assert.That(mapper.GetChrBank(4), Is.EqualTo(25));
        }

        private static Mapper033 CreateMapper()
        {
            var prg = new byte[16 * 8192];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 8192; i++) prg[bank * 8192 + i] = (byte)(0x20 + bank);
            var chr = new byte[64 * 1024];
            for (int bank = 0; bank < 64; bank++)
                for (int i = 0; i < 1024; i++) chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper033(prg, chr, MirroringMode.Vertical);
        }
    }
}
