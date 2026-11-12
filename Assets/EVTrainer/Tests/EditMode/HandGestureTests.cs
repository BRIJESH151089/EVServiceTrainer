using System.Linq;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class HandGestureTests
    {
        private static readonly HandGestureSettings Settings = HandGestureSettings.Default; // pinch 15..60 mm

        private static float PinchAt(float metres) =>
            HandGesture.Pinch(Vector3.Zero, new Vector3(metres, 0f, 0f), Settings);

        [Test]
        public void Pinch_TouchingTips_IsOne()
        {
            Assert.That(PinchAt(0f), Is.EqualTo(1f));
            Assert.That(PinchAt(0.015f), Is.EqualTo(1f));
        }

        [Test]
        public void Pinch_OpenHand_IsZero()
        {
            Assert.That(PinchAt(0.06f), Is.EqualTo(0f));
            Assert.That(PinchAt(0.12f), Is.EqualTo(0f));
        }

        [Test]
        public void Pinch_HalfwayBetweenThresholds_IsHalf()
        {
            Assert.That(PinchAt(0.0375f), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Pinch_IncreasesAsTipsCloseIn()
        {
            Assert.That(PinchAt(0.03f), Is.GreaterThan(PinchAt(0.05f)));
        }

        [Test]
        public void DistanceTo01_NaN_IsZero()
        {
            Assert.That(HandGesture.DistanceTo01(float.NaN, 0.01f, 0.05f), Is.EqualTo(0f));
        }

        [Test]
        public void Grip_Fist_IsOne_OpenHand_IsZero()
        {
            var palm = Vector3.Zero;
            var curled = new Vector3(0f, 0.03f, 0f);   // tips close to palm
            var straight = new Vector3(0f, 0.10f, 0f); // tips far from palm

            Assert.That(HandGesture.Grip(palm, curled, curled, curled, Settings), Is.EqualTo(1f));
            Assert.That(HandGesture.Grip(palm, straight, straight, straight, Settings), Is.EqualTo(0f));
        }

        [Test]
        public void Grip_IsAverageOfThreeFingers()
        {
            var palm = Vector3.Zero;
            var curled = new Vector3(0f, 0.03f, 0f);
            var straight = new Vector3(0f, 0.10f, 0f);

            Assert.That(HandGesture.Grip(palm, curled, straight, straight, Settings), Is.EqualTo(1f / 3f).Within(1e-5f));
        }

        [Test]
        public void Settings_RejectInvertedThresholds()
        {
            Assert.Throws<System.ArgumentException>(() => new HandGestureSettings(0.06f, 0.015f, 0.045f, 0.095f));
        }

        [Test]
        public void Skeleton_HasFiveChains_From_Wrist_To_Tips_CoveringAllFingerJoints()
        {
            Assert.That(HandSkeleton.FingerChains.Count, Is.EqualTo(5));
            Assert.That(HandSkeleton.FingerChains.All(c => c[0] == HandJoint.Wrist), Is.True);
            var tips = HandSkeleton.FingerChains.Select(c => c[c.Count - 1]).ToArray();
            Assert.That(tips, Is.EqualTo(new[] { HandJoint.ThumbTip, HandJoint.IndexTip, HandJoint.MiddleTip, HandJoint.RingTip, HandJoint.LittleTip }));

            // Every joint except Palm appears in exactly one chain (Wrist is shared).
            var fingerJoints = HandSkeleton.FingerChains.SelectMany(c => c.Skip(1)).ToList();
            Assert.That(fingerJoints.Count, Is.EqualTo(HandSkeleton.JointCount - 2));
            Assert.That(fingerJoints.Distinct().Count(), Is.EqualTo(fingerJoints.Count));
        }
    }
}
