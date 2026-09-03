using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper032Tests
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
        public void ProvidesEightKilobytesOfWorkRam()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x6000, 0x5A);
            mapper.CpuWrite(0x7FFF, 0xA5);

            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0x5A));
            Assert.That(mapper.CpuRead(0x7FFF), Is.EqualTo(0xA5));
        }

        [Test]
        public void ControlRegisterSwapsFirstAndThirdPrgSlots()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 2);
            mapper.CpuWrite(0x9000, 2);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x16));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x12));
        }

        [Test]
        public void ControlRegisterSelectsMirroring()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x9000, 0);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Vertical));
            mapper.CpuWrite(0x9000, 1);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        [Test]
        public void MapsEightIndependentOneKilobyteChrBanks()
        {
            var mapper = CreateMapper();
            for (int slot = 0; slot < 8; slot++)
                mapper.CpuWrite((ushort)(0xB000 + slot), (byte)(slot + 8));

            for (int slot = 0; slot < 8; slot++)
                Assert.That(mapper.PpuRead((ushort)(slot * 1024)), Is.EqualTo(0x48 + slot));
        }

        [Test]
        public void MajorLeagueBoardIgnoresControlAndUsesUpperSingleScreen()
        {
            var mapper = CreateMapper(true);
            mapper.CpuWrite(0x8000, 2);
            mapper.CpuWrite(0x9000, 3);

            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x16));
        }

        private static Mapper032 CreateMapper(bool majorLeagueBoard = false)
        {
            var prg = new byte[8 * 8192];
            var chr = new byte[32 * 1024];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 32; bank++)
                for (int i = 0; i < 1024; i++)
                    chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper032(prg, chr, MirroringMode.Horizontal, majorLeagueBoard);
        }
    }
}
