using System;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class YawFollowerTests
    {
        [Test]
        public void FirstUpdate_SnapsToTheTarget()
        {
            var f = new YawFollower();
            Assert.That(f.Update(30f, 0.016f), Is.EqualTo(30f));
            Assert.That(f.HasYaw, Is.True);
        }

        [Test]
        public void TrackingNoise_InsideTheDeadZone_DoesNotMoveIt()
        {
            var f = new YawFollower(4f, 0.18f);
            f.Snap(10f);
            var rng = new Random(1);
            for (int i = 0; i < 200; i++) f.Update(10f + (float)(rng.NextDouble() * 6 - 3), 0.014f);
            Assert.That(f.Yaw, Is.EqualTo(10f), "±3° of head noise never moves the label");
        }

        [Test]
        public void RealTurn_EasesAllTheWayIn_WithoutOvershoot()
        {
            var f = new YawFollower(4f, 0.18f);
            f.Snap(0f);
            float previous = 0f;
            for (int i = 0; i < 120; i++)
            {
                float y = f.Update(40f, 1f / 72f);
                Assert.That(y, Is.GreaterThanOrEqualTo(previous - 1e-4f));
                Assert.That(y, Is.LessThanOrEqualTo(40f + 1e-4f));
                previous = y;
            }
            Assert.That(f.Yaw, Is.EqualTo(40f).Within(0.3f));
        }

        [Test]
        public void TurnsTheShortWayAcross180()
        {
            var f = new YawFollower(1f, 0.1f);
            f.Snap(170f);
            for (int i = 0; i < 100; i++) f.Update(-170f, 0.02f);
            Assert.That(Math.Abs(AxisAngle.WrapDelta(f.Yaw - (-170f))), Is.LessThan(0.5f));
        }

        [Test]
        public void InvalidSettings_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new YawFollower(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new YawFollower(2f, 0f));
        }
    }
}
