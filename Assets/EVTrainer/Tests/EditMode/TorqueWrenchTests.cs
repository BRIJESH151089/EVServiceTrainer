using System;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class TorqueWrenchTests
    {
        private static readonly Vector3 Head = new Vector3(0.2f, 1.0f, 0.8f);
        private static readonly Vector3 Up = Vector3.UnitY;

        // Hand at radius r, angle a (degrees, clockwise from +X seen from above in Unity's left-handed world).
        private static Vector3 HandAt(float degrees, float radius = 0.25f, float height = 0f)
        {
            // Clockwise about +Y in our convention: +X turns towards -Z.
            var q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, degrees * (float)Math.PI / 180f);
            return Vector3.Transform(new Vector3(radius, 0f, 0f), q) + new Vector3(0f, height, 0f);
        }

        [Test]
        public void SocketResting_OnTheHead_Attaches_EvenAFewMillimetresOff()
        {
            var spec = WrenchSpec.Default;
            Assert.That(spec.IsOnHead(Head + new Vector3(0.008f, 0.002f, 0.006f), Up, Head, Up), Is.True, "resting on top, 1 cm off-centre");
            Assert.That(spec.IsOnHead(Head + new Vector3(0f, -0.02f, 0f), Up, Head, Up), Is.True, "pushed over the head");
            Assert.That(spec.IsOnHead(Head + new Vector3(0.02f, 0.035f, 0.02f), Up, Head, Up), Is.True, "just lowering onto it");
        }

        [Test]
        public void SocketTooFar_TooHigh_OrTooTilted_DoesNotAttach()
        {
            var spec = WrenchSpec.Default;
            Assert.That(spec.IsOnHead(Head + new Vector3(0.035f, 0f, 0f), Up, Head, Up), Is.False, "3.5 cm off the axis");
            Assert.That(spec.IsOnHead(Head + new Vector3(0f, 0.05f, 0f), Up, Head, Up), Is.False, "5 cm above");
            Assert.That(spec.IsOnHead(Head + new Vector3(0f, -0.04f, 0f), Up, Head, Up), Is.False, "4 cm below");
            var tilted = Vector3.Transform(Up, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 50f * (float)Math.PI / 180f));
            Assert.That(spec.IsOnHead(Head, tilted, Head, Up), Is.False, "socket 50° off the bolt axis");
        }

        [Test]
        public void Detach_WhenTheHandLeavesTheSwingPlane_By15cm()
        {
            var spec = WrenchSpec.Default;
            Assert.That(spec.ShouldDetach(Head + HandAt(30f, 0.25f, 0.10f), Head, Up), Is.False);
            Assert.That(spec.ShouldDetach(Head + HandAt(30f, 0.25f, -0.14f), Head, Up), Is.False);
            Assert.That(spec.ShouldDetach(Head + HandAt(30f, 0.25f, 0.16f), Head, Up), Is.True);
            Assert.That(spec.ShouldDetach(Head + HandAt(30f, 0.9f, 0f), Head, Up), Is.False, "distance in the plane never detaches");
        }

        [Test]
        public void ClockwiseSwing_DrivesTheBolt_HandleFollowsTheHand()
        {
            var swing = new RatchetSwing();
            var first = swing.Update(Up, HandAt(0f));
            Assert.That(first.DriveDegrees, Is.EqualTo(0f), "first update only sets the handle");
            Assert.That(first.HasAngle, Is.True);

            float driven = 0f;
            for (int a = 2; a <= 80; a += 2) driven += swing.Update(Up, HandAt(a)).DriveDegrees;

            Assert.That(driven, Is.EqualTo(80f).Within(0.01f));
            Assert.That(AxisAngle.WrapDelta(swing.HandleAngleDegrees - 80f), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void BackSwing_DoesNotDrive_AndClicksSoftly()
        {
            var spec = new WrenchSpec(ratchetClickDegrees: 6f);
            var swing = new RatchetSwing(spec);
            swing.Update(Up, HandAt(60f));
            float driven = 0f;
            int clicks = 0;
            for (int a = 58; a >= 0; a -= 2)
            {
                var step = swing.Update(Up, HandAt(a));
                driven += step.DriveDegrees;
                clicks += step.BackClicks;
            }
            Assert.That(driven, Is.EqualTo(0f));
            Assert.That(clicks, Is.EqualTo(10), "60° back / 6° per click");
            Assert.That(AxisAngle.WrapDelta(swing.HandleAngleDegrees - 0f), Is.EqualTo(0f).Within(0.01f), "the handle swings back with the hand");
        }

        [Test]
        public void SwingBackAndForth_OnlyForwardStrokesCount()
        {
            var swing = new RatchetSwing();
            swing.Update(Up, HandAt(0f));
            float driven = 0f;
            for (int stroke = 0; stroke < 3; stroke++)
            {
                for (int a = 5; a <= 30; a += 5) driven += swing.Update(Up, HandAt(a)).DriveDegrees;
                for (int a = 25; a >= 0; a -= 5) driven += swing.Update(Up, HandAt(a)).DriveDegrees;
            }
            Assert.That(driven, Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void HandOnTheAxis_GivesNoAngle_AndDoesNotJump()
        {
            var swing = new RatchetSwing();
            swing.Update(Up, HandAt(10f));
            swing.Update(Up, HandAt(20f));
            var near = swing.Update(Up, HandAt(170f, 0.01f));
            Assert.That(near.HasAngle, Is.False);
            Assert.That(near.DriveDegrees, Is.EqualTo(0f));
            Assert.That(swing.HandleAngleDegrees, Is.EqualTo(20f).Within(0.01f), "handle keeps its last angle");
            var back = swing.Update(Up, HandAt(175f));
            Assert.That(back.DriveDegrees, Is.EqualTo(0f), "re-acquiring the hand does not count as a swing");
        }

        [Test]
        public void HandHeightOffThePlane_DoesNotChangeTheAngle()
        {
            var swing = new RatchetSwing();
            swing.Update(Up, HandAt(0f));
            var step = swing.Update(Up, HandAt(10f, 0.25f, 0.1f));
            Assert.That(step.DriveDegrees, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void BoltSetSpec_DefaultsTheWrench_AndCarriesModuleData()
        {
            var plain = BoltSetTests.CoverBolts();
            Assert.That(plain.Wrench, Is.SameAs(WrenchSpec.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WrenchSpec(detachOffPlaneMetres: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WrenchSpec(attachMaxTiltDegrees: 100f));
        }
    }
}
