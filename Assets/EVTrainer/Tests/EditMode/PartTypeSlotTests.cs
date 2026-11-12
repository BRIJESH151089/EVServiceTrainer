using System;
using System.Collections.Generic;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    /// <summary>Interchangeable parts (types vs slots), symmetry and per-slot prerequisites on a mini battery pack.</summary>
    public class PartTypeSlotTests
    {
        private const float Spacing = 0.14f;

        private static float Rad(float degrees) => degrees * (float)Math.PI / 180f;

        private static Vector3 ModulePos(int i) => new Vector3(-0.21f + Spacing * i, 0.06f, 0f);
        private static Vector3 BusbarPos(int i) => new Vector3(-0.14f + Spacing * i, 0.116f, -0.08f);
        private static readonly Vector3 PlatePos = new Vector3(0f, 0.016f, 0f);
        private static readonly Vector3 BmsPos = new Vector3(0f, 0.106f, 0.06f);

        private static PartPose At(Vector3 p, float yawDegrees = 0f) =>
            new PartPose(p, Quaternion.CreateFromAxisAngle(Vector3.UnitY, Rad(yawDegrees)));

        private static AssemblyDefinition Pack()
        {
            var parts = new List<PartSpec>
            {
                new PartSpec("pack_tray", "Pack tray", PartPose.Identity, 0.02f, 10f, preInstalled: true, groupName: "pack tray"),
                new PartSpec("cooling_plate", "Cooling plate", At(PlatePos), 0.015f, 8f,
                    prerequisites: new[] { "pack_tray" }, groupName: "cooling plate"),
            };
            for (int i = 0; i < 4; i++)
                parts.Add(new PartSpec($"module_{i + 1}", $"Battery module {i + 1}", At(ModulePos(i)), 0.012f, 8f,
                    prerequisites: new[] { "cooling_plate" }, instruction: "Install battery modules",
                    typeId: "battery_module", groupName: "battery modules"));
            for (int i = 0; i < 3; i++)
                parts.Add(new PartSpec($"busbar_{i + 1}", $"Busbar {i + 1}", At(BusbarPos(i)), 0.008f, 6f,
                    prerequisites: new[] { $"module_{i + 1}", $"module_{i + 2}" }, instruction: "Connect busbars",
                    typeId: "busbar", groupName: "busbars", symmetry: PartSymmetry.HalfTurnAboutUp));
            parts.Add(new PartSpec("bms", "BMS board", At(BmsPos), 0.01f, 6f,
                prerequisites: new[] { "busbar_1", "busbar_2", "busbar_3" }, groupName: "BMS board"));
            return new AssemblyDefinition("battery_pack", "EV Battery Pack", parts);
        }

        private static AssemblyProgress NewProgress() => new AssemblyProgress(Pack(), TrainingMode.Learn);

        private static AssemblyProgress WithModules(params int[] slots)
        {
            var p = NewProgress();
            p.TryInstall("cooling_plate", At(PlatePos));
            foreach (int s in slots) Assert.That(p.TryInstall("battery_module", At(ModulePos(s))).IsInstalled, Is.True);
            return p;
        }

        [Test]
        public void IdenticalPart_InstallsIntoTheSlotItIsPlacedIn()
        {
            var progress = WithModules();

            var result = progress.TryInstall("battery_module", At(ModulePos(2) + new Vector3(0.005f, 0f, 0f)));

            Assert.That(result.IsInstalled, Is.True);
            Assert.That(result.PartId, Is.EqualTo("module_3"));
            Assert.That(result.TypeId, Is.EqualTo("battery_module"));
            Assert.That(progress.IsInstalled("module_3"), Is.True);
            Assert.That(progress.IsInstalled("module_1"), Is.False);
        }

        [Test]
        public void IdenticalParts_FillAnyFreeSlots_InAnyOrder()
        {
            var progress = WithModules(3, 0, 2, 1);

            Assert.That(progress.InstalledPartIds, Is.EqualTo(new[] { "pack_tray", "cooling_plate", "module_4", "module_1", "module_3", "module_2" }));
        }

        [Test]
        public void InstalledSlotIsSkipped_PartIsJudgedAgainstNearestFreeSlot()
        {
            var progress = WithModules(0);

            // Dropped exactly on the occupied slot 1: judged against free slot 2 (next nearest) and out of tolerance.
            var result = progress.TryInstall("battery_module", At(ModulePos(0)));

            Assert.That(result.Outcome, Is.EqualTo(InstallOutcome.OutOfTolerance));
            Assert.That(result.PartId, Is.EqualTo("module_2"));
            Assert.That(result.Evaluation.PositionErrorMetres, Is.EqualTo(Spacing).Within(1e-4f));
        }

        [Test]
        public void AllSlotsOfTypeInstalled_IsAlreadyInstalled()
        {
            var progress = WithModules(0, 1, 2, 3);

            Assert.That(progress.TryInstall("battery_module", At(ModulePos(1))).Outcome, Is.EqualTo(InstallOutcome.AlreadyInstalled));
        }

        [Test]
        public void SlotWithinTolerance_WinsOverNearerSlotOutOfTolerance()
        {
            // Two overlapping slots: A is nearer but the part is rotated 90° from it; B fits.
            var a = new PartSpec("a", "A", At(Vector3.Zero, 90f), 0.02f, 5f, typeId: "t");
            var b = new PartSpec("b", "B", At(new Vector3(0.012f, 0f, 0f)), 0.02f, 5f, typeId: "t");
            var progress = new AssemblyProgress(new AssemblyDefinition("x", "X", new[] { a, b }), TrainingMode.Learn);

            var result = progress.TryInstall("t", At(new Vector3(0.004f, 0f, 0f)));

            Assert.That(result.IsInstalled, Is.True);
            Assert.That(result.PartId, Is.EqualTo("b"));
        }

        [Test]
        public void Module_Rotated180_IsWrongOrientation()
        {
            var progress = WithModules();

            var result = progress.TryInstall("battery_module", At(ModulePos(1), 180f));

            Assert.That(result.Outcome, Is.EqualTo(InstallOutcome.WrongOrientation));
            Assert.That(result.PartId, Is.EqualTo("module_2"));
            Assert.That(result.Evaluation.AngleErrorDegrees, Is.EqualTo(180f).Within(0.01f));
        }

        [Test]
        public void Busbar_Flipped180AboutUp_StillFits_AndSnapsToFlippedPose()
        {
            var progress = WithModules(0, 1);

            var result = progress.TryInstall("busbar", At(BusbarPos(0), 178f));

            Assert.That(result.IsInstalled, Is.True);
            Assert.That(result.PartId, Is.EqualTo("busbar_1"));
            Assert.That(result.Evaluation.AngleErrorDegrees, Is.EqualTo(2f).Within(0.05f));
            float snapToFlipped = SnapEvaluator.AngleBetweenDegrees(result.SnapPose.Rotation,
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, Rad(180f)));
            Assert.That(snapToFlipped, Is.LessThan(0.05f), "snaps to the equivalent rotation, no 180° spin");
            Assert.That(result.SnapPose.Position, Is.EqualTo(BusbarPos(0)));
        }

        [Test]
        public void Busbar_Rotated90_IsWrongOrientation_SymmetryIsOnlyHalfTurn()
        {
            var progress = WithModules(0, 1);

            Assert.That(progress.TryInstall("busbar", At(BusbarPos(0), 90f)).Outcome, Is.EqualTo(InstallOutcome.WrongOrientation));
        }

        [Test]
        public void Busbar_Tipped180AboutHorizontal_IsWrongOrientation()
        {
            var progress = WithModules(0, 1);
            var upsideDown = new PartPose(BusbarPos(0), Quaternion.CreateFromAxisAngle(Vector3.UnitX, Rad(180f)));

            Assert.That(progress.TryInstall("busbar", upsideDown).Outcome, Is.EqualTo(InstallOutcome.WrongOrientation));
        }

        [Test]
        public void BusbarSlot_NeedsBothNeighbouringModules()
        {
            var progress = WithModules(0); // module 1 only

            var result = progress.TryInstall("busbar", At(BusbarPos(0)));

            Assert.That(result.Outcome, Is.EqualTo(InstallOutcome.WrongOrder));
            Assert.That(result.MissingPrerequisites, Is.EqualTo(new[] { "module_2" }));
            Assert.That(result.Reason, Is.EqualTo("Battery modules must be fitted before the busbars."));
        }

        [Test]
        public void BusbarSlot_AvailableAsSoonAsItsTwoModulesAreIn()
        {
            var progress = WithModules(2, 3);

            Assert.That(progress.IsAvailable("busbar_3"), Is.True);
            Assert.That(progress.IsAvailable("busbar_1"), Is.False);
            Assert.That(progress.TryInstall("busbar", At(BusbarPos(2))).IsInstalled, Is.True);
        }

        [Test]
        public void ModuleBeforeCoolingPlate_IsWrongOrder_WithHumanReason()
        {
            var progress = NewProgress();

            var result = progress.TryInstall("battery_module", At(ModulePos(0)));

            Assert.That(result.Outcome, Is.EqualTo(InstallOutcome.WrongOrder));
            Assert.That(result.Reason, Is.EqualTo("Cooling plate must be fitted before the battery modules."));
            Assert.That(progress.IsInstalled("module_1"), Is.False);
        }

        [Test]
        public void BmsBeforeBusbars_ReasonNamesTheGroupOnce()
        {
            var progress = WithModules(0, 1, 2, 3);
            progress.TryInstall("busbar", At(BusbarPos(1)));

            var result = progress.TryInstall("bms", At(BmsPos));

            Assert.That(result.MissingPrerequisites, Is.EqualTo(new[] { "busbar_1", "busbar_3" }));
            Assert.That(result.Reason, Is.EqualTo("Busbars must be fitted before the BMS board."));
        }

        [Test]
        public void Reason_ListsSeveralMissingGroups()
        {
            var a = new PartSpec("a", "A", PartPose.Identity, 0.01f, 5f, groupName: "cooling plate");
            var b = new PartSpec("b", "B", PartPose.Identity, 0.01f, 5f, groupName: "BMS board");
            var c = new PartSpec("c", "C", PartPose.Identity, 0.01f, 5f, prerequisites: new[] { "a", "b" }, groupName: "pack cover");
            var progress = new AssemblyProgress(new AssemblyDefinition("x", "X", new[] { a, b, c }), TrainingMode.Learn);

            Assert.That(progress.TryInstall("c", PartPose.Identity).Reason,
                Is.EqualTo("Cooling plate and BMS board must be fitted before the pack cover."));
        }

        [Test]
        public void Evaluate_DoesNotChangeProgress()
        {
            var progress = WithModules();

            var result = progress.Evaluate("battery_module", At(ModulePos(0)));

            Assert.That(result.IsInstalled, Is.True);
            Assert.That(progress.IsInstalled("module_1"), Is.False);
        }

        [Test]
        public void Steps_GroupConsecutiveSlotsOfOneType_AndCountProgress()
        {
            var progress = WithModules(0, 2);
            var steps = progress.Definition.Steps;

            Assert.That(steps.Count, Is.EqualTo(5));
            Assert.That(steps[2].Instruction, Is.EqualTo("Install battery modules"));
            Assert.That(steps[2].Slots.Count, Is.EqualTo(4));
            Assert.That(progress.InstalledCount(steps[2]), Is.EqualTo(2));
            Assert.That(progress.CurrentStep, Is.SameAs(steps[2]));
        }

        [Test]
        public void SlotsOfType_ReturnsSlotsInOrder_OrEmpty()
        {
            var def = Pack();

            Assert.That(def.SlotsOfType("busbar").Count, Is.EqualTo(3));
            Assert.That(def.SlotsOfType("busbar")[0].Id, Is.EqualTo("busbar_1"));
            Assert.That(def.SlotsOfType("nope"), Is.Empty);
            Assert.Throws<ArgumentException>(() => NewProgress().TryInstall("nope", PartPose.Identity));
        }
    }
}
