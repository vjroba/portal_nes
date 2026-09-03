using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper022Tests
    {
        [Test]
        public void MapsTwoSelectedAndTwoFixedPrgBanks()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 2);
            mapper.CpuWrite(0xA000, 3);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x13));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x16));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x17));
        }

        [Test]
        public void UsesVrc2aAddressWiringAndShiftedChrBanks()
        {
            var mapper = CreateMapper();
            // Slot 0: low nibble at B000, high nibble at B002.
            mapper.CpuWrite(0xB000, 4);
            mapper.CpuWrite(0xB002, 0);
            // Slot 1: low nibble at B001, high nibble at B003.
            mapper.CpuWrite(0xB001, 6);
            mapper.CpuWrite(0xB003, 0);
            // Raw values 4 and 6 select physical banks 2 and 3 on VRC2a.
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x42));
            Assert.That(mapper.PpuRead(0x0400), Is.EqualTo(0x43));
        }

        [Test]
        public void SwitchesOnlyHorizontalAndVerticalMirroring()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x9000, 0);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Vertical));
            mapper.CpuWrite(0x9000, 0xFF);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        [Test]
        public void StoresTheVrc2OneBitLatch()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x6000, 0xFE);
            Assert.That(mapper.CpuRead(0x6000), Is.Zero);
            mapper.CpuWrite(0x6FFF, 0xFF);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(1));
        }

        private static Mapper022 CreateMapper()
        {
            var prg = new byte[8 * 8192];
            var chr = new byte[16 * 1024];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 1024; i++)
                    chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper022(prg, chr, MirroringMode.Horizontal);
        }
    }
}
