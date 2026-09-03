using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper154Tests
    {
        [Test]
        public void UsesMapper88PrgAndChrBanking()
        {
            var mapper = CreateMapper();
            WriteBank(mapper, 6, 2);
            WriteBank(mapper, 2, 3);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x43));
        }

        [Test]
        public void BitSixSelectsOneScreenNametableOnEveryWrite()
        {
            var mapper = CreateMapper();

            mapper.CpuWrite(0x8000, 0x40);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));

            mapper.CpuWrite(0xA000, 0x00);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenLower));

            mapper.CpuWrite(0xE001, 0x40);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
        }

        private static Mapper154 CreateMapper()
        {
            var prg = new byte[8 * 8192];
            var chr = new byte[128 * 1024];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 128; bank++)
                for (int i = 0; i < 1024; i++)
                    chr[bank * 1024 + i] = (byte)bank;
            return new Mapper154(prg, chr);
        }

        private static void WriteBank(Mapper154 mapper, int register, byte value)
        {
            mapper.CpuWrite(0x8000, (byte)register);
            mapper.CpuWrite(0x8001, value);
        }
    }
}
