using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper069Tests
    {
        [Test]
        public void CommandsSelectChrPrgAndMirroring()
        {
            var mapper = CreateMapper();
            WriteCommand(mapper, 3, 7);
            WriteCommand(mapper, 9, 5);
            WriteCommand(mapper, 10, 6);
            WriteCommand(mapper, 11, 7);
            WriteCommand(mapper, 12, 3);

            Assert.That(mapper.GetChrBank(3), Is.EqualTo(7));
            Assert.That(mapper.PpuRead(0x0C00), Is.EqualTo(0x47));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x25));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x26));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x27));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x3F));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
        }

        [Test]
        public void LowWindowCanSelectRomRamOrOpenBus()
        {
            var mapper = CreateMapper();
            WriteCommand(mapper, 8, 0x03);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0x23));

            WriteCommand(mapper, 8, 0xC0);
            mapper.CpuWrite(0x6123, 0x5A);
            Assert.That(mapper.CpuRead(0x6123), Is.EqualTo(0x5A));

            WriteCommand(mapper, 8, 0x40);
            mapper.CpuWrite(0x6123, 0x99);
            Assert.That(mapper.CpuRead(0x6123), Is.Zero);
        }

        [Test]
        public void IrqCounterWrapsAndControlWriteAcknowledges()
        {
            var mapper = CreateMapper();
            WriteCommand(mapper, 14, 2);
            WriteCommand(mapper, 15, 0);
            WriteCommand(mapper, 13, 0x81);

            mapper.ClockCpu(2);
            Assert.That(mapper.IrqCounter, Is.Zero);
            Assert.That(mapper.IrqPending, Is.False);
            mapper.ClockCpu(1);
            Assert.That(mapper.IrqCounter, Is.EqualTo(0xFFFF));
            Assert.That(mapper.IrqPending, Is.True);

            WriteCommand(mapper, 13, 0);
            Assert.That(mapper.IrqPending, Is.False);
            Assert.That(mapper.IrqCounterEnabled, Is.False);
        }

        [Test]
        public void Sunsoft5BToneProducesAudio()
        {
            var mapper = CreateMapper();
            // Channel A period 1, tone enabled, noise disabled, full fixed volume.
            mapper.CpuWrite(0xC000, 0);
            mapper.CpuWrite(0xE000, 1);
            mapper.CpuWrite(0xC000, 1);
            mapper.CpuWrite(0xE000, 0);
            mapper.CpuWrite(0xC000, 7);
            mapper.CpuWrite(0xE000, 0x3E);
            mapper.CpuWrite(0xC000, 8);
            mapper.CpuWrite(0xE000, 0x0F);

            bool heardNonZero = false;
            for (int i = 0; i < 8; i++)
            {
                mapper.ClockAudio(16);
                if (mapper.ExpansionAudioSample > 0) heardNonZero = true;
            }
            Assert.That(heardNonZero, Is.True);
        }

        private static void WriteCommand(Mapper069 mapper, byte command, byte value)
        {
            mapper.CpuWrite(0x8000, command);
            mapper.CpuWrite(0xA000, value);
        }

        private static Mapper069 CreateMapper()
        {
            var prg = new byte[32 * 8192];
            for (int bank = 0; bank < 32; bank++)
                for (int i = 0; i < 8192; i++) prg[bank * 8192 + i] = (byte)(0x20 + bank);
            var chr = new byte[32 * 1024];
            for (int bank = 0; bank < 32; bank++)
                for (int i = 0; i < 1024; i++) chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper069(prg, chr, MirroringMode.Horizontal);
        }
    }
}
