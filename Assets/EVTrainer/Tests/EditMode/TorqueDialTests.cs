using System;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class TorqueDialTests
    {
        private static readonly BoltSpec Spec = new BoltSpec("b", PartPose.Identity, 3f, 8f, 0.4f, 0.1f, 10f);

        [Test]
        public void Dial_FillsWithTorque_AndClamps()
        {
            float full = TorqueDial.FullScaleNm(Spec); // 8.4 x 1.25 = 10.5
            Assert.That(full, Is.EqualTo(10.5f).Within(1e-4f));
            Assert.That(TorqueDial.Fill01(Spec, 0f), Is.EqualTo(0f));
            Assert.That(TorqueDial.Fill01(Spec, 5.25f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(TorqueDial.Fill01(Spec, 20f), Is.EqualTo(1f));
            Assert.That(TorqueDial.Fill01(Spec, -1f), Is.EqualTo(0f));
            Assert.That(TorqueDial.Fill01(Spec, float.NaN), Is.EqualTo(0f));
        }

        [Test]
        public void Dial_ZoneMarksTheSpecWindow()
        {
            var (start, end) = TorqueDial.Zone01(Spec);
            Assert.That(start, Is.EqualTo(7.6f / 10.5f).Within(1e-4f));
            Assert.That(end, Is.EqualTo(8.4f / 10.5f).Within(1e-4f));
            Assert.That(TorqueDial.Fill01(Spec, 8f), Is.InRange(start, end), "spec torque fills into the zone");
            Assert.Throws<ArgumentOutOfRangeException>(() => TorqueDial.FullScaleNm(Spec, 0.5f));
        }

    }
}
