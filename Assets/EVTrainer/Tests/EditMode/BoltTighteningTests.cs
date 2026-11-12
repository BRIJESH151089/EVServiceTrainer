using System;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class BoltTighteningTests
    {
        // 3 turns to seat, 8.0 ± 0.4 N·m, 0.1 N·m per degree (spec reached after 80° of wrench swing), max start tilt 10°.
        private static BoltTightening NewBolt() => new BoltTightening(new BoltSpec("bolt_1", PartPose.Identity, 3f, 8f, 0.4f, 0.1f, 10f));

        private static BoltTightening Seated()
        {
            var b = NewBolt();
            b.HandTurn(3 * 360f, 0f);
            Assume.That(b.State, Is.EqualTo(BoltState.Seated));
            return b;
        }

        [Test]
        public void NewBolt_IsLoose()
        {
            var b = NewBolt();
            Assert.That(b.State, Is.EqualTo(BoltState.Loose));
            Assert.That(b.Turns, Is.Zero);
            Assert.That(b.TorqueNm, Is.Zero);
        }

        [Test]
        public void Ratchet_IgnoresBackwardZeroAndNaN()
        {
            var b = NewBolt();
            b.HandTurn(-90f, 0f);
            b.HandTurn(0f, 0f);
            b.HandTurn(float.NaN, 0f);
            Assert.That(b.State, Is.EqualTo(BoltState.Loose));

            b.HandTurn(90f, 0f);
            b.HandTurn(-200f, 0f);
            Assert.That(b.Turns, Is.EqualTo(0.25f).Within(1e-5f), "backward swing does not unscrew");
        }

        [Test]
        public void StraightFirstTwist_Starts_ThenThreadsDown_SeatedAfterThreeTurns()
        {
            var b = NewBolt();

            Assert.That(b.HandTurn(30f, 4f), Is.EqualTo(BoltState.Started));
            b.HandTurn(330f, 4f);
            Assert.That(b.Turns, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(b.SeatProgress01, Is.EqualTo(1f / 3f).Within(1e-4f));
            b.HandTurn(700f, 0f);
            Assert.That(b.State, Is.EqualTo(BoltState.Started), "2.94 turns: not yet");
            Assert.That(b.HandTurn(20f, 0f), Is.EqualTo(BoltState.Seated));
            Assert.That(b.SeatProgress01, Is.EqualTo(1f));
            Assert.That(b.IsSeated, Is.True);
        }

        [Test]
        public void HandCannotTorque_TurnsAfterSeatingAreIgnored()
        {
            var b = Seated();

            b.HandTurn(720f, 0f);

            Assert.That(b.State, Is.EqualTo(BoltState.Seated));
            Assert.That(b.TorqueNm, Is.Zero);
        }

        [Test]
        public void TiltAboveLimitAtFirstTwist_CrossThreads_AndJams()
        {
            var b = NewBolt();

            Assert.That(b.HandTurn(15f, 12f), Is.EqualTo(BoltState.CrossThreaded));
            b.HandTurn(500f, 0f);
            b.ApplyRotation(500f);

            Assert.That(b.State, Is.EqualTo(BoltState.CrossThreaded), "jammed: turning does nothing");
            Assert.That(b.Turns, Is.Zero);
        }

        [Test]
        public void TiltExactlyAtLimit_IsAllowed_AndTiltOnlyMattersAtTheFirstTwist()
        {
            var b = NewBolt();

            Assert.That(b.HandTurn(10f, 10f), Is.EqualTo(BoltState.Started));
            Assert.That(b.HandTurn(10f, 40f), Is.EqualTo(BoltState.Started), "already threaded");
            Assert.That(BoltTightening.IsCrossThreaded(10.01f, 10f), Is.True);
            Assert.That(BoltTightening.IsCrossThreaded(9.99f, 10f), Is.False);
        }

        [Test]
        public void Remove_ClearsCrossThread_AndAStraightRestartWorks()
        {
            var b = NewBolt();
            b.HandTurn(15f, 20f);

            b.Remove();

            Assert.That(b.State, Is.EqualTo(BoltState.Loose));
            Assert.That(b.HandTurn(15f, 2f), Is.EqualTo(BoltState.Started));
        }

        [Test]
        public void Wrench_TorqueRisesByStiffnessPerDegree_TorquedInsideWindow()
        {
            var b = Seated();

            Assert.That(b.ApplyRotation(75f), Is.EqualTo(BoltState.Seated), "7.5 N·m: below 7.6");
            Assert.That(b.TorqueNm, Is.EqualTo(7.5f).Within(1e-4f));
            Assert.That(b.ApplyRotation(1f), Is.EqualTo(BoltState.Torqued), "7.6 N·m: lower limit");
            Assert.That(b.ApplyRotation(7f), Is.EqualTo(BoltState.Torqued), "8.3 N·m: inside the window");
            Assert.That(b.TorqueNm, Is.EqualTo(8.3f).Within(1e-4f));
            Assert.That(b.IsFinished, Is.True);
            Assert.That(b.ApplyRotation(2f), Is.EqualTo(BoltState.OverTorqued), "8.5 N·m: above 8.4");
        }

        [Test]
        public void Wrench_PastUpperLimit_IsOverTorqued_AndStaysOverTorqued()
        {
            var b = Seated();

            Assert.That(b.ApplyRotation(85f), Is.EqualTo(BoltState.OverTorqued));
            float torque = b.TorqueNm;
            b.ApplyRotation(30f);
            b.ApplyRotation(-30f);

            Assert.That(b.State, Is.EqualTo(BoltState.OverTorqued));
            Assert.That(b.TorqueNm, Is.EqualTo(torque), "no further change");
            Assert.That(b.IsFinished, Is.True);
        }

        [Test]
        public void Wrench_BackSwing_IsIgnored()
        {
            var b = Seated();
            b.ApplyRotation(40f);

            b.ApplyRotation(-40f);
            b.ApplyRotation(-40f);

            Assert.That(b.TorqueNm, Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void ApplyRotation_OnStartedBolt_ThreadsDown_ThenLeftoverBecomesTorque()
        {
            var b = NewBolt();
            b.HandTurn(2 * 360f + 340f, 0f); // 20° short of seated

            b.ApplyRotation(60f);            // 20° to seat, 40° of torque

            Assert.That(b.State, Is.EqualTo(BoltState.Seated));
            Assert.That(b.TorqueNm, Is.EqualTo(4f).Within(1e-3f));
        }

        [Test]
        public void ApplyRotation_OnLooseBolt_StartsIt()
        {
            var b = NewBolt();

            Assert.That(b.ApplyRotation(10f), Is.EqualTo(BoltState.Started));
        }

        [Test]
        public void InvalidSpecs_Throw()
        {
            Assert.Throws<ArgumentException>(() => new BoltSpec(" ", PartPose.Identity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoltSpec("b", PartPose.Identity, threadTurnsToSeat: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoltSpec("b", PartPose.Identity, torqueSpecNm: 8f, torqueToleranceNm: 9f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoltSpec("b", PartPose.Identity, stiffnessNmPerDegree: 0f));
            Assert.Throws<ArgumentNullException>(() => new BoltTightening(null));
        }
    }
}
