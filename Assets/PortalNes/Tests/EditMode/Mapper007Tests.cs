using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;
using PortalNes.Emulator.Ppu;

namespace PortalNes.Tests
{
    public sealed class Mapper007Tests
    {
        [Test]
        public void SwitchesPrgBankAndOneScreenNametable()
        {
            var prg = new byte[4 * 32768];
            for (int bank = 0; bank < 4; bank++) prg[bank * 32768] = (byte)(0x20 + bank);
            var mapper = new Mapper007(prg, new byte[8192], true);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x20));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenLower));
            mapper.CpuWrite(0x8000, 0x12);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x22));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
        }

        [Test]
        public void PpuUsesMapperMirroringChanges()
        {
            var mapper = new Mapper007(new byte[32768], new byte[8192], true);
            var ppu = new Ppu2C02(mapper, MirroringMode.Horizontal);
            ppu.PpuWrite(0x2000, 0x11);
            ppu.PpuWrite(0x2400, 0x22);
            Assert.That(ppu.PpuRead(0x2000), Is.EqualTo(0x22));

            mapper.CpuWrite(0x8000, 0x10);
            ppu.PpuWrite(0x2000, 0x33);
            Assert.That(ppu.PpuRead(0x2C00), Is.EqualTo(0x33));

            mapper.CpuWrite(0x8000, 0x00);
            Assert.That(ppu.PpuRead(0x2000), Is.EqualTo(0x22));
        }
    }
}
