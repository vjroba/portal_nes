using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper018Tests
    {
        [Test]
        public void CombinesSplitPrgAndChrBankNibbles()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 3); mapper.CpuWrite(0x8001, 1);
            mapper.CpuWrite(0xA000, 5); mapper.CpuWrite(0xA001, 2);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x13));
            Assert.That(mapper.PpuRead(0), Is.EqualTo(0x25));
        }

        [Test]
        public void ProtectsPrgRamAndChangesMirroring()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x6000, 0x55);
            Assert.That(mapper.CpuRead(0x6000), Is.Zero);
            mapper.CpuWrite(0x9002, 3); mapper.CpuWrite(0x6000, 0x55);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0x55));
            mapper.CpuWrite(0xF002, 3);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
        }

        [Test]
        public void RaisesVariableWidthIrqAndPreservesUpperBits()
        {
            var mapper = CreateMapper();
            WriteReload(mapper, 0x1231);
            mapper.CpuWrite(0xF000, 0); mapper.CpuWrite(0xF001, 0x09);
            mapper.ClockCpu(2);
            Assert.That(mapper.IrqPending, Is.True);
            Assert.That(mapper.IrqCounter, Is.EqualTo(0x123F));
            mapper.CpuWrite(0xF001, 0);
            Assert.That(mapper.IrqPending, Is.False);
        }

        [Test]
        public void SixteenBitIrqWrapsAtZero()
        {
            var mapper = CreateMapper();
            WriteReload(mapper, 1);
            mapper.CpuWrite(0xF000, 0); mapper.CpuWrite(0xF001, 1);
            mapper.ClockCpu(2);
            Assert.That(mapper.IrqPending, Is.True);
            Assert.That(mapper.IrqCounter, Is.EqualTo(0xFFFF));
        }

        private static Mapper018 CreateMapper()
        {
            var prg = new byte[64 * 8192];
            var chr = new byte[256 * 1024];
            for (int bank = 0; bank < 64; bank++)
                for (int i = 0; i < 8192; i++) prg[bank * 8192 + i] = (byte)bank;
            for (int bank = 0; bank < 256; bank++)
                for (int i = 0; i < 1024; i++) chr[bank * 1024 + i] = (byte)bank;
            return new Mapper018(prg, chr, MirroringMode.Horizontal);
        }

        private static void WriteReload(Mapper018 mapper, ushort value)
        {
            for (int nibble = 0; nibble < 4; nibble++)
                mapper.CpuWrite((ushort)(0xE000 + nibble),
                    (byte)((value >> (nibble * 4)) & 0x0F));
        }
    }
}
