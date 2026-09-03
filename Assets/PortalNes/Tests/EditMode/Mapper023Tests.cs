using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper023Tests
    {
        [Test]
        public void MapsPrgBanksAndSupportsSwapMode()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 2);
            mapper.CpuWrite(0xA000, 3);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x13));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x16));

            mapper.CpuWrite(0x9002, 2);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x16));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x12));
        }

        [Test]
        public void AcceptsVrc4fAndVrc4eChrRegisterWiring()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xB000, 3);
            mapper.CpuWrite(0xB001, 0);
            mapper.CpuWrite(0xB008, 4);
            mapper.CpuWrite(0xB00C, 0);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x0400), Is.EqualTo(0x44));
        }

        [Test]
        public void SupportsAllVrc4MirroringModes()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x9000, 0);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Vertical));
            mapper.CpuWrite(0x9000, 1);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
            mapper.CpuWrite(0x9000, 2);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenLower));
            mapper.CpuWrite(0x9000, 3);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
        }

        [Test]
        public void ProvidesVrc2LatchWhileVrc4RamIsDisabled()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x6000, 1);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(1));
            mapper.CpuWrite(0x6FFF, 0);
            Assert.That(mapper.CpuRead(0x6000), Is.Zero);

            mapper.CpuWrite(0x9002, 1);
            mapper.CpuWrite(0x6000, 0xA5);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0xA5));
        }

        [Test]
        public void RaisesAndAcknowledgesCycleModeIrq()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xF000, 0x0E);
            mapper.CpuWrite(0xF001, 0x0F);
            mapper.CpuWrite(0xF002, 0x07);
            mapper.ClockCpu(2);
            Assert.That(mapper.IrqPending, Is.True);

            mapper.CpuWrite(0xF003, 0);
            Assert.That(mapper.IrqPending, Is.False);
            mapper.ClockCpu(2);
            Assert.That(mapper.IrqPending, Is.True);
        }

        [Test]
        public void ClocksScanlineModeEveryThreeHundredFortyOnePpuCycles()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xF000, 0x0F);
            mapper.CpuWrite(0xF001, 0x0F);
            mapper.CpuWrite(0xF002, 0x02);
            mapper.ClockCpu(113);
            Assert.That(mapper.IrqPending, Is.False);
            mapper.ClockCpu(1);
            Assert.That(mapper.IrqPending, Is.True);
        }

        private static Mapper023 CreateMapper()
        {
            var prg = new byte[8 * 8192];
            var chr = new byte[32 * 1024];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 32; bank++)
                for (int i = 0; i < 1024; i++)
                    chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper023(prg, chr, MirroringMode.Horizontal);
        }
    }
}
