using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    /// <summary>Learn takes the wrench off a finished bolt by itself; Test leaves it on so over-torque stays possible.</summary>
    public class WrenchModeTests
    {
        private static AssemblyProgress Run(TrainingMode mode)
        {
            var def = new AssemblyDefinition("pack", "Pack", new[]
            {
                new PartSpec("cover", "Pack cover", PartPose.Identity, 0.015f, 8f, preInstalled: true),
            }, new[] { BoltSetTests.CoverBolts("cover") });
            return new AssemblyProgress(def, mode);
        }

        [Test]
        public void Learn_AutoDetaches_Test_DoesNot()
        {
            Assert.That(Run(TrainingMode.Learn).BoltSets[0].AutoDetachWhenFinished, Is.True);
            Assert.That(Run(TrainingMode.Test).BoltSets[0].AutoDetachWhenFinished, Is.False);
        }

        [Test]
        public void Test_SwingingOnPastTheWindow_OverTorques_AndIsLoggedOnce()
        {
            var p = Run(TrainingMode.Test);
            var set = p.BoltSets[0];
            foreach (var b in set.Bolts) { set.Insert(b.Spec.HoleId); set.HandTurn(b.Spec.HoleId, 1080f, 0f, 0); }
            Assert.That(set.EngageWrench("h1", 1.0).Allowed, Is.True);
            Assert.That(set.WrenchTurn("h1", 78f, 2.0), Is.EqualTo(BoltState.Torqued), "click at spec");
            Assert.That(set.WrenchTurn("h1", 10f, 2.5), Is.EqualTo(BoltState.OverTorqued), "kept swinging past the window");
            set.WrenchTurn("h1", 10f, 3.0);
            Assert.That(p.Mistakes.CountOf(MistakeKind.OverTorque), Is.EqualTo(1));
            Assert.That(set.Get("h1").IsFinished, Is.True);
        }
    }
}
