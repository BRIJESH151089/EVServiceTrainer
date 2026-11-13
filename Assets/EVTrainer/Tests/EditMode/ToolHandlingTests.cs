using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class ToolHandlingTests
    {
        [Test]
        public void Wrench_LetGoOnAnUnfinishedBolt_ParksThere()
        {
            Assert.That(ToolReleaseRule.ForWrench(true, false, 1.1f, 0.92f, 0.6f, 1f), Is.EqualTo(ToolRelease.ParkOnBolt));
            Assert.That(ToolReleaseRule.ForWrench(true, false, 0.5f, 0.92f, 3f, 1f), Is.EqualTo(ToolRelease.ParkOnBolt),
                "parking wins even if the hand is low or far");
        }

        [Test]
        public void Wrench_LetGoInReach_Floats_LowOrFar_GoesHome()
        {
            Assert.That(ToolReleaseRule.ForWrench(false, false, 1.1f, 0.92f, 0.7f, 1f), Is.EqualTo(ToolRelease.FloatInPlace));
            Assert.That(ToolReleaseRule.ForWrench(true, true, 1.1f, 0.92f, 0.7f, 1f), Is.EqualTo(ToolRelease.FloatInPlace),
                "on a finished bolt it is just let go in the air");
            Assert.That(ToolReleaseRule.ForWrench(false, false, 0.85f, 0.92f, 0.7f, 1f), Is.EqualTo(ToolRelease.ReturnHome), "below the bench top");
            Assert.That(ToolReleaseRule.ForWrench(false, false, 1.2f, 0.92f, 1.2f, 1f), Is.EqualTo(ToolRelease.ReturnHome), "more than 1 m away");
            Assert.That(ToolReleaseRule.ForWrench(false, false, float.NaN, 0.92f, 0.5f, 1f), Is.EqualTo(ToolRelease.ReturnHome));
        }

        [Test]
        public void Bolt_LooseGoesBackToTheTray_InAHoleItStays()
        {
            Assert.That(ToolReleaseRule.ForBolt(true), Is.EqualTo(BoltRelease.StayInHole));
            Assert.That(ToolReleaseRule.ForBolt(false), Is.EqualTo(BoltRelease.ReturnToTray));
        }
    }
}
