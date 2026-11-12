using System;
using System.Collections.Generic;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class BoltSetTests
    {
        private static readonly string[] Cross = { "h1", "h5", "h3", "h7", "h2", "h6", "h4", "h8" };

        // 8 holes on a 0.6 x 0.4 m cover at y = 0.14, numbered around the perimeter.
        private static readonly Vector3[] HolePositions =
        {
            new Vector3(-0.29f, 0.14f, -0.19f), new Vector3(0f, 0.14f, -0.19f), new Vector3(0.29f, 0.14f, -0.19f),
            new Vector3(0.29f, 0.14f, 0f), new Vector3(0.29f, 0.14f, 0.19f), new Vector3(0f, 0.14f, 0.19f),
            new Vector3(-0.29f, 0.14f, 0.19f), new Vector3(-0.29f, 0.14f, 0f),
        };

        internal static BoltSetSpec CoverBolts(string prerequisite = "cover")
        {
            var bolts = new List<BoltSpec>();
            for (int i = 0; i < 8; i++)
                bolts.Add(new BoltSpec($"h{i + 1}", new PartPose(HolePositions[i], Quaternion.Identity), 3f, 8f, 0.4f, 0.1f, 10f));
            return new BoltSetSpec("cover_bolts", prerequisite, bolts, Cross,
                "Hand-start cover bolts", "Torque bolts in cross pattern", "cover bolts");
        }

        private static BoltSet NewSet(bool learn, MistakeLog log) => new BoltSet(CoverBolts(), blockOutOfOrder: learn, log);

        private static void SeatAll(BoltSet set)
        {
            foreach (var b in set.Bolts)
            {
                set.Insert(b.Spec.HoleId);
                set.HandTurn(b.Spec.HoleId, 1080f, 0f, 0);
            }
        }

        [Test]
        public void FindFreeHole_FindsNearestWithinRadius_AboveTheHole()
        {
            var set = NewSet(true, new MistakeLog());
            var aboveH4 = HolePositions[3] + new Vector3(0.012f, 0.05f, 0.005f);

            Assert.That(set.FindFreeHole(aboveH4, 0.02f, 0.08f), Is.EqualTo("h4"));
            Assert.That(set.FindFreeHole(aboveH4, 0.01f, 0.08f), Is.Null, "13 mm off-axis > 10 mm radius");
            Assert.That(set.FindFreeHole(HolePositions[3] + new Vector3(0, 0.2f, 0), 0.02f, 0.08f), Is.Null, "too high");
            set.Insert("h4");
            Assert.That(set.FindFreeHole(aboveH4, 0.02f, 0.08f), Is.Null, "occupied hole is skipped");
            Assert.That(set.IsOccupied("h4"), Is.True);
        }

        [Test]
        public void Remove_FreesTheHole_AndResetsTheBolt()
        {
            var set = NewSet(true, new MistakeLog());
            set.Insert("h2");
            set.HandTurn("h2", 400f, 0f, 0);

            set.Remove("h2");

            Assert.That(set.IsOccupied("h2"), Is.False);
            Assert.That(set.Get("h2").State, Is.EqualTo(BoltState.Loose));
        }

        [Test]
        public void CrossThread_IsLoggedOnce()
        {
            var log = new MistakeLog();
            var set = NewSet(true, log);
            set.Insert("h1");

            set.HandTurn("h1", 20f, 15f, 3.0);
            set.HandTurn("h1", 20f, 15f, 3.5);

            Assert.That(log.CountOf(MistakeKind.CrossThread), Is.EqualTo(1));
            Assert.That(log.Entries[0].SlotId, Is.EqualTo("h1"));
            Assert.That(log.Entries[0].TypeId, Is.EqualTo(BoltSet.MistakeTypeId));
            Assert.That(log.Entries[0].TimeSeconds, Is.EqualTo(3.0));
        }

        [Test]
        public void Wrench_OnUnseatedBolt_IsRefused_NotLogged()
        {
            var log = new MistakeLog();
            var set = NewSet(true, log);
            set.Insert("h1");
            set.HandTurn("h1", 100f, 0f, 0);

            var r = set.EngageWrench("h1", 1);

            Assert.That(r.Check, Is.EqualTo(WrenchCheck.BoltNotSeated));
            Assert.That(r.Allowed, Is.False);
            Assert.That(log.Count, Is.Zero);
        }

        [Test]
        public void Wrench_BeforeAllSeated_IsBlocked_AndLogged_InBothModes()
        {
            foreach (bool learn in new[] { true, false })
            {
                var log = new MistakeLog();
                var set = NewSet(learn, log);
                set.Insert("h1");
                set.HandTurn("h1", 1080f, 0f, 0);

                var r = set.EngageWrench("h1", 2);

                Assert.That(r.Check, Is.EqualTo(WrenchCheck.NotAllSeated), learn ? "Learn" : "Test");
                Assert.That(r.Allowed, Is.False);
                Assert.That(r.Reason, Is.EqualTo("Hand-start all bolts first (1/8 seated)."));
                Assert.That(log.CountOf(MistakeKind.TorqueBeforeAllSeated), Is.EqualTo(1));
            }
        }

        [Test]
        public void Wrench_OnNextBoltInPattern_IsOk()
        {
            var set = NewSet(true, new MistakeLog());
            SeatAll(set);

            Assert.That(set.NextInPattern, Is.EqualTo("h1"));
            Assert.That(set.EngageWrench("h1", 0).Check, Is.EqualTo(WrenchCheck.Ok));
        }

        [Test]
        public void Wrench_OutOfOrder_Learn_IsBlockedWithReason_AndLogged()
        {
            var log = new MistakeLog();
            var set = NewSet(true, log);
            SeatAll(set);

            var r = set.EngageWrench("h2", 5);

            Assert.That(r.Check, Is.EqualTo(WrenchCheck.OutOfOrderBlocked));
            Assert.That(r.Allowed, Is.False);
            Assert.That(r.Reason, Is.EqualTo("Follow the cross pattern: bolt 1 is next (this is bolt 5)."));
            Assert.That(log.CountOf(MistakeKind.WrongSequence), Is.EqualTo(1));
        }

        [Test]
        public void Wrench_OutOfOrder_Test_IsAllowed_ButLogged()
        {
            var log = new MistakeLog();
            var set = NewSet(false, log);
            SeatAll(set);

            var r = set.EngageWrench("h2", 5);

            Assert.That(r.Check, Is.EqualTo(WrenchCheck.OutOfOrderAllowed));
            Assert.That(r.Allowed, Is.True);
            Assert.That(log.CountOf(MistakeKind.WrongSequence), Is.EqualTo(1));
        }

        [Test]
        public void WrenchTurn_TorquesInWindow_RecordsOrder_AdvancesPattern()
        {
            var set = NewSet(true, new MistakeLog());
            SeatAll(set);

            Assert.That(set.WrenchTurn("h1", 80f, 0), Is.EqualTo(BoltState.Torqued));

            Assert.That(set.TorqueOrder, Is.EqualTo(new[] { "h1" }));
            Assert.That(set.NextInPattern, Is.EqualTo("h5"));
            Assert.That(set.FinishedCount, Is.EqualTo(1));
        }

        [Test]
        public void WrenchTurn_OverTorque_IsLoggedOnce_AndCountsAsFinished()
        {
            var log = new MistakeLog();
            var set = NewSet(true, log);
            SeatAll(set);

            set.WrenchTurn("h1", 90f, 7);
            set.WrenchTurn("h1", 10f, 8);

            Assert.That(set.Get("h1").State, Is.EqualTo(BoltState.OverTorqued));
            Assert.That(log.CountOf(MistakeKind.OverTorque), Is.EqualTo(1));
            Assert.That(set.FinishedCount, Is.EqualTo(1));
        }

        [Test]
        public void WrenchTurn_IgnoresUnseatedBolts()
        {
            var set = NewSet(true, new MistakeLog());
            set.Insert("h1");
            set.HandTurn("h1", 100f, 0f, 0);

            Assert.That(set.WrenchTurn("h1", 500f, 0), Is.EqualTo(BoltState.Started));
        }

        [Test]
        public void TorquingAllInCrossPattern_Completes_AndEvaluatesOk()
        {
            var set = NewSet(true, new MistakeLog());
            SeatAll(set);

            foreach (var h in Cross)
            {
                Assert.That(set.EngageWrench(h, 0).Check, Is.EqualTo(WrenchCheck.Ok), h);
                set.WrenchTurn(h, 80f, 0);
            }

            Assert.That(set.IsComplete, Is.True);
            Assert.That(set.Pattern.Evaluate(set.TorqueOrder), Is.EqualTo(PatternCheck.Ok));
        }

        [Test]
        public void BoltSetSpec_Validates_PatternAndHoles()
        {
            var bolts = new[] { new BoltSpec("a", PartPose.Identity), new BoltSpec("b", PartPose.Identity) };
            Assert.Throws<ArgumentException>(() => new BoltSetSpec("s", null, bolts, new[] { "a" }));
            Assert.Throws<ArgumentException>(() => new BoltSetSpec("s", null, bolts, new[] { "a", "a" }));
            Assert.Throws<ArgumentException>(() => new BoltSetSpec("s", null, bolts, new[] { "a", "c" }));
            Assert.Throws<ArgumentException>(() => new BoltSetSpec("s", null, new[] { bolts[0], bolts[0] }, new[] { "a", "a" }));
            Assert.Throws<ArgumentException>(() => new BoltSetSpec("s", null, new BoltSpec[0], new string[0]));
        }
    }
}
