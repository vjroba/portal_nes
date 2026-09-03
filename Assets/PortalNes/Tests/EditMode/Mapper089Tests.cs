using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper089Tests
    {
        [Test]
        public void RegisterSelectsPrgChrAndOneScreenMirroring()
        {
            var mapper = CreateMapper();

            mapper.CpuWrite(0x8001, 0xDB);

            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(5));
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(11));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x25));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x27));
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x4B));
        }

        [Test]
        public void BusConflictMasksRegisterValue()
        {
            var prg = CreatePrg();
            prg[0] = 0x11;
            var mapper = new Mapper089(prg, CreateChr());

            mapper.CpuWrite(0x8000, 0xFF);

            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(1));
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(1));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenLower));
        }

        private static Mapper089 CreateMapper() => new Mapper089(CreatePrg(), CreateChr());

        private static byte[] CreatePrg()
        {
            var data = new byte[8 * 16384];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 16384; i++) data[bank * 16384 + i] = (byte)(0x20 + bank);
            for (int bank = 0; bank < 8; bank++) data[bank * 16384 + 1] = 0xFF;
            return data;
        }

        private static byte[] CreateChr()
        {
            var data = new byte[16 * 8192];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 8192; i++) data[bank * 8192 + i] = (byte)(0x40 + bank);
            return data;
        }
    }
}
