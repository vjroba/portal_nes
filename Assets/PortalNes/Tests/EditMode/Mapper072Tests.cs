using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper072Tests
    {
        [Test]
        public void RisingCommandEdgesLatchPrgAndChrBanks()
        {
            var mapper = CreateMapper();

            mapper.CpuWrite(0x8001, 0x83);
            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(3));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x23));

            mapper.CpuWrite(0x8001, 0x03);
            mapper.CpuWrite(0x8001, 0x4A);
            Assert.That(mapper.SelectedChrBank, Is.EqualTo(10));
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x4A));
        }

        [Test]
        public void CommandMustReturnLowBeforeSameLatchCanTriggerAgain()
        {
            var mapper = CreateMapper();

            mapper.CpuWrite(0x8001, 0x81);
            mapper.CpuWrite(0x8001, 0x82);
            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(1));
            mapper.CpuWrite(0x8001, 0x02);
            mapper.CpuWrite(0x8001, 0x82);
            Assert.That(mapper.SelectedPrgBank, Is.EqualTo(2));
        }

        [Test]
        public void LastPrgBankIsFixedAtUpperCpuWindow()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8001, 0x84);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x24));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x27));
        }

        [Test]
        public void BusConflictMasksWrittenCommand()
        {
            var prg = CreatePrg();
            var chr = CreateChr();
            prg[0] = 0x03;
            var mapper = new Mapper072(prg, chr);

            mapper.CpuWrite(0x8000, 0x83);

            Assert.That(mapper.SelectedPrgBank, Is.Zero);
        }

        private static Mapper072 CreateMapper() => new Mapper072(CreatePrg(), CreateChr());

        private static byte[] CreatePrg()
        {
            var prg = new byte[8 * 16384];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 16384; i++) prg[bank * 16384 + i] = (byte)(0x20 + bank);
            // Permit all command bits at the address used by tests.
            for (int bank = 0; bank < 8; bank++) prg[bank * 16384 + 1] = 0xFF;
            return prg;
        }

        private static byte[] CreateChr()
        {
            var chr = new byte[16 * 8192];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 8192; i++) chr[bank * 8192 + i] = (byte)(0x40 + bank);
            return chr;
        }
    }
}
