using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper078Tests
    {
        [Test]
        public void SelectsPrgAndChrBanksWithBusConflict()
        {
            var mapper = CreateMapper(false);
            mapper.CpuWrite(0x8000, 0xB5);

            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(5));
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(11));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x25));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x27));
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x4B));
        }

        [Test]
        public void HolyDiverBoardSwitchesHorizontalAndVertical()
        {
            var mapper = CreateMapper(true);
            mapper.CpuWrite(0x8000, 0x08);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Vertical));
            mapper.CpuWrite(0x8000, 0x00);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        [Test]
        public void CosmoCarrierBoardSwitchesOneScreenPage()
        {
            var mapper = CreateMapper(false);
            mapper.CpuWrite(0x8000, 0x08);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
            mapper.CpuWrite(0x8000, 0x00);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenLower));
        }

        [Test]
        public void RomOutputMasksRegisterValue()
        {
            var mapper = CreateMapper(false, 0x0F);
            mapper.CpuWrite(0x8000, 0xF7);
            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(7));
            Assert.That(mapper.SelectedChrBank, Is.Zero);
        }

        private static Mapper078 CreateMapper(bool holyDiver, byte commandByte = 0xFF)
        {
            var prg = new byte[8 * 16384];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 16384; i++) prg[bank * 16384 + i] = (byte)(0x20 + bank);
            prg[0] = commandByte;
            var chr = new byte[16 * 8192];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 8192; i++) chr[bank * 8192 + i] = (byte)(0x40 + bank);
            return new Mapper078(prg, chr, holyDiver);
        }
    }
}
