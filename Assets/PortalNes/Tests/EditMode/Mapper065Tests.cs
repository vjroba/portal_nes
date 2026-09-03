using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper065Tests
    {
        [Test]
        public void SelectsThreePrgBanksEightChrBanksAndMirroring()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 3);
            mapper.CpuWrite(0xA000, 4);
            mapper.CpuWrite(0xC000, 5);
            for (int i = 0; i < 8; i++) mapper.CpuWrite((ushort)(0xB000 + i), (byte)(8 + i));
            mapper.CpuWrite(0x9001, 0x80);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x23));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x24));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x25));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x2F));
            for (int i = 0; i < 8; i++)
                Assert.That(mapper.PpuRead((ushort)(i * 0x400)), Is.EqualTo(0x48 + i));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        [Test]
        public void IrqReloadsCountsDownAndDisablesAfterTrigger()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x9005, 0x00);
            mapper.CpuWrite(0x9006, 0x03);
            mapper.CpuWrite(0x9004, 0);
            mapper.CpuWrite(0x9003, 0x80);

            mapper.ClockCpu(2);
            Assert.That(mapper.IrqCounter, Is.EqualTo(1));
            Assert.That(mapper.IrqPending, Is.False);
            mapper.ClockCpu(1);
            Assert.That(mapper.IrqPending, Is.True);
            Assert.That(mapper.IrqEnabled, Is.False);

            mapper.CpuWrite(0x9003, 0);
            Assert.That(mapper.IrqPending, Is.False);
        }

        [Test]
        public void CounterAtZeroWrapsWithoutImmediatelyTriggering()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x9003, 0x80);
            mapper.ClockCpu(1);
            Assert.That(mapper.IrqCounter, Is.EqualTo(0xFFFF));
            Assert.That(mapper.IrqPending, Is.False);
        }

        private static Mapper065 CreateMapper()
        {
            var prg = new byte[16 * 8192];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 8192; i++) prg[bank * 8192 + i] = (byte)(0x20 + bank);
            var chr = new byte[32 * 1024];
            for (int bank = 0; bank < 32; bank++)
                for (int i = 0; i < 1024; i++) chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper065(prg, chr, MirroringMode.Vertical);
        }
    }
}
