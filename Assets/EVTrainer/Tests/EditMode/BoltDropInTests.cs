using System;
using System.Collections.Generic;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    /// <summary>Releasing a bolt over a hole drops it in (Started or cross-threaded); hand twists are geared.</summary>
    public class BoltDropInTests
    {
        private static readonly CaptureZone Learn = new CaptureZone(0.05f, 25f);
        private static readonly CaptureZone Test = new CaptureZone(0.03f, 15f);

        private static BoltSet NewSet(MistakeLog log, float gearing = 4f)
        {
            var spec = BoltSetTests.CoverBolts();
            var geared = new List<BoltSpec>();
            foreach (var b in spec.Bolts)
                geared.Add(new BoltSpec(b.HoleId, b.HolePose, 3f, 8f, 0.4f, 0.1f, 10f, gearing));
            return new BoltSet(new BoltSetSpec(spec.Id, spec.PrerequisitePartId, geared, spec.PatternOrder), true, log);
        }

        private static Vector3 Hole(BoltSet set, string id) => set.Get(id).Spec.HolePose.Position;

        private static Vector3 TiltedAxis(float degrees) =>
            Vector3.Transform(Vector3.UnitY, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, degrees * (float)Math.PI / 180f));

        [Test]
        public void FindDropHole_UsesTheModesCaptureZone()
        {
            var set = NewSet(new MistakeLog());
            var near = Hole(set, "h3") + new Vector3(0.02f, 0.03f, 0f); // 3.6 cm away

            Assert.That(set.FindDropHole(near, Vector3.UnitY, Learn), Is.EqualTo("h3"));
            Assert.That(set.FindDropHole(near, Vector3.UnitY, Test), Is.Null, "3.6 cm > Test's 3 cm");
            Assert.That(set.FindDropHole(Hole(set, "h3") + new Vector3(0, 0.02f, 0), Vector3.UnitY, Test), Is.EqualTo("h3"));
        }

        [Test]
        public void FindDropHole_TooCrooked_OrOccupied_IsNull()
        {
            var set = NewSet(new MistakeLog());
            var above = Hole(set, "h6") + new Vector3(0f, 0.01f, 0f);

            Assert.That(set.FindDropHole(above, TiltedAxis(20f), Learn), Is.EqualTo("h6"), "20° is inside Learn's 25°");
            Assert.That(set.FindDropHole(above, TiltedAxis(20f), Test), Is.Null, "20° is outside Test's 15°");
            Assert.That(set.FindDropHole(above, TiltedAxis(30f), Learn), Is.Null);
            set.DropIn("h6", 0f, 0);
            Assert.That(set.FindDropHole(above, Vector3.UnitY, Learn), Is.Null, "occupied");
        }

        [Test]
        public void StraightDrop_IsStarted_WithNoTurns()
        {
            var log = new MistakeLog();
            var set = NewSet(log);

            Assert.That(set.DropIn("h1", 4f, 1.0), Is.EqualTo(BoltState.Started));
            Assert.That(set.Get("h1").Turns, Is.EqualTo(0f));
            Assert.That(set.IsOccupied("h1"), Is.True);
            Assert.That(log.Count, Is.EqualTo(0));
        }

        [Test]
        public void TiltedDrop_IsCrossThreaded_Logged_AndPullingOutClearsIt()
        {
            var log = new MistakeLog();
            var set = NewSet(log);

            Assert.That(set.DropIn("h2", 12f, 2.0), Is.EqualTo(BoltState.CrossThreaded));
            Assert.That(log.Count, Is.EqualTo(1));
            Assert.That(log.Entries[0].Kind, Is.EqualTo(MistakeKind.CrossThread));
            Assert.That(set.HandTurn("h2", 360f, 0f, 3.0), Is.EqualTo(BoltState.CrossThreaded), "jammed: twisting does nothing");

            set.Remove("h2");
            Assert.That(set.DropIn("h2", 2f, 4.0), Is.EqualTo(BoltState.Started));
            Assert.That(log.Count, Is.EqualTo(1));
        }

        [Test]
        public void Gearing4_SeatsThreeTurns_InAbout270DegreesOfWrist()
        {
            var set = NewSet(new MistakeLog());
            set.DropIn("h4", 0f, 0);

            for (int i = 0; i < 26; i++) set.HandTurn("h4", 10f, 0f, 0); // 260° of wrist
            Assert.That(set.Get("h4").State, Is.EqualTo(BoltState.Started));
            Assert.That(set.Get("h4").Turns, Is.EqualTo(260f * 4f / 360f).Within(0.001f));

            set.HandTurn("h4", 10f, 0f, 0);
            Assert.That(set.Get("h4").State, Is.EqualTo(BoltState.Seated));
        }

        [Test]
        public void DropIn_OnlyAffectsALooseBolt_AndGearingMustBePositive()
        {
            var t = new BoltTightening(new BoltSpec("b", PartPose.Identity));
            Assert.That(t.DropIn(0f), Is.EqualTo(BoltState.Started));
            Assert.That(t.DropIn(30f), Is.EqualTo(BoltState.Started), "already in: unchanged");
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoltSpec("b", PartPose.Identity, handTurnGearing: 0f));
        }
    }
}
