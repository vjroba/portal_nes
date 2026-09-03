using NUnit.Framework;
using PortalNes.Emulator.Mappers;
using PortalNes.Emulator.Ppu;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Tests
{
    public sealed class Mapper095Tests
    {
        [Test]
        public void MapsNamcot108PrgAndChrBanks()
        {
            var mapper = CreateMapper();
            WriteBank(mapper, 6, 2);
            WriteBank(mapper, 7, 3);
            WriteBank(mapper, 0, 6);
            WriteBank(mapper, 2, 10);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x13));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x16));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x17));
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x46));
            Assert.That(mapper.PpuRead(0x0400), Is.EqualTo(0x47));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x4A));
        }

        [Test]
        public void ChrBankBitFiveSelectsNametableForEachHorizontalPair()
        {
            var mapper = CreateMapper();
            WriteBank(mapper, 0, 0x20);
            WriteBank(mapper, 1, 0x02);
            var ppu = new Ppu2C02(mapper, MirroringMode.Vertical);

            ppu.PpuWrite(0x2000, 0xA1);
            ppu.PpuWrite(0x2800, 0xB2);

            Assert.That(ppu.PpuRead(0x2400), Is.EqualTo(0xA1));
            Assert.That(ppu.PpuRead(0x2C00), Is.EqualTo(0xB2));
            Assert.That(ppu.PpuRead(0x2000), Is.EqualTo(0xA1));
            Assert.That(ppu.PpuRead(0x2800), Is.EqualTo(0xB2));
        }

        [Test]
        public void NametableSelectionDoesNotDiscardChrBankBitFive()
        {
            var mapper = CreateMapper();
            WriteBank(mapper, 0, 0x22);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x62));
            Assert.That(mapper.MapNametableAddress(0x2000), Is.EqualTo(0x400));
        }

        private static Mapper095 CreateMapper()
        {
            var prg = new byte[8 * 8192];
            var chr = new byte[64 * 1024];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 64; bank++)
                for (int i = 0; i < 1024; i++)
                    chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper095(prg, chr);
        }

        private static void WriteBank(Mapper095 mapper, int register, byte value)
        {
            mapper.CpuWrite(0x8000, (byte)register);
            mapper.CpuWrite(0x8001, value);
        }
    }
}
