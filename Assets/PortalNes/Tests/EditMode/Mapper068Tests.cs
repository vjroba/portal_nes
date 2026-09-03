using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;
using PortalNes.Emulator.Ppu;

namespace PortalNes.Tests
{
    public sealed class Mapper068Tests
    {
        [Test]
        public void SwitchesFourChrWindowsAndLowerPrgWindow()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 3);
            mapper.CpuWrite(0x9000, 4);
            mapper.CpuWrite(0xA000, 5);
            mapper.CpuWrite(0xB000, 6);
            mapper.CpuWrite(0xF000, 2);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x0800), Is.EqualTo(0x44));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x45));
            Assert.That(mapper.PpuRead(0x1800), Is.EqualTo(0x46));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x22));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x27));
        }

        [Test]
        public void ChrRomNametablesUseSelectedBanksAndMirroring()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xC000, 1);
            mapper.CpuWrite(0xD000, 2);
            mapper.CpuWrite(0xE000, 0x10); // ROM nametables, vertical.

            Assert.That(mapper.ReadNametable(0x2000), Is.EqualTo(0x80));
            Assert.That(mapper.ReadNametable(0x2400), Is.EqualTo(0x81));
            Assert.That(mapper.ReadNametable(0x2800), Is.EqualTo(0x80));
            Assert.That(mapper.ReadNametable(0x2C00), Is.EqualTo(0x81));
        }

        [Test]
        public void PpuRoutesNametableReadsThroughMapperOwnedRom()
        {
            var mapper = CreateMapper();
            var ppu = new Ppu2C02(mapper, MirroringMode.Horizontal);
            mapper.CpuWrite(0xC000, 3);
            mapper.CpuWrite(0xE000, 0x12); // ROM nametables, single-screen bank 0.

            Assert.That(ppu.PpuRead(0x2000), Is.EqualTo(0x81));
            Assert.That(ppu.PpuRead(0x2C00), Is.EqualTo(0x81));
            ppu.PpuWrite(0x2000, 0xFF);
            Assert.That(ppu.PpuRead(0x2000), Is.EqualTo(0x81));
        }

        [Test]
        public void CiramModeSupportsAllFourMirroringLayouts()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xE000, 0x00);
            mapper.WriteNametable(0x2000, 0x11);
            mapper.WriteNametable(0x2400, 0x22);
            Assert.That(mapper.ReadNametable(0x2800), Is.EqualTo(0x11));
            Assert.That(mapper.ReadNametable(0x2C00), Is.EqualTo(0x22));

            mapper.CpuWrite(0xE000, 0x02);
            mapper.WriteNametable(0x2000, 0x33);
            Assert.That(mapper.ReadNametable(0x2C00), Is.EqualTo(0x33));
        }

        private static Mapper068 CreateMapper()
        {
            var prg = new byte[8 * 16384];
            var chr = new byte[256 * 1024];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 16384; i++) prg[bank * 16384 + i] = (byte)(0x20 + bank);
            for (int bank = 0; bank < chr.Length / 2048; bank++)
                for (int i = 0; i < 2048; i++) chr[bank * 2048 + i] = (byte)(0x40 + bank);
            return new Mapper068(prg, chr, MirroringMode.Horizontal);
        }
    }
}
