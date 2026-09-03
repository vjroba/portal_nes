using NUnit.Framework;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper019Tests
    {
        [Test]
        public void MapsEightChrFourNametableAndThreePrgBanks()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 3);
            mapper.CpuWrite(0xB800, 11);
            mapper.CpuWrite(0xC000, 5);
            mapper.CpuWrite(0xE000, 2);
            mapper.CpuWrite(0xE800, 3);
            mapper.CpuWrite(0xF000, 4);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x1C00), Is.EqualTo(0x4B));
            Assert.That(mapper.ReadNametable(0x2000), Is.EqualTo(0x45));
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x13));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x14));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x1F));
        }

        [Test]
        public void PatternAndNametableBanksCanShareCiram()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x8000, 0xE1);
            mapper.PpuWrite(0x0012, 0x66);
            Assert.That(mapper.PpuRead(0x0012), Is.EqualTo(0x66));

            mapper.CpuWrite(0xC000, 0xE1);
            Assert.That(mapper.ReadNametable(0x2012), Is.EqualTo(0x66));
            mapper.WriteNametable(0x2013, 0x77);
            Assert.That(mapper.PpuRead(0x0013), Is.EqualTo(0x77));

            mapper.CpuWrite(0xE800, 0x40);
            Assert.That(mapper.PpuRead(0x0012), Is.EqualTo(0x21));
        }

        [Test]
        public void IrqCountsCpuCyclesAndAcknowledgesOnRegisterWrite()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x5000, 0xFE);
            mapper.CpuWrite(0x5800, 0xFF);

            mapper.ClockCpu(1);
            Assert.That(mapper.IrqPending, Is.True);
            Assert.That(mapper.IrqCounter, Is.EqualTo(0x7FFF));

            mapper.CpuWrite(0x5000, 0);
            Assert.That(mapper.IrqPending, Is.False);
        }

        [Test]
        public void InternalRamPortSupportsStoppedAutoIncrement()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0xF800, 0xFE);
            mapper.CpuWrite(0x4800, 0x12);
            mapper.CpuWrite(0x4800, 0x34);
            Assert.That(mapper.InternalAddress, Is.EqualTo(0x7F));

            mapper.CpuWrite(0xF800, 0xFE);
            Assert.That(mapper.CpuRead(0x4800), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0x4800), Is.EqualTo(0x34));
            Assert.That(mapper.InternalAddress, Is.EqualTo(0x7F));
        }

        [Test]
        public void ExternalRamHonorsProtectionKeyAndWindowBits()
        {
            var mapper = CreateMapper();
            mapper.CpuWrite(0x6000, 0x11);
            Assert.That(mapper.CpuRead(0x6000), Is.Zero);

            mapper.CpuWrite(0xF800, 0x40);
            mapper.CpuWrite(0x6000, 0x22);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0x22));

            mapper.CpuWrite(0xF800, 0x41);
            mapper.CpuWrite(0x6000, 0x33);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0x22));
        }

        [Test]
        public void WavetableChannelProducesExpansionAudio()
        {
            var mapper = CreateMapper();
            WriteInternal(mapper, 0x00, 0x0F);
            WriteInternal(mapper, 0x7C, 0xFC);
            WriteInternal(mapper, 0x7E, 0x00);
            WriteInternal(mapper, 0x7F, 0x0F);
            mapper.CpuWrite(0xE000, 0x00);

            mapper.ClockAudio(15);

            Assert.That(mapper.ExpansionAudioSample, Is.EqualTo(105f));
        }

        private static Mapper019 CreateMapper()
        {
            var prg = new byte[16 * 8192];
            var chr = new byte[256 * 1024];
            for (int bank = 0; bank < 16; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 256; bank++)
                for (int i = 0; i < 1024; i++)
                    chr[bank * 1024 + i] = (byte)(0x40 + bank);
            return new Mapper019(prg, chr);
        }

        private static void WriteInternal(Mapper019 mapper, byte address, byte value)
        {
            mapper.CpuWrite(0xF800, address);
            mapper.CpuWrite(0x4800, value);
        }
    }
}
