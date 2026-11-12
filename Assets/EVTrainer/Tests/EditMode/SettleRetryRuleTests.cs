using System;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class SettleRetryRuleTests
    {
        private static readonly Vector3 Target = new Vector3(0.2f, 0.956f, 0.78f);

        private static PartSpec Plate() => new PartSpec("cooling_plate", "Cooling plate",
            new PartPose(Target, Quaternion.Identity), 0.015f, 8f);

        private static PartPose At(float dx, float dy, float dz) =>
            new PartPose(Target + new Vector3(dx, dy, dz), Quaternion.Identity);

        private static InstallResult Result(InstallOutcome outcome) => new InstallResult(outcome, "cooling_plate", default);

        private static bool Eligible(InstallOutcome outcome, PartPose pose) =>
            SettleRetryRule.IsEligible(Result(outcome), Plate(), pose);

        [Test]
        public void ReleasedAboveTarget_WithinHorizontalTolerance_IsEligible()
        {
            Assert.That(Eligible(InstallOutcome.OutOfTolerance, At(0.01f, 0.04f, 0f)), Is.True);
            Assert.That(Eligible(InstallOutcome.OutOfTolerance, At(0f, 0.06f, 0f)), Is.True, "6 cm is the limit");
        }

        [Test]
        public void TooHighAboveTarget_IsNotEligible()
        {
            Assert.That(Eligible(InstallOutcome.OutOfTolerance, At(0f, 0.065f, 0f)), Is.False);
        }

        [Test]
        public void OutsideHorizontalTolerance_IsNotEligible_EvenAtTargetHeight()
        {
            Assert.That(Eligible(InstallOutcome.OutOfTolerance, At(0.012f, 0.02f, 0.012f)), Is.False, "17 mm diagonal");
            Assert.That(Eligible(InstallOutcome.OutOfTolerance, At(0.03f, 0f, 0f)), Is.False);
        }

        [Test]
        public void WellBelowTarget_IsNotEligible()
        {
            Assert.That(Eligible(InstallOutcome.OutOfTolerance, At(0f, -0.01f, 0f)), Is.True, "within tolerance below");
            Assert.That(Eligible(InstallOutcome.OutOfTolerance, At(0f, -0.03f, 0f)), Is.False);
        }

        [Test]
        public void WrongOrientationAboveTarget_IsEligible()
        {
            Assert.That(Eligible(InstallOutcome.WrongOrientation, At(0f, 0.01f, 0f)), Is.True);
        }

        [Test]
        public void OtherOutcomes_AreNeverEligible()
        {
            var pose = At(0f, 0.02f, 0f);
            Assert.That(Eligible(InstallOutcome.Installed, pose), Is.False);
            Assert.That(Eligible(InstallOutcome.WrongOrder, pose), Is.False);
            Assert.That(Eligible(InstallOutcome.AlreadyInstalled, pose), Is.False);
        }

        [Test]
        public void CustomHeightLimit_IsUsed_AndNegativeLimitThrows()
        {
            var pose = At(0f, 0.08f, 0f);
            Assert.That(SettleRetryRule.IsEligible(Result(InstallOutcome.OutOfTolerance), Plate(), pose, 0.1f), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SettleRetryRule.IsEligible(Result(InstallOutcome.OutOfTolerance), Plate(), pose, -0.01f));
        }

        [Test]
        public void ReleaseAbove_ThenSettledOnTarget_InstallsOnRetry()
        {
            var progress = new AssemblyProgress(new AssemblyDefinition("p", "P", new[] { Plate() }), TrainingMode.Learn);
            var released = At(0.005f, 0.04f, 0f);

            var first = progress.TryInstall("cooling_plate", released);
            Assert.That(first.Outcome, Is.EqualTo(InstallOutcome.OutOfTolerance));
            Assert.That(SettleRetryRule.IsEligible(first, Plate(), released), Is.True);

            var settled = progress.TryInstall("cooling_plate", At(0.005f, 0.0005f, 0f));
            Assert.That(settled.IsInstalled, Is.True);
        }
    }
}
