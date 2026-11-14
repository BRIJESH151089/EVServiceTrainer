using System;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class ControllerHandPoseTests
    {
        /// <summary>Half way between the relaxed curl and a full curl.</summary>
        private static float Halfway => (ControllerHandPose.RestCurl + 1f) * 0.5f;

        [Test]
        public void Released_IsARelaxedOpenHand()
        {
            var c = ControllerHandPose.FromInput(0f, 0f);
            foreach (Finger f in Enum.GetValues(typeof(Finger))) Assert.That(c[f], Is.EqualTo(ControllerHandPose.RestCurl).Within(1e-5f), f.ToString());
        }

        [Test]
        public void Grip_ClosesTheFist_IndexMostlyFree()
        {
            var c = ControllerHandPose.FromInput(1f, 0f);
            Assert.That(c.Middle, Is.EqualTo(1f));
            Assert.That(c.Ring, Is.EqualTo(1f));
            Assert.That(c.Little, Is.EqualTo(1f));
            Assert.That(c.Thumb, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(c.Index, Is.LessThan(Halfway), "index stays loose so the trigger can still curl it");
        }

        [Test]
        public void Trigger_CurlsTheIndexOnly()
        {
            var c = ControllerHandPose.FromInput(0f, 1f);
            Assert.That(c.Index, Is.EqualTo(1f));
            Assert.That(c.Middle, Is.EqualTo(ControllerHandPose.RestCurl).Within(1e-5f));
            Assert.That(ControllerHandPose.FromInput(0f, 0.5f).Index, Is.EqualTo(Halfway).Within(1e-5f), "half trigger = half way from rest to curled");
        }

        [Test]
        public void OutOfRangeAndNaN_AreClamped()
        {
            var c = ControllerHandPose.FromInput(2f, float.NaN);
            Assert.That(c.Middle, Is.EqualTo(1f));
            Assert.That(c.Index, Is.LessThan(Halfway));
            Assert.That(new FingerCurls(-1f, 5f, 0.5f, 0.5f, 0.5f).Index, Is.EqualTo(1f));
        }

        [Test]
        public void Ease_MovesSmoothlyAndArrives()
        {
            var open = ControllerHandPose.FromInput(0f, 0f);
            var fist = ControllerHandPose.FromInput(1f, 1f);
            var c = ControllerHandPose.Ease(open, fist, 1f / 72f);
            Assert.That(c.Middle, Is.GreaterThan(open.Middle).And.LessThan(fist.Middle), "one frame moves part of the way");
            for (int i = 0; i < 72; i++) c = ControllerHandPose.Ease(c, fist, 1f / 72f);
            Assert.That(c.Middle, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(ControllerHandPose.Ease(open, fist, 0.1f, 0f).Index, Is.EqualTo(1f), "no easing = target");
        }

        [Test]
        public void JointAngles_ScaleWithCurl_ThumbBendsLess()
        {
            Assert.That(ControllerHandPose.JointAngleDegrees(Finger.Index, 1, 1f), Is.EqualTo(95f));
            Assert.That(ControllerHandPose.JointAngleDegrees(Finger.Index, 0, 0.5f), Is.EqualTo(35f));
            Assert.That(ControllerHandPose.JointAngleDegrees(Finger.Thumb, 1, 1f), Is.LessThan(ControllerHandPose.JointAngleDegrees(Finger.Middle, 1, 1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ControllerHandPose.JointAngleDegrees(Finger.Ring, 3, 1f));
        }
    }
}
