using System;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class AxisAngleTests
    {
        private static float Rad(float d) => d * (float)Math.PI / 180f;

        [Test]
        public void ClockwiseFromAbove_IsPositive()
        {
            // Unity world: Y up; looking down, Z (forward) -> X (right) is clockwise.
            AxisAngle.TryAngleAbout(Vector3.UnitY, Vector3.UnitZ, out float z);
            AxisAngle.TryAngleAbout(Vector3.UnitY, Vector3.UnitX, out float x);

            Assert.That(AxisAngle.WrapDelta(x - z), Is.EqualTo(90f).Within(1e-3f));
        }

        [Test]
        public void ComponentAlongTheAxis_IsIgnored()
        {
            AxisAngle.TryAngleAbout(Vector3.UnitY, new Vector3(1f, 0f, 0f), out float a);
            AxisAngle.TryAngleAbout(Vector3.UnitY, new Vector3(1f, 5f, 0f), out float b);

            Assert.That(b, Is.EqualTo(a).Within(1e-3f));
        }

        [Test]
        public void DirectionAlongTheAxis_HasNoAngle()
        {
            Assert.That(AxisAngle.TryAngleAbout(Vector3.UnitY, new Vector3(0f, 2f, 0f), out _), Is.False);
            Assert.That(AxisAngle.TryAngleAbout(Vector3.Zero, Vector3.UnitX, out _), Is.False);
        }

        [Test]
        public void WrapDelta_KeepsTheShortWayRound()
        {
            Assert.That(AxisAngle.WrapDelta(350f), Is.EqualTo(-10f).Within(1e-4f));
            Assert.That(AxisAngle.WrapDelta(-350f), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(AxisAngle.WrapDelta(180f), Is.EqualTo(180f).Within(1e-4f));
            Assert.That(AxisAngle.WrapDelta(-180f), Is.EqualTo(180f).Within(1e-4f));
            Assert.That(AxisAngle.WrapDelta(725f), Is.EqualTo(5f).Within(1e-3f));
        }

        [Test]
        public void Tracker_SumsPerFrameDeltas_AcrossTheWrap()
        {
            var t = new AxisRotationTracker();
            float total = 0f;
            // A handle swinging clockwise from -170° through ±180° to +170° (as seen in the plane): 380° in 10° steps.
            for (float a = 0f; a <= 380f; a += 10f)
            {
                // Numerics rotation by +a about Y turns Z toward X: clockwise seen from above in Unity's world.
                var dir = Vector3.Transform(Vector3.UnitZ, Quaternion.CreateFromAxisAngle(Vector3.UnitY, Rad(a)));
                total += t.Update(Vector3.UnitY, dir);
            }

            Assert.That(total, Is.EqualTo(380f).Within(0.05f));
        }

        [Test]
        public void Tracker_FirstUpdateAndAfterResetReturnZero()
        {
            var t = new AxisRotationTracker();
            Assert.That(t.Update(Vector3.UnitY, Vector3.UnitX), Is.Zero);
            t.Reset();
            Assert.That(t.Update(Vector3.UnitY, Vector3.UnitZ), Is.Zero);
        }

        [Test]
        public void TiltedAxis_StillMeasuresRotationAboutIt()
        {
            var axis = Vector3.Normalize(new Vector3(0.3f, 1f, 0.2f));
            var basis = Vector3.Normalize(Vector3.Cross(axis, Vector3.UnitZ));
            var t = new AxisRotationTracker();
            t.Update(axis, basis);
            var rotated = Vector3.Transform(basis, Quaternion.CreateFromAxisAngle(axis, -Rad(30f)));

            Assert.That(Math.Abs(t.Update(axis, rotated)), Is.EqualTo(30f).Within(0.05f));
        }

        [Test]
        public void AngleBetween_Basics()
        {
            Assert.That(AxisAngle.AngleBetween(Vector3.UnitY, Vector3.UnitY), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(AxisAngle.AngleBetween(Vector3.UnitY, Vector3.UnitX), Is.EqualTo(90f).Within(1e-3f));
            Assert.That(AxisAngle.AngleBetween(Vector3.UnitY, Vector3.Zero), Is.Zero);
        }
    }
}
