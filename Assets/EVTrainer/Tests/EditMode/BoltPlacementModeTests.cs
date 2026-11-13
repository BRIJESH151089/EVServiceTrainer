using System;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    /// <summary>Bolt drop-in per mode: Learn 6 cm / 30° and straightens crooked bolts; Test 3 cm / 15° and cross-threads.</summary>
    public class BoltPlacementModeTests
    {
        private static readonly SnapAssist PartAssist = new SnapAssist(new CaptureZone(0.05f, 25f), new CaptureZone(0.03f, 15f));
        private static readonly SnapAssist BoltAssist = new SnapAssist(new CaptureZone(0.06f, 30f), new CaptureZone(0.03f, 15f));

        private static AssemblyProgress Run(TrainingMode mode, SnapAssist boltCapture = null, bool straighten = true)
        {
            var b = BoltSetTests.CoverBolts("cover");
            var set = new BoltSetSpec(b.Id, b.PrerequisitePartId, b.Bolts, b.PatternOrder, capture: boltCapture ?? BoltAssist,
                straightenInLearn: straighten);
            var def = new AssemblyDefinition("pack", "Pack", new[]
            {
                new PartSpec("cover", "Pack cover", PartPose.Identity, 0.015f, 8f),
            }, new[] { set }, PartAssist);
            return new AssemblyProgress(def, mode);
        }

        private static Vector3 Hole(BoltSet set, string id) => set.Get(id).Spec.HolePose.Position;

        private static Vector3 Tilted(float degrees) =>
            Vector3.Transform(Vector3.UnitY, Quaternion.CreateFromAxisAngle(Vector3.UnitX, degrees * (float)Math.PI / 180f));

        [Test]
        public void BoltZone_ComesFromTheBoltSet_PerMode_ElseTheAssembly()
        {
            var learn = Run(TrainingMode.Learn);
            Assert.That(learn.BoltCaptureZone(learn.BoltSets[0]).PositionMetres, Is.EqualTo(0.06f));
            Assert.That(learn.BoltCaptureZone(learn.BoltSets[0]).AngleDegrees, Is.EqualTo(30f));
            var test = Run(TrainingMode.Test);
            Assert.That(test.BoltCaptureZone(test.BoltSets[0]).PositionMetres, Is.EqualTo(0.03f));

            var fallback = new AssemblyProgress(new AssemblyDefinition("p", "P",
                new[] { new PartSpec("cover", "C", PartPose.Identity, 0.01f, 5f) },
                new[] { BoltSetTests.CoverBolts("cover") }, PartAssist), TrainingMode.Learn);
            Assert.That(fallback.BoltCaptureZone(fallback.BoltSets[0]).PositionMetres, Is.EqualTo(0.05f));
        }

        [Test]
        public void Learn_5cmAway_And25degTilted_IsCaptured_AndStraightened_NotLogged()
        {
            var p = Run(TrainingMode.Learn);
            var set = p.BoltSets[0];
            var tip = Hole(set, "h2") + new Vector3(0.03f, 0.04f, 0f); // 5 cm

            var hole = set.FindDropHole(tip, Tilted(25f), p.BoltCaptureZone(set));
            Assert.That(hole, Is.EqualTo("h2"));
            Assert.That(set.WouldCrossThread(hole, 25f), Is.True, "Learn still knows it would cross-thread (tip label)");
            Assert.That(set.DropIn(hole, 25f, 1.0), Is.EqualTo(BoltState.Started));
            Assert.That(p.Mistakes.Count, Is.EqualTo(0));
        }

        [Test]
        public void Learn_Over30deg_OrOver6cm_IsNotCaptured()
        {
            var p = Run(TrainingMode.Learn);
            var set = p.BoltSets[0];
            var zone = p.BoltCaptureZone(set);
            Assert.That(set.FindDropHole(Hole(set, "h3") + new Vector3(0, 0.02f, 0), Tilted(32f), zone), Is.Null);
            Assert.That(set.FindDropHole(Hole(set, "h3") + new Vector3(0.05f, 0.04f, 0), Vector3.UnitY, zone), Is.Null, "6.4 cm");
        }

        [Test]
        public void Test_KeepsTheStrictRule_CrossThreadsBetween10And15deg()
        {
            var p = Run(TrainingMode.Test);
            var set = p.BoltSets[0];
            var zone = p.BoltCaptureZone(set);
            var above = Hole(set, "h4") + new Vector3(0f, 0.02f, 0f);

            Assert.That(set.StraightensCrooked, Is.False);
            Assert.That(set.FindDropHole(above, Tilted(20f), zone), Is.Null, "20° > 15°");
            Assert.That(set.FindDropHole(Hole(set, "h4") + new Vector3(0.035f, 0f, 0f), Vector3.UnitY, zone), Is.Null, "3.5 cm > 3 cm");
            Assert.That(set.FindDropHole(above, Tilted(12f), zone), Is.EqualTo("h4"));
            Assert.That(set.DropIn("h4", 12f, 2.0), Is.EqualTo(BoltState.CrossThreaded));
            Assert.That(p.Mistakes.CountOf(MistakeKind.CrossThread), Is.EqualTo(1));
            Assert.That(set.DropIn("h5", 8f, 3.0), Is.EqualTo(BoltState.Started));
        }

        [Test]
        public void Learn_WithoutStraightening_InTheData_CrossThreadsLikeTest()
        {
            var p = Run(TrainingMode.Learn, straighten: false);
            var set = p.BoltSets[0];
            Assert.That(set.StraightensCrooked, Is.False);
            Assert.That(set.DropIn("h1", 20f, 0), Is.EqualTo(BoltState.CrossThreaded));
        }
    }
}
