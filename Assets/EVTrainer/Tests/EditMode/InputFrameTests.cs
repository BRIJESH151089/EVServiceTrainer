using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class InputFrameTests
    {
        [Test]
        public void Values_AreClampedToZeroOne()
        {
            var frame = new InputFrame(InputSource.Controller, 1.5f, -0.2f, float.NaN, PartPose.Identity, true);

            Assert.That(frame.Grip, Is.EqualTo(1f));
            Assert.That(frame.Trigger, Is.EqualTo(0f));
            Assert.That(frame.Pinch, Is.EqualTo(0f));
            Assert.That(frame.Source, Is.EqualTo(InputSource.Controller));
        }

        [Test]
        public void None_IsUntrackedAndZero()
        {
            var frame = InputFrame.None;

            Assert.That(frame.Source, Is.EqualTo(InputSource.None));
            Assert.That(frame.IsTracked, Is.False);
            Assert.That(frame.Grip + frame.Trigger + frame.Pinch, Is.EqualTo(0f));
        }
    }
}
