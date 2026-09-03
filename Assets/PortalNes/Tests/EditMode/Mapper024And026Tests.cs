using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper024And026Tests
    {
        [Test]
        public void MapsVrc6PrgAndChrBanks()
        {
            var mapper = CreateMapper24();
            mapper.CpuWrite(0x8000, 2);
            mapper.CpuWrite(0xC000, 6);
            for (int i = 0; i < 8; i++) mapper.CpuWrite((ushort)(0xD000 + i % 4 + (i >= 4 ? 0x1000 : 0)), (byte)(8 + i));

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x14));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x16));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x2F));
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x48));
            Assert.That(mapper.PpuRead(0x1C00), Is.EqualTo(0x4F));
        }

        [Test]
        public void EnablesPrgRamAndSelectsMirroring()
        {
            var mapper = CreateMapper24();
            mapper.CpuWrite(0x6000, 0xA5);
            Assert.That(mapper.CpuRead(0x6000), Is.Zero);
            mapper.CpuWrite(0xB003, 0xA4);
            mapper.CpuWrite(0x6000, 0xA5);
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0xA5));
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        [Test]
        public void ProducesTwoPulseAndSawAudio()
        {
            var mapper = CreateMapper24();
            mapper.CpuWrite(0x9000, 0x8F);
            mapper.CpuWrite(0x9001, 0);
            mapper.CpuWrite(0x9002, 0x80);
            mapper.CpuWrite(0xA000, 0x8E);
            mapper.CpuWrite(0xA001, 0);
            mapper.CpuWrite(0xA002, 0x80);
            mapper.CpuWrite(0xB000, 8);
            mapper.CpuWrite(0xB001, 0);
            mapper.CpuWrite(0xB002, 0x80);

            mapper.ClockAudio(2);
            Assert.That(mapper.ExpansionAudioSample, Is.GreaterThanOrEqualTo(30));
        }

        [Test]
        public void RaisesAndAcknowledgesVrc6Irq()
        {
            var mapper = CreateMapper24();
            mapper.CpuWrite(0xF000, 0xFE);
            mapper.CpuWrite(0xF001, 0x07);
            mapper.ClockCpu(2);
            Assert.That(mapper.IrqPending, Is.True);
            mapper.CpuWrite(0xF002, 0);
            Assert.That(mapper.IrqPending, Is.False);
        }

        [Test]
        public void Mapper26SwapsA0AndA1()
        {
            var mapper = CreateMapper26();
            mapper.CpuWrite(0x9000, 0x8F);
            mapper.CpuWrite(0x9002, 0);
            mapper.CpuWrite(0x9001, 0x80);
            mapper.ClockAudio(1);
            Assert.That(mapper.ExpansionAudioSample, Is.EqualTo(15));

            mapper.CpuWrite(0xD002, 9);
            Assert.That(mapper.PpuRead(0x0400), Is.EqualTo(0x49));
        }

        private static Mapper024 CreateMapper24()
        {
            CreateMemory(out byte[] prg, out byte[] chr);
            return new Mapper024(prg, chr, MirroringMode.Vertical);
        }

        private static Mapper026 CreateMapper26()
        {
            CreateMemory(out byte[] prg, out byte[] chr);
            return new Mapper026(prg, chr, MirroringMode.Vertical);
        }

        private static void CreateMemory(out byte[] prg, out byte[] chr)
        {
            prg = new byte[16 * 16384];
            chr = new byte[32 * 1024];
            for (int bank = 0; bank < 32; bank++)
                for (int i = 0; i < 8192; i++)
                    prg[bank * 8192 + i] = (byte)(0x10 + bank);
            for (int bank = 0; bank < 32; bank++)
                for (int i = 0; i < 1024; i++)
                    chr[bank * 1024 + i] = (byte)(0x40 + bank);
        }
    }
}
