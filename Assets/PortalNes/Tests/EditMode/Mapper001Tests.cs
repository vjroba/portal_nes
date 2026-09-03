using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper001Tests
    {
        [Test]
        public void DefaultModeSwitchesLowerPrgAndFixesLastBank()
        {
            var mapper = CreateMapper();
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x10));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x13));
            WriteRegister(mapper, 0xE000, 2);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x13));
        }

        [Test]
        public void SupportsAllMirroringModes()
        {
            var mapper = CreateMapper();
            WriteRegister(mapper, 0x8000, 0);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenLower));
            WriteRegister(mapper, 0x8000, 1);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.SingleScreenUpper));
            WriteRegister(mapper, 0x8000, 2);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Vertical));
            WriteRegister(mapper, 0x8000, 3);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        [Test]
        public void SwitchesIndependentFourKilobyteChrBanks()
        {
            var mapper = CreateMapper();
            WriteRegister(mapper, 0x8000, 0x1C);
            WriteRegister(mapper, 0xA000, 2);
            WriteRegister(mapper, 0xC000, 3);
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x42));
            Assert.That(mapper.PpuRead(0x1000), Is.EqualTo(0x43));
        }

        [Test]
        public void PrgRamCanBeWrittenAndDisabled()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x6000, 0xA5);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0xA5));
            WriteRegister(mapper, 0xE000, 0x10);
            mapper.CpuWrite(0x6000, 0x55);
            Assert.That(mapper.CpuRead(0x6000), Is.Zero);
        }

        [Test]
        public void ResetWriteRestoresFixedLastBankMode()
        {
            var mapper = CreateMapper();
            WriteRegister(mapper, 0x8000, 0);
            mapper.CpuWrite(0x8000, 0x80);
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x13));
        }

        private static Mapper001 CreateMapper()
        {
            var prg = new byte[4 * 16384];
            var chr = new byte[4 * 4096];
            for (int i = 0; i < 4; i++)
            {
                prg[i * 16384] = (byte)(0x10 + i);
                chr[i * 4096] = (byte)(0x40 + i);
            }
            return new Mapper001(prg, chr, false);
        }

        private static void WriteRegister(Mapper001 mapper, ushort address, byte value)
        {
            for (int bit = 0; bit < 5; bit++)
                mapper.CpuWrite(address, (byte)((value >> bit) & 1));
        }
    }
}
