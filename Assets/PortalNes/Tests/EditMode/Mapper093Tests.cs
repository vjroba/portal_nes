using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper093Tests
    {
        [Test]
        public void SelectsLowerPrgBankAndKeepsLastBankFixed()
        {
            var mapper = CreateMapper();

            mapper.CpuWrite(0x8001, 0x31);

            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(3));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x23));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x27));
        }

        [Test]
        public void ChrRamAccessRequiresEnableBit()
        {
            var mapper = CreateMapper();

            mapper.PpuWrite(0x123, 0x55);
            Assert.That(mapper.PpuRead(0x123), Is.Zero);
            mapper.CpuWrite(0x8001, 0x01);
            mapper.PpuWrite(0x123, 0x55);

            Assert.That(mapper.ChrRamEnabled, Is.True);
            Assert.That(mapper.PpuRead(0x123), Is.EqualTo(0x55));
        }

        [Test]
        public void BusConflictMasksBankSelection()
        {
            var prg = CreatePrg();
            prg[0] = 0x11;
            var mapper = new Mapper093(prg, new byte[8192]);

            mapper.CpuWrite(0x8000, 0x71);

            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(1));
        }

        private static Mapper093 CreateMapper() => new Mapper093(CreatePrg(), new byte[8192]);

        private static byte[] CreatePrg()
        {
            var prg = new byte[8 * 16384];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 16384; i++) prg[bank * 16384 + i] = (byte)(0x20 + bank);
            for (int bank = 0; bank < 8; bank++) prg[bank * 16384 + 1] = 0xFF;
            return prg;
        }
    }
}
