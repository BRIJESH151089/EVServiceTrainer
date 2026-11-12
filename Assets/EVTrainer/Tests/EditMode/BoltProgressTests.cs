using System;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    /// <summary>Bolt sets inside an assembly: steps, availability after the cover, completion.</summary>
    public class BoltProgressTests
    {
        private static readonly PartPose CoverTarget = new PartPose(new Vector3(0f, 0.135f, 0f), Quaternion.Identity);

        private static AssemblyDefinition Pack() => new AssemblyDefinition("pack", "Pack", new[]
        {
            new PartSpec("tray", "Tray", PartPose.Identity, 0.02f, 10f, preInstalled: true),
            new PartSpec("cover", "Pack cover", CoverTarget, 0.015f, 8f, prerequisites: new[] { "tray" }),
        }, new[] { BoltSetTests.CoverBolts("cover") });

        private static void SeatAll(BoltSet set)
        {
            foreach (var b in set.Bolts)
            {
                set.Insert(b.Spec.HoleId);
                set.HandTurn(b.Spec.HoleId, 1080f, 0f, 0);
            }
        }

        [Test]
        public void BoltSet_AddsHandStartAndTorqueSteps_AfterTheParts()
        {
            var steps = Pack().Steps;

            Assert.That(steps.Count, Is.EqualTo(4));
            Assert.That(steps[2].Kind, Is.EqualTo(StepKind.HandStartBolts));
            Assert.That(steps[2].Instruction, Is.EqualTo("Hand-start cover bolts"));
            Assert.That(steps[2].Size, Is.EqualTo(8));
            Assert.That(steps[3].Kind, Is.EqualTo(StepKind.TorqueBolts));
            Assert.That(steps[3].Slots, Is.Empty);
            Assert.That(steps[0].IsPreInstalled, Is.True);
            Assert.That(steps[1].IsPreInstalled, Is.False);
            Assert.That(steps[2].IsPreInstalled, Is.False);
        }

        [Test]
        public void BoltsAreAvailable_OnlyAfterTheirPartIsInstalled()
        {
            var p = new AssemblyProgress(Pack(), TrainingMode.Learn);
            var set = p.BoltSets[0];

            Assert.That(p.AreBoltsAvailable(set), Is.False);
            p.TryInstall("cover", CoverTarget);
            Assert.That(p.AreBoltsAvailable(set), Is.True);
        }

        [Test]
        public void StepCounts_FollowSeatedAndFinishedBolts_AndCompletionNeedsAllTorqued()
        {
            var p = new AssemblyProgress(Pack(), TrainingMode.Learn);
            p.TryInstall("cover", CoverTarget);
            var set = p.BoltSets[0];
            var handStart = p.Definition.Steps[2];
            var torque = p.Definition.Steps[3];

            Assert.That(p.CurrentStep, Is.SameAs(handStart));
            Assert.That(p.IsComplete, Is.False, "all parts in, bolts not done");

            SeatAll(set);
            Assert.That(p.CompletedCount(handStart), Is.EqualTo(8));
            Assert.That(p.CurrentStep, Is.SameAs(torque));

            foreach (var h in set.Spec.PatternOrder)
            {
                set.EngageWrench(h, 0);
                set.WrenchTurn(h, 80f, 0);
            }
            Assert.That(p.CompletedCount(torque), Is.EqualTo(8));
            Assert.That(p.CurrentStep, Is.Null);
            Assert.That(p.IsComplete, Is.True);
        }

        [Test]
        public void OverTorquedBolts_StillCompleteTheModule_ButAreLogged()
        {
            var p = new AssemblyProgress(Pack(), TrainingMode.Test);
            p.TryInstall("cover", CoverTarget);
            var set = p.BoltSets[0];
            SeatAll(set);

            foreach (var h in set.Spec.PatternOrder) set.WrenchTurn(h, 100f, 0);

            Assert.That(p.IsComplete, Is.True);
            Assert.That(p.Mistakes.CountOf(MistakeKind.OverTorque), Is.EqualTo(8));
        }

        [Test]
        public void Mode_DecidesWhetherOutOfOrderIsBlocked()
        {
            Assert.That(new AssemblyProgress(Pack(), TrainingMode.Learn).BoltSets[0].BlockOutOfOrder, Is.True);
            Assert.That(new AssemblyProgress(Pack(), TrainingMode.Test).BoltSets[0].BlockOutOfOrder, Is.False);
        }

        [Test]
        public void UnknownPrerequisite_OrDuplicateSet_Throws()
        {
            var parts = new[] { new PartSpec("tray", "Tray", PartPose.Identity, 0.02f, 10f, preInstalled: true) };
            Assert.Throws<ArgumentException>(() => new AssemblyDefinition("x", "X", parts, new[] { BoltSetTests.CoverBolts("cover") }));
            Assert.Throws<ArgumentException>(() => new AssemblyDefinition("x", "X", parts,
                new[] { BoltSetTests.CoverBolts(null), BoltSetTests.CoverBolts(null) }));
        }

        [Test]
        public void GetBoltSet_UnknownId_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AssemblyProgress(Pack(), TrainingMode.Learn).GetBoltSet("nope"));
        }
    }
}
