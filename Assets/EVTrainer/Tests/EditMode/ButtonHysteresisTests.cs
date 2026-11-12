using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class ButtonHysteresisTests
    {
        [Test]
        public void Presses_AtPressThreshold_AndReportsEdgeOnce()
        {
            var b = new ButtonHysteresis(0.75f, 0.5f);

            Assert.That(b.Update(0.7f), Is.False);
            Assert.That(b.Update(0.75f), Is.True);
            Assert.That(b.WasPressedThisUpdate, Is.True);

            b.Update(0.9f);
            Assert.That(b.IsPressed, Is.True);
            Assert.That(b.WasPressedThisUpdate, Is.False);
        }

        [Test]
        public void StaysPressed_BetweenThresholds_ReleasesAtReleaseThreshold()
        {
            var b = new ButtonHysteresis(0.75f, 0.5f);
            b.Update(1f);

            Assert.That(b.Update(0.6f), Is.True, "jitter between thresholds must not release");
            Assert.That(b.Update(0.5f), Is.False);
            Assert.That(b.WasReleasedThisUpdate, Is.True);
        }

        [Test]
        public void Reset_ReleasesAndReportsEdge()
        {
            var b = new ButtonHysteresis();
            b.Update(1f);

            b.Reset();

            Assert.That(b.IsPressed, Is.False);
            Assert.That(b.WasReleasedThisUpdate, Is.True);
        }

        [Test]
        public void Constructor_RejectsReleaseAbovePress()
        {
            Assert.Throws<System.ArgumentException>(() => new ButtonHysteresis(0.5f, 0.75f));
        }
    }
}
