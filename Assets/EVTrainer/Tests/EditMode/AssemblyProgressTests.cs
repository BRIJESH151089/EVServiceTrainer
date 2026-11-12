using System;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class AssemblyProgressTests
    {
        private const float PosTol = 0.015f; // 1.5 cm
        private const float AngleTol = 8f;   // degrees

        private static readonly Vector3 PlatePosition = new Vector3(0f, 0.95f, 0.8f);
        private static readonly PartPose PlateTarget = new PartPose(PlatePosition, Quaternion.Identity);

        private static float Rad(float degrees) => degrees * (float)Math.PI / 180f;

        /// <summary>Tray (pre-installed) → cooling plate → module (needs plate) ; bms (needs plate and module).</summary>
        private static AssemblyDefinition Pack() => new AssemblyDefinition("battery_pack", "EV Battery Pack", new[]
        {
            new PartSpec("pack_tray", "Pack tray", PartPose.Identity, PosTol, AngleTol, preInstalled: true),
            new PartSpec("cooling_plate", "Cooling plate", PlateTarget, PosTol, AngleTol,
                prerequisites: new[] { "pack_tray" }, instruction: "Fit cooling plate"),
            new PartSpec("module_1", "Battery module 1", PlateTarget, PosTol, AngleTol,
                prerequisites: new[] { "cooling_plate" }),
            new PartSpec("bms", "BMS board", PlateTarget, PosTol, AngleTol,
                prerequisites: new[] { "cooling_plate", "module_1" }),
        });

        private static AssemblyProgress NewProgress() => new AssemblyProgress(Pack(), TrainingMode.Learn);

        private static PartPose Moved(float metres) =>
            new PartPose(PlatePosition + new Vector3(metres, 0f, 0f), Quaternion.Identity);

        private static PartPose Yawed(float degrees) =>
            new PartPose(PlatePosition, Quaternion.CreateFromAxisAngle(Vector3.UnitY, Rad(degrees)));

        [Test]
        public void PreInstalledParts_StartInstalled_AndModeIsStored()
        {
            var progress = new AssemblyProgress(Pack(), TrainingMode.Test);

            Assert.That(progress.Mode, Is.EqualTo(TrainingMode.Test));
            Assert.That(progress.IsInstalled("pack_tray"), Is.True);
            Assert.That(progress.InstalledPartIds, Is.EqualTo(new[] { "pack_tray" }));
            Assert.That(progress.CurrentStepIndex, Is.EqualTo(1));
            Assert.That(progress.CurrentPart.Id, Is.EqualTo("cooling_plate"));
            Assert.That(progress.IsComplete, Is.False);
        }

        [Test]
        public void AvailableParts_AreThoseWithAllPrerequisitesInstalled()
        {
            var progress = NewProgress();

            Assert.That(progress.IsAvailable("cooling_plate"), Is.True);
            Assert.That(progress.IsAvailable("module_1"), Is.False);
            Assert.That(progress.IsAvailable("pack_tray"), Is.False, "installed parts are not available");
            Assert.That(progress.AvailableParts.Count, Is.EqualTo(1));
            Assert.That(progress.AvailableParts[0].Id, Is.EqualTo("cooling_plate"));
        }

        [Test]
        public void ExactPose_Installs_AndAdvancesStep()
        {
            var progress = NewProgress();

            var result = progress.TryInstall("cooling_plate", PlateTarget);

            Assert.That(result.Outcome, Is.EqualTo(InstallOutcome.Installed));
            Assert.That(result.IsInstalled, Is.True);
            Assert.That(result.PartId, Is.EqualTo("cooling_plate"));
            Assert.That(progress.IsInstalled("cooling_plate"), Is.True);
            Assert.That(progress.CurrentPart.Id, Is.EqualTo("module_1"));
            Assert.That(progress.IsAvailable("module_1"), Is.True);
        }

        [Test]
        public void SmallErrorsInsideTolerance_Install()
        {
            var progress = NewProgress();
            var pose = new PartPose(PlatePosition + new Vector3(0.01f, 0f, 0f),
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, Rad(7f)));

            Assert.That(progress.TryInstall("cooling_plate", pose).Outcome, Is.EqualTo(InstallOutcome.Installed));
        }

        [Test]
        public void PositionOff_IsOutOfTolerance_AndNotInstalled()
        {
            var progress = NewProgress();

            var result = progress.TryInstall("cooling_plate", Moved(0.03f));

            Assert.That(result.Outcome, Is.EqualTo(InstallOutcome.OutOfTolerance));
            Assert.That(result.Evaluation.PositionErrorMetres, Is.EqualTo(0.03f).Within(1e-4f));
            Assert.That(progress.IsInstalled("cooling_plate"), Is.False);
            Assert.That(progress.CurrentPart.Id, Is.EqualTo("cooling_plate"));
        }

        [Test]
        public void PositionAndAngleOff_IsOutOfTolerance_NotWrongOrientation()
        {
            var progress = NewProgress();
            var pose = new PartPose(PlatePosition + new Vector3(0.05f, 0f, 0f),
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, Rad(90f)));

            Assert.That(progress.TryInstall("cooling_plate", pose).Outcome, Is.EqualTo(InstallOutcome.OutOfTolerance));
        }

        [Test]
        public void PositionOk_AngleOff_IsWrongOrientation()
        {
            var progress = NewProgress();

            var result = progress.TryInstall("cooling_plate", Yawed(180f));

            Assert.That(result.Outcome, Is.EqualTo(InstallOutcome.WrongOrientation));
            Assert.That(result.Evaluation.AngleErrorDegrees, Is.EqualTo(180f).Within(0.01f));
            Assert.That(result.Evaluation.IsPositionWithinTolerance, Is.True);
            Assert.That(progress.IsInstalled("cooling_plate"), Is.False);
        }

        [Test]
        public void AngleJustOutsideTolerance_IsWrongOrientation()
        {
            var progress = NewProgress();

            Assert.That(progress.TryInstall("cooling_plate", Yawed(8.5f)).Outcome, Is.EqualTo(InstallOutcome.WrongOrientation));
        }

        [Test]
        public void MissingPrerequisite_IsWrongOrder_ListingMissingIds_EvenAtExactPose()
        {
            var progress = NewProgress();

            var result = progress.TryInstall("bms", PlateTarget);

            Assert.That(result.Outcome, Is.EqualTo(InstallOutcome.WrongOrder));
            Assert.That(result.MissingPrerequisites, Is.EqualTo(new[] { "cooling_plate", "module_1" }));
            Assert.That(progress.IsInstalled("bms"), Is.False);
        }

        [Test]
        public void WrongOrder_ListsOnlyPrerequisitesStillMissing()
        {
            var progress = NewProgress();
            progress.TryInstall("cooling_plate", PlateTarget);

            var result = progress.TryInstall("bms", PlateTarget);

            Assert.That(result.MissingPrerequisites, Is.EqualTo(new[] { "module_1" }));
        }

        [Test]
        public void ReInstall_IsAlreadyInstalled_AndChangesNothing()
        {
            var progress = NewProgress();
            progress.TryInstall("cooling_plate", PlateTarget);

            var again = progress.TryInstall("cooling_plate", Moved(0.5f));
            var tray = progress.TryInstall("pack_tray", PartPose.Identity);

            Assert.That(again.Outcome, Is.EqualTo(InstallOutcome.AlreadyInstalled));
            Assert.That(tray.Outcome, Is.EqualTo(InstallOutcome.AlreadyInstalled));
            Assert.That(progress.InstalledPartIds, Is.EqualTo(new[] { "pack_tray", "cooling_plate" }));
            Assert.That(again.MissingPrerequisites, Is.Empty);
        }

        [Test]
        public void FailedAttempt_ThenCorrectAttempt_Installs()
        {
            var progress = NewProgress();
            progress.TryInstall("cooling_plate", Yawed(180f));

            Assert.That(progress.TryInstall("cooling_plate", PlateTarget).IsInstalled, Is.True);
        }

        [Test]
        public void InstallingEveryPartInOrder_Completes()
        {
            var progress = NewProgress();

            progress.TryInstall("cooling_plate", PlateTarget);
            progress.TryInstall("module_1", PlateTarget);
            Assert.That(progress.IsComplete, Is.False);
            progress.TryInstall("bms", PlateTarget);

            Assert.That(progress.IsComplete, Is.True);
            Assert.That(progress.CurrentPart, Is.Null);
            Assert.That(progress.CurrentStepIndex, Is.EqualTo(4));
            Assert.That(progress.AvailableParts, Is.Empty);
            Assert.That(progress.InstalledPartIds, Is.EqualTo(new[] { "pack_tray", "cooling_plate", "module_1", "bms" }));
        }

        [Test]
        public void UnknownPartId_Throws()
        {
            var progress = NewProgress();

            Assert.Throws<ArgumentException>(() => progress.TryInstall("nope", PlateTarget));
            Assert.Throws<ArgumentException>(() => progress.IsAvailable(null));
        }

        [Test]
        public void PartNameAndHint_DefaultToDisplayNameAndEmpty_OrComeFromData()
        {
            var plain = new PartSpec("x", "Battery module 2", PartPose.Identity, 0.01f, 5f);
            var described = new PartSpec("y", "Busbar 1", PartPose.Identity, 0.01f, 5f,
                partName: "Busbar", hint: "Links - of one module to + of the next");

            Assert.That(plain.PartName, Is.EqualTo("Battery module 2"));
            Assert.That(plain.Hint, Is.Empty);
            Assert.That(described.PartName, Is.EqualTo("Busbar"));
            Assert.That(described.Hint, Is.EqualTo("Links - of one module to + of the next"));
        }

        [Test]
        public void Instruction_DefaultsToFitDisplayName()
        {
            var def = Pack();
            def.TryGetPart("cooling_plate", out var plate);
            def.TryGetPart("module_1", out var module);

            Assert.That(plate.Instruction, Is.EqualTo("Fit cooling plate"));
            Assert.That(module.Instruction, Is.EqualTo("Fit Battery module 1"));
        }
    }
}
