using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper073Tests
    {
        [Test]
        public void MapsSelectedAndFixedPrgBanks()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xF000, 3);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x13));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x17));
        }

        [Test]
        public void ReadsAndWritesPrgAndChrRam()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x6123, 0xA5);
            mapper.PpuWrite(0x1234, 0x5A);
            Assert.That(mapper.CpuRead(0x6123), Is.EqualTo(0xA5));
            Assert.That(mapper.PpuRead(0x1234), Is.EqualTo(0x5A));
        }

        [Test]
        public void RaisesSixteenBitIrqAndReloadsLatch()
        {
            var mapper = CreateMapper();
            WriteLatch(mapper, 0xFFFE);
            mapper.CpuWrite(0xC000, 0x03);
            mapper.ClockCpu(2);
            Assert.That(mapper.IrqPending, Is.True);
            mapper.CpuWrite(0xD000, 0);
            Assert.That(mapper.IrqPending, Is.False);
            mapper.ClockCpu(2);
            Assert.That(mapper.IrqPending, Is.True);
        }

        [Test]
        public void EightBitModePreservesUpperCounterByte()
        {
            var mapper = CreateMapper();
            WriteLatch(mapper, 0x12FE);
            mapper.CpuWrite(0xC000, 0x07);
            mapper.ClockCpu(2);
            Assert.That(mapper.IrqPending, Is.True);
            mapper.CpuWrite(0xD000, 0);
            mapper.ClockCpu(2);
            Assert.That(mapper.IrqPending, Is.True);
        }

        private static Mapper073 CreateMapper()
        {
            var prg = new byte[8 * 16384];
            for (int bank = 0; bank < 8; bank++)
                for (int i = 0; i < 16384; i++)
                    prg[bank * 16384 + i] = (byte)(0x10 + bank);
            return new Mapper073(prg, new byte[8192]);
        }

        private static void WriteLatch(Mapper073 mapper, ushort value)
        {
            mapper.CpuWrite(0x8000, (byte)(value & 0x0F));
            mapper.CpuWrite(0x9000, (byte)((value >> 4) & 0x0F));
            mapper.CpuWrite(0xA000, (byte)((value >> 8) & 0x0F));
            mapper.CpuWrite(0xB000, (byte)((value >> 12) & 0x0F));
        }
    }
}
