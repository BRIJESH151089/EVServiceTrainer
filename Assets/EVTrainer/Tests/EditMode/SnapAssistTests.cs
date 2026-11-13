using System;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;
using static EVTrainer.Tests.EditMode.PartTypeSlotTests;

namespace EVTrainer.Tests.EditMode
{
    /// <summary>Magnetic snap assist: per-mode capture zones widen the tolerances; the part snaps to its exact pose.</summary>
    public class SnapAssistTests
    {
        private static readonly SnapAssist Assist = new SnapAssist(new CaptureZone(0.05f, 25f), new CaptureZone(0.03f, 15f));

        private static AssemblyProgress Run(TrainingMode mode)
        {
            var p = new AssemblyProgress(Pack(Assist), mode);
            Assert.That(p.TryInstall("cooling_plate", At(PlatePos)).IsInstalled, Is.True);
            return p;
        }

        private static PartPose Tilted(Vector3 position, float yaw, float pitch)
        {
            var q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw * (float)Math.PI / 180f) *
                    Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch * (float)Math.PI / 180f);
            return new PartPose(position, q);
        }

        [Test]
        public void ZoneIsChosenByMode()
        {
            Assert.That(Run(TrainingMode.Learn).CaptureZone.PositionMetres, Is.EqualTo(0.05f));
            Assert.That(Run(TrainingMode.Test).CaptureZone.AngleDegrees, Is.EqualTo(15f));
            Assert.That(new AssemblyProgress(Pack(), TrainingMode.Learn).CaptureZone.PositionMetres, Is.EqualTo(0f), "no assist by default");
        }

        [Test]
        public void Learn_4cmAnd20degOff_Installs_AndSnapsToTheExactPose()
        {
            var progress = Run(TrainingMode.Learn);
            var released = Tilted(ModulePos(1) + new Vector3(0.03f, 0.02f, 0.015f), 14f, 12f);

            var result = progress.TryInstall("battery_module", released);

            Assert.That(result.IsInstalled, Is.True);
            Assert.That(result.PartId, Is.EqualTo("module_2"));
            Assert.That(result.SnapPose.Position, Is.EqualTo(ModulePos(1)), "pulled into the exact target");
            Assert.That(SnapEvaluator.AngleBetweenDegrees(result.SnapPose.Rotation, Quaternion.Identity), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Test_SameRelease_IsOutOfTolerance_ButInside3cm15deg_Installs()
        {
            var test = Run(TrainingMode.Test);
            Assert.That(test.TryInstall("battery_module", Tilted(ModulePos(1) + new Vector3(0.035f, 0f, 0f), 0f, 0f)).Outcome,
                Is.EqualTo(InstallOutcome.OutOfTolerance), "3.5 cm > 3 cm");
            Assert.That(test.TryInstall("battery_module", Tilted(ModulePos(1), 18f, 0f)).Outcome,
                Is.EqualTo(InstallOutcome.WrongOrientation), "18° > 15°");
            Assert.That(test.TryInstall("battery_module", Tilted(ModulePos(1) + new Vector3(0.02f, 0.015f, 0f), 10f, 5f)).IsInstalled,
                Is.True);
        }

        [Test]
        public void Learn_JustOutsideTheZone_IsNotInstalled()
        {
            var progress = Run(TrainingMode.Learn);
            Assert.That(progress.TryInstall("battery_module", At(ModulePos(0) + new Vector3(0f, 0.051f, 0f))).Outcome,
                Is.EqualTo(InstallOutcome.OutOfTolerance));
            Assert.That(progress.TryInstall("battery_module", At(ModulePos(0), 26f)).Outcome,
                Is.EqualTo(InstallOutcome.WrongOrientation));
        }

        [Test]
        public void ModuleTurned180_IsStillRejected_EvenOnTarget()
        {
            var progress = Run(TrainingMode.Learn);
            var result = progress.TryInstall("battery_module", At(ModulePos(2), 180f));
            Assert.That(result.Outcome, Is.EqualTo(InstallOutcome.WrongOrientation));
            Assert.That(progress.Mistakes.Count, Is.EqualTo(1));
        }

        [Test]
        public void Busbar_KeepsItsHalfTurnSymmetry_InsideTheZone()
        {
            var progress = Run(TrainingMode.Learn);
            progress.TryInstall("battery_module", At(ModulePos(0)));
            progress.TryInstall("battery_module", At(ModulePos(1)));

            var result = progress.TryInstall("busbar", At(BusbarPos(0) + new Vector3(0.02f, 0.01f, 0f), 170f));

            Assert.That(result.IsInstalled, Is.True);
            float toFlipped = SnapEvaluator.AngleBetweenDegrees(result.SnapPose.Rotation,
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Math.PI));
            Assert.That(toFlipped, Is.EqualTo(0f).Within(0.01f), "snaps to the flipped pose, not spun round");
        }

        [Test]
        public void PartsOwnTolerance_StillCounts_WhenWiderThanTheZone()
        {
            var spec = new PartSpec("p", "P", PartPose.Identity, 0.1f, 40f);
            var e = SnapEvaluator.Evaluate(spec, Tilted(new Vector3(0.08f, 0f, 0f), 35f, 0f), new CaptureZone(0.03f, 15f));
            Assert.That(e.IsWithinTolerance, Is.True);
            Assert.That(SnapEvaluator.PositionTolerance(spec, new CaptureZone(0.03f, 15f)), Is.EqualTo(0.1f));
        }

        [Test]
        public void InvalidZones_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureZone(-0.01f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureZone(0.01f, 200f));
        }
    }
}
