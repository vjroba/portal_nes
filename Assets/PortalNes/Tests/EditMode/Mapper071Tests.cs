using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper071Tests
    {
        [Test]
        public void SelectsLowPrgBankAndFixesLastBankHigh()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xC000, 5);
            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(5));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x45));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x47));
        }

        [Test]
        public void BankSelectUsesLowFourBitsAndWrapsAvailableBanks()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xFFFF, 0x1B);
            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(3));
        }

        [Test]
        public void LegacyMirroringDetectionIgnoresStartupWritesBelow9000()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 0x10);
            Assert.That(mapper.MirroringOverride, Is.Null);
            mapper.CpuWrite(0x9000, 0x10);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
            mapper.CpuWrite(0x9FFF, 0x00);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenLower));
        }

        [Test]
        public void UsesWritableChrRam()
        {
            var mapper = CreateMapper();
            mapper.PpuWrite(0x1234, 0xA5);
            Assert.That(mapper.PpuRead(0x1234), Is.EqualTo(0xA5));
        }

        private static Mapper071 CreateMapper()
        {
            var prg = new byte[8 * 16384];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 16384; i++)
                    prg[bank * 16384 + i] = (byte)(0x40 + bank);
            return new Mapper071(prg, new byte[8192]);
        }
    }
}
