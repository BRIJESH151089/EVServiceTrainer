using System;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class HapticProfilesTests
    {
        private static HapticProfiles P => HapticProfiles.Default;

        [Test]
        public void Events_MapOntoTheSharedProfiles()
        {
            Assert.That(HapticProfiles.KindFor(FeedbackCue.Snap), Is.EqualTo(HapticKind.Snap));
            Assert.That(HapticProfiles.KindFor(FeedbackCue.BoltSeated), Is.EqualTo(HapticKind.Snap), "locking in feels the same for parts and bolts");
            Assert.That(HapticProfiles.KindFor(FeedbackCue.WrenchAttach), Is.EqualTo(HapticKind.Snap));
            Assert.That(HapticProfiles.KindFor(FeedbackCue.Reject), Is.EqualTo(HapticKind.Reject));
            Assert.That(HapticProfiles.KindFor(FeedbackCue.BoltThreadTick), Is.EqualTo(HapticKind.LightTick));
            Assert.That(HapticProfiles.KindFor(FeedbackCue.RatchetBack), Is.EqualTo(HapticKind.LightTick));
            Assert.That(HapticProfiles.KindFor(FeedbackCue.UiPress), Is.EqualTo(HapticKind.LightTick));
            Assert.That(HapticProfiles.KindFor(FeedbackCue.TorqueClick), Is.EqualTo(HapticKind.TorqueClick));
            Assert.That(HapticProfiles.KindFor(FeedbackCue.OverTorque), Is.EqualTo(HapticKind.OverTorque));
            Assert.That(P.ForCue(FeedbackCue.CheckingFit).IsNone, Is.True);
            Assert.That(P.ForCue(FeedbackCue.ModuleComplete).IsNone, Is.True);
        }

        [Test]
        public void EveryCue_HasAValidPulse()
        {
            foreach (FeedbackCue cue in Enum.GetValues(typeof(FeedbackCue)))
            {
                var p = P.ForCue(cue);
                Assert.That(p.Amplitude, Is.InRange(0f, 1f), cue.ToString());
                Assert.That(p.Seconds, Is.InRange(0f, 2f), cue.ToString());
            }
        }

        [Test]
        public void Strength_ShowsHowImportantTheEventIs()
        {
            Assert.That(P.Get(HapticKind.LightTick).Amplitude, Is.LessThan(P.Get(HapticKind.Snap).Amplitude));
            Assert.That(P.Get(HapticKind.Snap).Amplitude, Is.LessThan(P.Get(HapticKind.Reject).Amplitude));
            Assert.That(P.Get(HapticKind.Reject).Seconds, Is.GreaterThan(P.Get(HapticKind.Snap).Seconds), "no = long buzz");
            Assert.That(P.Get(HapticKind.OverTorque).Seconds, Is.GreaterThan(P.Get(HapticKind.TorqueClick).Seconds));
        }

        [Test]
        public void TorqueRamp_GrowsWithTheSquareOfTorqueUpToSpec()
        {
            var zero = P.TorqueRamp(0f, 8f);
            var half = P.TorqueRamp(4f, 8f);
            var spec = P.TorqueRamp(8f, 8f);
            var over = P.TorqueRamp(12f, 8f);

            Assert.That(zero.Amplitude, Is.EqualTo(P.RampMinAmplitude).Within(1e-6f));
            Assert.That(half.Amplitude, Is.EqualTo(P.RampMinAmplitude + (P.RampMaxAmplitude - P.RampMinAmplitude) * 0.25f).Within(1e-6f));
            Assert.That(spec.Amplitude, Is.EqualTo(P.RampMaxAmplitude).Within(1e-6f));
            Assert.That(over.Amplitude, Is.EqualTo(P.RampMaxAmplitude).Within(1e-6f), "capped at spec");
            Assert.That(spec.Seconds, Is.EqualTo(P.RampSeconds));
            Assert.That(spec.Amplitude, Is.LessThan(P.Get(HapticKind.TorqueClick).Amplitude), "the click stands out from the ramp");
        }

        [Test]
        public void TorqueRamp_BadInput_GivesTheMinimum()
        {
            Assert.That(P.TorqueRamp(float.NaN, 8f).Amplitude, Is.EqualTo(P.RampMinAmplitude));
            Assert.That(P.TorqueRamp(5f, 0f).Amplitude, Is.EqualTo(P.RampMinAmplitude));
            Assert.That(P.TorqueRamp(-3f, 8f).Amplitude, Is.EqualTo(P.RampMinAmplitude));
        }

        [Test]
        public void TorqueClick_LastsLongerWhenTheWrenchStaysOn()
        {
            Assert.That(P.TorqueClick(false), Is.EqualTo(P.Get(HapticKind.TorqueClick)));
            Assert.That(P.TorqueClick(true).Seconds, Is.EqualTo(P.TorqueClickHoldSeconds));
            Assert.That(P.TorqueClick(true).Amplitude, Is.EqualTo(P.Get(HapticKind.TorqueClick).Amplitude));
        }

        [Test]
        public void InvalidData_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticPulse(1.5f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HapticPulse(0.5f, -0.1f));
            var t = new HapticPulse(0.2f, 0.02f);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new HapticProfiles(t, t, t, t, t, t, 0.5f, 0.2f, 0.02f, 0.3f)); // max below min
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new HapticProfiles(t, t, t, t, new HapticPulse(1f, 0.5f), t, 0.1f, 0.4f, 0.02f, 0.3f)); // hold shorter than the click
        }
    }
}
