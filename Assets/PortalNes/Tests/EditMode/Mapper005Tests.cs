using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper005Tests
    {
        private static byte[] Banks(int count, int size)
        {
            var data = new byte[count * size];
            for (int bank = 0; bank < count; bank++)
                for (int i = 0; i < size; i++) data[bank * size + i] = (byte)bank;
            return data;
        }

        [Test]
        public void PrgMode3_MapsFourIndependent8KSlots()
        {
            var mapper = new Mapper005(Banks(16, 8192), Banks(8, 1024), false);
            mapper.CpuWrite(0x5114, 0x81);
            mapper.CpuWrite(0x5115, 0x82);
            mapper.CpuWrite(0x5116, 0x83);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(1));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(2));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(3));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(15));
        }

        [Test]
        public void PrgMode0_AlwaysTreats5117AsRom()
        {
            var mapper = new Mapper005(Banks(16, 8192), Banks(8, 1024), false);
            mapper.CpuWrite(0x5100, 0);
            mapper.CpuWrite(0x5117, 0x07); // bit 7 is ignored for $5117
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(4));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(7));
        }

        [Test]
        public void PrgRam_RequiresBothProtectionKeys()
        {
            var mapper = new Mapper005(Banks(4, 8192), Banks(8, 1024), false);
            mapper.CpuWrite(0x6000, 0x44);
            Assert.That(mapper.CpuRead(0x6000), Is.Zero);
            mapper.CpuWrite(0x5102, 2);
            mapper.CpuWrite(0x5103, 1);
            mapper.CpuWrite(0x6000, 0x44);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0x44));
        }

        [Test]
        public void BackgroundAndSpriteUseSeparateChrRegisters()
        {
            var mapper = new Mapper005(Banks(4, 8192), Banks(16, 1024), false);
            mapper.CpuWrite(0x5120, 2);
            mapper.CpuWrite(0x5128, 5);
            Assert.That(mapper.ReadSpritePattern(0), Is.EqualTo(2));
            Assert.That(mapper.ReadBackgroundPattern(0, 0x2000), Is.EqualTo(5));
        }

        [Test]
        public void NametableSourcesIncludeExRamAndFillMode()
        {
            var mapper = new Mapper005(Banks(4, 8192), Banks(8, 1024), false);
            mapper.CpuWrite(0x5105, 0xE4); // CIRAM 0, CIRAM 1, ExRAM, fill
            mapper.WriteNametable(0x2000, 0x11);
            mapper.WriteNametable(0x2400, 0x22);
            mapper.CpuWrite(0x5C00, 0x33);
            mapper.CpuWrite(0x5106, 0x44);
            mapper.CpuWrite(0x5107, 2);
            Assert.That(mapper.ReadNametable(0x2000), Is.EqualTo(0x11));
            Assert.That(mapper.ReadNametable(0x2400), Is.EqualTo(0x22));
            Assert.That(mapper.ReadNametable(0x2800), Is.EqualTo(0x33));
            Assert.That(mapper.ReadNametable(0x2C00), Is.EqualTo(0x44));
            Assert.That(mapper.ReadNametable(0x2FC0), Is.EqualTo(0xAA));
        }

        [Test]
        public void ExtendedAttributesSelectPaletteAndChrBank()
        {
            var mapper = new Mapper005(Banks(4, 8192), Banks(32, 1024), false);
            mapper.CpuWrite(0x5104, 1);
            mapper.CpuWrite(0x5C00, 0xC2); // palette 3, 4KB CHR bank 2
            Assert.That(mapper.GetBackgroundPalette(0x2000, 0), Is.EqualTo(3));
            Assert.That(mapper.ReadBackgroundPattern(0, 0x2000), Is.EqualTo(8));
        }

        [Test]
        public void VerticalSplitUsesExRamAndItsOwnChrBank()
        {
            var mapper = new Mapper005(Banks(4, 8192), Banks(32, 1024), false);
            mapper.CpuWrite(0x5104, 0);
            mapper.CpuWrite(0x5C00 + 5, 7);
            mapper.CpuWrite(0x5C00 + 0x3C1, 0xAA);
            mapper.CpuWrite(0x5200, 0xC4); // enabled, right side, starts at column 4
            mapper.CpuWrite(0x5201, 0);
            mapper.CpuWrite(0x5202, 3);

            Assert.That(mapper.TryGetSplitTile(3, 0, out _, out _, out _), Is.False);
            Assert.That(mapper.TryGetSplitTile(5, 0, out byte tile, out int palette, out int fineY), Is.True);
            Assert.That(tile, Is.EqualTo(7));
            Assert.That(palette, Is.EqualTo(2));
            Assert.That(fineY, Is.Zero);
            Assert.That(mapper.ReadSplitPattern(0), Is.EqualTo(12));
        }

        [Test]
        public void ScanlineIrqAndMultiplierRegistersWork()
        {
            var mapper = new Mapper005(Banks(4, 8192), Banks(8, 1024), false);
            mapper.CpuWrite(0x5203, 1);
            mapper.CpuWrite(0x5204, 0x80);
            mapper.BeginPpuFrame();
            mapper.ClockScanline();
            mapper.ClockScanline();
            Assert.That(mapper.IrqPending, Is.True);
            Assert.That(mapper.CpuRead(0x5204) & 0xC0, Is.EqualTo(0xC0));
            Assert.That(mapper.IrqPending, Is.False);
            mapper.CpuWrite(0x5205, 13);
            mapper.CpuWrite(0x5206, 20);
            Assert.That(mapper.CpuRead(0x5205), Is.EqualTo(4));
            Assert.That(mapper.CpuRead(0x5206), Is.EqualTo(1));
        }

        [Test]
        public void ExpansionPulseUsesEnvelopeAndLengthStatus()
        {
            var mapper = new Mapper005(Banks(4, 8192), Banks(8, 1024), false);
            mapper.CpuWrite(0x5015, 1);
            mapper.CpuWrite(0x5000, 0xDF); // duty 3, constant volume 15
            mapper.CpuWrite(0x5002, 8);
            mapper.CpuWrite(0x5003, 0); // length table value 10
            Assert.That(mapper.CpuRead(0x5015) & 1, Is.EqualTo(1));
            Assert.That(mapper.ExpansionAudioSample, Is.EqualTo(15));
            for (int i = 0; i < 10; i++) mapper.ClockAudioHalfFrame();
            Assert.That(mapper.CpuRead(0x5015) & 1, Is.Zero);
        }

        [Test]
        public void PcmSupportsDirectOutputAndReadModeIrq()
        {
            var mapper = new Mapper005(Banks(4, 8192), Banks(8, 1024), false);
            mapper.CpuWrite(0x5011, 128);
            Assert.That(mapper.ExpansionAudioSample, Is.EqualTo(64));
            mapper.CpuWrite(0x5010, 0x81);
            mapper.CpuRead(0x8000); // default RAM window reads zero
            Assert.That(mapper.IrqPending, Is.True);
            Assert.That(mapper.CpuRead(0x5010), Is.EqualTo(0x80));
            Assert.That(mapper.IrqPending, Is.False);
        }
    }
}
