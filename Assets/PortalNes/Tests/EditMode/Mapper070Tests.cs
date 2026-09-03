using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper070Tests
    {
        [Test]
        public void SelectsPrgAndChrBanksAndFixesLastPrgBank()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 0x35);
            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(3));
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(5));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x23));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x27));
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x45));
        }

        [Test]
        public void BusConflictMasksWrittenValue()
        {
            var prg = CreatePrg(); prg[0] = 0x12;
            var mapper = new Mapper070(prg, CreateChr(), MirroringMode.Vertical);
            mapper.CpuWrite(0x8000, 0x35);
            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(1));
            Assert.That(mapper.SelectedChrBank, Is.Zero);
        }

        [Test]
        public void NoConflictVariantUsesUnmaskedValue()
        {
            var prg = CreatePrg(); prg[0] = 0;
            var mapper = new Mapper070(prg, CreateChr(), MirroringMode.Vertical, false);
            mapper.CpuWrite(0x8000, 0x35);
            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(3));
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(5));
        }

        [Test]
        public void BitSevenEnablesLegacyOneScreenMirroringControl()
        {
            var mapper = CreateMapper(MirroringMode.Vertical);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Vertical));
            mapper.CpuWrite(0x8000, 0x80);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
            mapper.CpuWrite(0x8000, 0);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenLower));
        }

        private static Mapper070 CreateMapper(MirroringMode mirroring = MirroringMode.Horizontal) =>
            new Mapper070(CreatePrg(), CreateChr(), mirroring);

        private static byte[] CreatePrg()
        {
            var data = new byte[8 * 16384];
            for (int bank = 0; bank < 8; bank++) for (int i = 0; i < 16384; i++) data[bank * 16384 + i] = (byte)(0x20 + bank);
            data[0] = 0xFF;
            return data;
        }

        private static byte[] CreateChr()
        {
            var data = new byte[16 * 8192];
            for (int bank = 0; bank < 16; bank++) for (int i = 0; i < 8192; i++) data[bank * 8192 + i] = (byte)(0x40 + bank);
            return data;
        }
    }
}
