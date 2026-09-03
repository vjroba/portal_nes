using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper086Tests
    {
        [Test]
        public void SelectsPrgAndSplitBitChrBank()
        {
            var mapper = CreateMapper();

            mapper.CpuWrite(0x6000, 0x72);

            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(3));
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(6));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x23));
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x46));
        }

        [Test]
        public void RetainsExternalAudioCommandForDiagnostics()
        {
            var mapper = CreateMapper();

            mapper.CpuWrite(0x7000, 0x2A);

            Assert.That(mapper.AudioTrack, Is.EqualTo(10));
            Assert.That(mapper.AudioResetReleased, Is.True);
            Assert.That(mapper.AudioStartReleased, Is.False);
        }

        [Test]
        public void DecodesAccidentalRegisterMirrorsInUpperRomRange()
        {
            var mapper = CreateMapper();

            mapper.CpuWrite(0xE000, 0x51);
            mapper.CpuWrite(0xF000, 0x3C);

            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(1));
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(5));
            Assert.That(mapper.AudioTrack, Is.EqualTo(12));
        }

        private static Mapper086 CreateMapper()
        {
            var prg = new byte[4 * 32768];
            var chr = new byte[8 * 8192];
            for (int bank = 0; bank < 4; bank++)
                for (int i = 0; i < 32768; i++) prg[bank * 32768 + i] = (byte)(0x20 + bank);
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 8192; i++) chr[bank * 8192 + i] = (byte)(0x40 + bank);
            return new Mapper086(prg, chr);
        }
    }
}
