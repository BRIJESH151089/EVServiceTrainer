using System;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class SoundSynthTests
    {
        private static FeedbackCue[] AllCues => (FeedbackCue[])Enum.GetValues(typeof(FeedbackCue));

        private static float Peak(float[] s)
        {
            float max = 0f;
            foreach (var v in s) max = Math.Max(max, Math.Abs(v));
            return max;
        }

        [Test]
        public void EveryCue_IsShortFiniteAndBelowClipping()
        {
            foreach (var cue in AllCues)
            {
                var s = SoundSynth.Generate(cue);
                Assert.That(s.Length, Is.GreaterThan(100), cue.ToString());
                Assert.That(s.Length, Is.LessThanOrEqualTo(SoundSynth.DefaultSampleRate * 3 / 2), $"{cue}: at most 1.5 s");
                foreach (var v in s) Assert.That(float.IsNaN(v) || float.IsInfinity(v), Is.False, cue.ToString());
                Assert.That(Peak(s), Is.LessThanOrEqualTo(SoundSynth.MaxPeak), $"{cue}: no clipping");
                Assert.That(Peak(s), Is.EqualTo(SoundSynth.TargetPeak(cue)).Within(0.02f), $"{cue}: normalised to its level");
            }
        }

        [Test]
        public void EveryCue_StartsAndEndsAtSilence()
        {
            foreach (var cue in AllCues)
            {
                var s = SoundSynth.Generate(cue);
                Assert.That(s[0], Is.EqualTo(0f).Within(1e-6f), $"{cue}: first sample");
                Assert.That(s[s.Length - 1], Is.EqualTo(0f).Within(1e-6f), $"{cue}: last sample");
            }
        }

        [Test]
        public void SameCue_GivesTheSameSamples()
        {
            var a = SoundSynth.Generate(FeedbackCue.WrenchAttach);
            var b = SoundSynth.Generate(FeedbackCue.WrenchAttach);
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void StopSignals_AreLouderThanBackgroundTicks()
        {
            Assert.That(SoundSynth.TargetPeak(FeedbackCue.TorqueClick), Is.GreaterThan(SoundSynth.TargetPeak(FeedbackCue.RatchetBack)));
            Assert.That(SoundSynth.TargetPeak(FeedbackCue.OverTorque), Is.GreaterThan(SoundSynth.TargetPeak(FeedbackCue.BoltThreadTick)));
        }

        [Test]
        public void AmbienceLoop_IsQuietExactLengthAndSeamless()
        {
            const int rate = 22050;
            var s = SoundSynth.AmbienceLoop(rate, 8f, 0.25f);
            Assert.That(s.Length, Is.EqualTo(rate * 8));
            Assert.That(Peak(s), Is.LessThanOrEqualTo(0.25f + 1e-5f));

            float maxStep = 0f;
            for (int i = 1; i < s.Length; i++) maxStep = Math.Max(maxStep, Math.Abs(s[i] - s[i - 1]));
            float wrap = Math.Abs(s[0] - s[s.Length - 1]);
            Assert.That(wrap, Is.LessThanOrEqualTo(maxStep), "the loop point is no bigger a jump than any normal step");
        }

        [Test]
        public void InvalidSampleRate_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SoundSynth.Generate(FeedbackCue.Snap, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => SoundSynth.AmbienceLoop(22050, 0.2f));
        }

        [Test]
        public void Routing_InterfaceSoundsAre2D_WorkshopSoundsAreAtTheSource()
        {
            Assert.That(FeedbackRouting.BusFor(FeedbackCue.UiPress), Is.EqualTo(SoundBus.Ui));
            Assert.That(FeedbackRouting.BusFor(FeedbackCue.ModuleComplete), Is.EqualTo(SoundBus.Ui));
            Assert.That(FeedbackRouting.IsSpatial(FeedbackCue.UiPress), Is.False);
            foreach (var cue in new[] { FeedbackCue.PickUp, FeedbackCue.Snap, FeedbackCue.TorqueClick, FeedbackCue.BoltThreadTick, FeedbackCue.RatchetBack })
            {
                Assert.That(FeedbackRouting.BusFor(cue), Is.EqualTo(SoundBus.Sfx), cue.ToString());
                Assert.That(FeedbackRouting.IsSpatial(cue), Is.True, cue.ToString());
            }
        }
    }
}
