using System;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class HandSmoothingTests
    {
        private const float Dt = 1f / 72f;
        private static readonly OneEuroSettings Pos = new OneEuroSettings(1.5f, 4f);
        private static readonly OneEuroSettings Rot = new OneEuroSettings(1.5f, 0.6f);

        private static float Deg(Quaternion a, Quaternion b) => SnapEvaluator.AngleBetweenDegrees(a, b);
        private static Quaternion Yaw(float deg) => TwoHandCarry.YawRotation(deg);

        [Test]
        public void HandHeldStill_TremorIsReducedSeveralTimes()
        {
            var f = new OneEuroPoseFilter(Pos, Rot);
            var rng = new Random(7);
            float rawSum = 0f, outSum = 0f;
            var centre = new Vector3(0.2f, 1.1f, 0.5f);
            for (int i = 0; i < 400; i++)
            {
                var noise = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.006f;
                f.Update(centre + noise, Yaw((float)(rng.NextDouble() - 0.5) * 3f), Dt);
                if (i < 100) continue;
                rawSum += noise.Length();
                outSum += (f.Position - centre).Length();
            }
            Assert.That(outSum, Is.LessThan(rawSum / 3f), "6 mm hand tremor is cut to under a third");
            Assert.That(Deg(f.Rotation, Quaternion.Identity), Is.LessThan(1f), "±1.5° rotation tremor stays well under 1°");
        }

        [Test]
        public void FastMove_KeepsUp_WithSmallLag()
        {
            var f = new OneEuroPoseFilter(Pos, Rot);
            var p = Vector3.Zero;
            for (int i = 0; i < 72; i++) // 1 m/s for a second
            {
                p += new Vector3(1f, 0f, 0f) * Dt;
                f.Update(p, Quaternion.Identity, Dt);
            }
            Assert.That(p.X - f.Position.X, Is.LessThan(0.04f), "less than 4 cm behind at 1 m/s");
        }

        [Test]
        public void StepThenRest_Converges_AndResetPassesThrough()
        {
            var f = new OneEuroPoseFilter(Pos, Rot);
            f.Update(Vector3.Zero, Quaternion.Identity, Dt);
            for (int i = 0; i < 300; i++) f.Update(new Vector3(0f, 0.1f, 0f), Yaw(40f), Dt);
            Assert.That((f.Position - new Vector3(0f, 0.1f, 0f)).Length(), Is.LessThan(0.001f));
            Assert.That(Deg(f.Rotation, Yaw(40f)), Is.LessThan(0.5f));

            f.Reset();
            f.Update(new Vector3(5f, 5f, 5f), Yaw(-90f), Dt);
            Assert.That(f.Position, Is.EqualTo(new Vector3(5f, 5f, 5f)));
        }

        [Test]
        public void Settings_Validate()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OneEuroSettings(0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OneEuroSettings(1f, -1f));
        }

        [Test]
        public void Debounce_GrabsAtOnce_ReleasesOnlyAfterStayingOpen()
        {
            var d = new ReleaseDebounce(0.1f);
            Assert.That(d.Update(true, 0.0), Is.True);
            Assert.That(d.Update(false, 0.02), Is.True, "open 0 ms");
            Assert.That(d.Update(false, 0.08), Is.True, "open 60 ms");
            Assert.That(d.Update(true, 0.09), Is.True, "closed again: the flicker is ignored");
            Assert.That(d.Update(false, 0.10), Is.True);
            Assert.That(d.Update(false, 0.19), Is.True, "open 90 ms");
            Assert.That(d.Update(false, 0.20), Is.False, "open 100 ms: released");
            Assert.That(d.Update(false, 0.5), Is.False);
            d.Update(true, 0.6);
            d.Reset();
            Assert.That(d.IsPressed, Is.False);
        }

        [Test]
        public void TwoHands_MidpointCarriesThePart_AndItStaysLevel()
        {
            var carry = new TwoHandCarry();
            var left = new Vector3(-0.3f, 1.0f, 0.5f);
            var right = new Vector3(0.3f, 1.0f, 0.5f);
            var part = new Vector3(0f, 0.95f, 0.55f);
            carry.Begin(part, 0f, left, right);

            carry.Pose(left + new Vector3(0.1f, 0.2f, 0f), right + new Vector3(0.1f, 0.2f, 0f), out var p, out float yaw);
            Assert.That((p - (part + new Vector3(0.1f, 0.2f, 0f))).Length(), Is.LessThan(1e-5f));
            Assert.That(yaw, Is.EqualTo(0f).Within(1e-3f));

            // One hand higher than the other: no roll, the part only rises by the midpoint.
            carry.Pose(left, right + new Vector3(0f, 0.2f, 0f), out p, out yaw);
            Assert.That(yaw, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(p.Y, Is.EqualTo(1.05f).Within(1e-4f));
        }

        [Test]
        public void TwoHands_TurningTheLineBetweenThem_TurnsThePartAboutTheMidpoint()
        {
            var carry = new TwoHandCarry();
            var left = new Vector3(-0.3f, 1f, 0f);
            var right = new Vector3(0.3f, 1f, 0f);
            carry.Begin(new Vector3(0f, 1f, 0.1f), 10f, left, right);

            // Rotate both hands 90° clockwise (seen from above) about the midpoint.
            carry.Pose(new Vector3(0f, 1f, 0.3f), new Vector3(0f, 1f, -0.3f), out var p, out float yaw);
            Assert.That(yaw, Is.EqualTo(100f).Within(1e-3f), "part yaw follows the hands, keeping its 10° offset");
            Assert.That(p.X, Is.EqualTo(0.1f).Within(1e-4f), "the 10 cm forward offset turned with the hands");
            Assert.That(p.Z, Is.EqualTo(0f).Within(1e-4f));
        }
    }
}
