using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper097Tests
    {
        [Test]
        public void KeepsLastBankLowAndSwitchesHighBank()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 0x43);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x2F));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x23));
            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(3));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        [TestCase(0x00, MirroringMode.SingleScreenLower)]
        [TestCase(0x40, MirroringMode.Horizontal)]
        [TestCase(0x80, MirroringMode.Vertical)]
        [TestCase(0xC0, MirroringMode.SingleScreenUpper)]
        public void SelectsAllFourMirroringModes(byte value, MirroringMode expected)
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xFFFF, value);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(expected));
        }

        [Test]
        public void ChrRamIsWritableAndPeekHasNoSideEffects()
        {
            var mapper = CreateMapper();
            mapper.PpuWrite(0x1234, 0xA5);
            Assert.That(mapper.PpuRead(0x1234), Is.EqualTo(0xA5));
            Assert.That(mapper.PpuPeek(0x1234), Is.EqualTo(0xA5));
        }

        private static Mapper097 CreateMapper()
        {
            var prg = new byte[16 * 16384];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 16384; i++) prg[bank * 16384 + i] = (byte)(0x20 + bank);
            return new Mapper097(prg, new byte[8192], MirroringMode.Vertical);
        }
    }
}
