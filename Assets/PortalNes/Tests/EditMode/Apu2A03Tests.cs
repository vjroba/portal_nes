using System;
using NUnit.Framework;
using PortalNes.Emulator.Apu;

namespace PortalNes.Tests
{
    public sealed class Apu2A03Tests
    {
        [Test]
        public void PulseChannelProducesPcmSamples()
        {
            var apu = new Apu2A03();
            apu.Reset();
            apu.SetSampleRate(48000);
            apu.WriteRegister(0x4015, 0x01);
            apu.WriteRegister(0x4000, 0xBF); // 50% duty, halt length, constant volume 15.
            apu.WriteRegister(0x4002, 0xFD);
            apu.WriteRegister(0x4003, 0x00);
            apu.Clock(50000);

            var samples = new float[2048];
            int count = apu.ReadSamples(samples, 0, samples.Length);
            Assert.That(count, Is.GreaterThan(0));
            Assert.That(Array.Exists(samples, sample => Math.Abs(sample) > 0.0001f), Is.True);
        }

        [Test]
        public void StatusReportsAndDisablesLengthCounters()
        {
            var apu = new Apu2A03();
            apu.Reset();
            apu.WriteRegister(0x4015, 0x01);
            apu.WriteRegister(0x4003, 0xF8);
            Assert.That(apu.ReadStatus() & 1, Is.Not.Zero);
            apu.WriteRegister(0x4015, 0x00);
            Assert.That(apu.ReadStatus() & 1, Is.Zero);
        }

        [Test]
        public void FrameIrqIsClearedByStatusRead()
        {
            var apu = new Apu2A03();
            apu.Reset();
            apu.Clock(29829);
            Assert.That(apu.IrqPending, Is.True);
            Assert.That(apu.ReadStatus() & 0x40, Is.Not.Zero);
            Assert.That(apu.IrqPending, Is.False);
        }

        [Test]
        public void DmcFetchesSampleAndStallsCpu()
        {
            var apu = new Apu2A03();
            ushort fetchedAddress = 0;
            int fetches = 0, stalledCycles = 0;
            apu.ConfigureDmc(address =>
            {
                fetchedAddress = address;
                fetches++;
                return 0xFF;
            }, cycles => stalledCycles += cycles);
            apu.Reset();
            apu.WriteRegister(0x4012, 0x00);
            apu.WriteRegister(0x4013, 0x00);
            apu.WriteRegister(0x4015, 0x10);

            Assert.That(fetches, Is.EqualTo(1));
            Assert.That(fetchedAddress, Is.EqualTo(0xC000));
            Assert.That(stalledCycles, Is.EqualTo(4));
            Assert.That(apu.ReadStatus() & 0x10, Is.Zero);

            apu.Clock(5000);
            var samples = new float[256];
            int count = apu.ReadSamples(samples, 0, samples.Length);
            Assert.That(count, Is.GreaterThan(0));
            Assert.That(Array.Exists(samples, sample => Math.Abs(sample) > 0.0001f), Is.True);
        }

        [Test]
        public void DmcLoopRestartsSample()
        {
            var apu = new Apu2A03();
            int fetches = 0;
            apu.ConfigureDmc(_ => { fetches++; return 0x55; }, _ => { });
            apu.Reset();
            apu.WriteRegister(0x4010, 0x40);
            apu.WriteRegister(0x4013, 0x00);
            apu.WriteRegister(0x4015, 0x10);
            apu.Clock(10000);

            Assert.That(fetches, Is.GreaterThan(1));
            Assert.That(apu.ReadStatus() & 0x10, Is.Not.Zero);
            Assert.That(apu.IrqPending, Is.False);
        }

        [Test]
        public void DmcIrqPersistsAcrossStatusReadAndIsClearedByControlWrite()
        {
            var apu = new Apu2A03();
            apu.ConfigureDmc(_ => 0, _ => { });
            apu.Reset();
            apu.WriteRegister(0x4010, 0x80);
            apu.WriteRegister(0x4013, 0x00);
            apu.WriteRegister(0x4015, 0x10);

            Assert.That(apu.IrqPending, Is.True);
            Assert.That(apu.ReadStatus() & 0x80, Is.Not.Zero);
            Assert.That(apu.IrqPending, Is.True);
            apu.WriteRegister(0x4015, 0x00);
            Assert.That(apu.IrqPending, Is.False);
        }
    }
}
