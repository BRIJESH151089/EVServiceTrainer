using System;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class PushButtonLogicTests
    {
        [Test]
        public void TouchAndShallowPress_DoNotClick()
        {
            var b = new PushButtonLogic(0.7f, 0.15f);

            Assert.That(b.Update(0.05f), Is.False, "first touch");
            Assert.That(b.Update(0.5f), Is.False);
            Assert.That(b.Update(0.69f), Is.False);
            Assert.That(b.IsArmed, Is.True);
        }

        [Test]
        public void ClicksAtThreshold_ExactlyOnce()
        {
            var b = new PushButtonLogic(0.7f, 0.15f);
            b.Update(0.3f);

            Assert.That(b.Update(0.7f), Is.True, "fires at 70 %");
            Assert.That(b.Update(0.9f), Is.False, "held deeper: no repeat");
            Assert.That(b.Update(1f), Is.False);
            Assert.That(b.IsArmed, Is.False);
        }

        [Test]
        public void PartialRelease_DoesNotRearm()
        {
            var b = new PushButtonLogic(0.7f, 0.15f);
            b.Update(0.8f);

            b.Update(0.4f);          // let go only half-way
            Assert.That(b.IsArmed, Is.False);
            Assert.That(b.Update(0.8f), Is.False, "pushing again without returning does not fire");
        }

        [Test]
        public void FullReturn_Rearms_AndCanClickAgain()
        {
            var b = new PushButtonLogic(0.7f, 0.15f);
            b.Update(0.8f);

            Assert.That(b.Update(0.15f), Is.False, "returning never fires");
            Assert.That(b.IsArmed, Is.True);
            Assert.That(b.Update(0.75f), Is.True);
        }

        [Test]
        public void JumpStraightToFullDepth_ClicksOnce()
        {
            var b = new PushButtonLogic();

            Assert.That(b.Update(1f), Is.True);
            Assert.That(b.Update(1f), Is.False);
        }

        [Test]
        public void OutOfRangeAndNaN_AreClamped()
        {
            var b = new PushButtonLogic(0.7f, 0.15f);

            Assert.That(b.Update(float.NaN), Is.False);
            Assert.That(b.Update(-3f), Is.False);
            Assert.That(b.Update(5f), Is.True);
            Assert.That(b.Update(float.NaN), Is.False, "NaN counts as released");
            Assert.That(b.IsArmed, Is.True);
        }

        [Test]
        public void Reset_Rearms()
        {
            var b = new PushButtonLogic();
            b.Update(1f);

            b.Reset();

            Assert.That(b.IsArmed, Is.True);
        }

        [Test]
        public void InvalidThresholds_Throw()
        {
            Assert.Throws<ArgumentException>(() => new PushButtonLogic(0.5f, 0.6f));
            Assert.Throws<ArgumentException>(() => new PushButtonLogic(1.2f, 0.1f));
            Assert.Throws<ArgumentException>(() => new PushButtonLogic(0.7f, -0.1f));
        }

        [Test]
        public void ProceduralClick_IsShortAndWithinRange()
        {
            var s = ProceduralClick.CreateSamples(44100, 0.05f);

            Assert.That(s.Length, Is.EqualTo(2205));
            foreach (var v in s) Assert.That(Math.Abs(v), Is.LessThanOrEqualTo(1f));
            Assert.That(Math.Abs(s[s.Length - 1]), Is.LessThan(0.1f), "decays by the end");
            Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralClick.CreateSamples(0));
        }
    }
}
