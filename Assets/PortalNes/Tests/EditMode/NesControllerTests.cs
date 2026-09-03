using NUnit.Framework;
using PortalNes.Emulator.Input;

namespace PortalNes.Tests
{
    public sealed class NesControllerTests
    {
        [Test]
        public void FallingStrobe_LatchesButtonsInNesOrder()
        {
            var pad = new NesController { State = 0b1010_0101 };
            pad.WriteStrobe(1); pad.WriteStrobe(0);
            byte result = 0;
            for (int i = 0; i < 8; i++) result |= (byte)(pad.Read() << i);
            Assert.That(result, Is.EqualTo(0b1010_0101));
        }

        [Test]
        public void ReadsAfterEightButtons_ReturnOne()
        {
            var pad = new NesController { State = 0 };
            pad.WriteStrobe(1); pad.WriteStrobe(0);
            for (int i = 0; i < 8; i++) pad.Read();
            Assert.That(pad.Read(), Is.EqualTo(1));
        }

        [Test]
        public void HighStrobe_ContinuouslyReportsCurrentAButton()
        {
            var pad = new NesController { State = 1 };
            pad.WriteStrobe(1);
            Assert.That(pad.Read(), Is.EqualTo(1));
            pad.State = 0;
            Assert.That(pad.Read(), Is.Zero);
        }
    }
}
