using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper009And010Tests
    {
        [Test]
        public void Mmc2MapsOneSwitchableAndThreeFixedPrgBanks()
        {
            byte[] prg = Banked(8 * 1024, 8, 0x10);
            var mapper = new Mapper009(prg, Banked(4 * 1024, 8, 0x40));

            mapper.CpuWrite(0xA000, 2);
            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x12));
            Assert.That(mapper.CpuRead(0xA000), Is.EqualTo(0x15));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x16));
            Assert.That(mapper.CpuRead(0xE000), Is.EqualTo(0x17));
        }

        [Test]
        public void Mmc2ChrTriggerChangesBankAfterTriggerByteWasRead()
        {
            byte[] chr = Banked(4 * 1024, 8, 0x40);
            var mapper = new Mapper009(Banked(8 * 1024, 4, 0x10), chr);
            mapper.CpuWrite(0xB000, 2);
            mapper.CpuWrite(0xC000, 3);

            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x43));
            Assert.That(mapper.PpuRead(0x0FD8), Is.EqualTo(0x43));
            Assert.That(mapper.Latch0, Is.EqualTo(0xFD));
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x42));
            mapper.PpuRead(0x0FE8);
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x43));
        }

        [Test]
        public void Mmc2DiagnosticPeekDoesNotMoveChrLatch()
        {
            var mapper = new Mapper009(Banked(8 * 1024, 4, 0x10),
                Banked(4 * 1024, 8, 0x40));

            Assert.That(mapper.Latch0, Is.EqualTo(0xFE));
            mapper.PpuPeek(0x0FD8);
            Assert.That(mapper.Latch0, Is.EqualTo(0xFE));
            mapper.PpuRead(0x0FD8);
            Assert.That(mapper.Latch0, Is.EqualTo(0xFD));
        }

        [Test]
        public void Mmc2LowLatchOnlyRespondsToExactTriggerAddresses()
        {
            var mapper = new Mapper009(Banked(8 * 1024, 4, 0x10),
                Banked(4 * 1024, 8, 0x40));

            mapper.PpuRead(0x0FD8);
            Assert.That(mapper.Latch0, Is.EqualTo(0xFD));
            mapper.PpuRead(0x0FE9);
            Assert.That(mapper.Latch0, Is.EqualTo(0xFD));
            mapper.PpuRead(0x0FE8);
            Assert.That(mapper.Latch0, Is.EqualTo(0xFE));
        }

        [Test]
        public void Mmc4SupportsPrgRamBankingMirroringAndWideFdTrigger()
        {
            var mapper = new Mapper010(Banked(16 * 1024, 4, 0x20),
                Banked(4 * 1024, 8, 0x50));
            mapper.CpuWrite(0xA000, 1);
            mapper.CpuWrite(0xB000, 4);
            mapper.CpuWrite(0xC000, 5);
            mapper.CpuWrite(0x6000, 0xA5);

            Assert.That(mapper.CpuRead(0x8000), Is.EqualTo(0x21));
            Assert.That(mapper.CpuRead(0xC000), Is.EqualTo(0x23));
            Assert.That(mapper.CpuRead(0x6000), Is.EqualTo(0xA5));
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x55));
            mapper.PpuRead(0x0FDF);
            Assert.That(mapper.PpuRead(0x0000), Is.EqualTo(0x54));
            mapper.CpuWrite(0xF000, 1);
            Assert.That(mapper.MirroringOverride, Is.EqualTo(MirroringMode.Horizontal));
        }

        private static byte[] Banked(int bankSize, int count, int first)
        {
            var data = new byte[bankSize * count];
            for (int bank = 0; bank < count; bank++)
                for (int i = 0; i < bankSize; i++)
                    data[bank * bankSize + i] = (byte)(first + bank);
            return data;
        }
    }
}
