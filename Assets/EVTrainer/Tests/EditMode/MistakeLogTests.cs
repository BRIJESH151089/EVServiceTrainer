using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class MistakeLogTests
    {
        private static readonly Vector3 PlatePos = new Vector3(0f, 0.016f, 0f);
        private static Vector3 ModulePos(int i) => new Vector3(-0.21f + 0.14f * i, 0.06f, 0f);

        private static PartPose At(Vector3 p, float yawDegrees = 0f) =>
            new PartPose(p, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawDegrees * (float)System.Math.PI / 180f));

        private static AssemblyProgress NewProgress(TrainingMode mode = TrainingMode.Learn)
        {
            var parts = new System.Collections.Generic.List<PartSpec>
            {
                new PartSpec("pack_tray", "Pack tray", PartPose.Identity, 0.02f, 10f, preInstalled: true),
                new PartSpec("cooling_plate", "Cooling plate", At(PlatePos), 0.015f, 8f,
                    prerequisites: new[] { "pack_tray" }, groupName: "cooling plate"),
            };
            for (int i = 0; i < 4; i++)
                parts.Add(new PartSpec($"module_{i + 1}", $"Battery module {i + 1}", At(ModulePos(i)), 0.012f, 8f,
                    prerequisites: new[] { "cooling_plate" }, typeId: "battery_module", groupName: "battery modules"));
            return new AssemblyProgress(new AssemblyDefinition("p", "P", parts), mode);
        }

        [Test]
        public void EachFailureKind_IsLoggedWithTimeSlotItemAndReason()
        {
            var progress = NewProgress();

            progress.TryInstall("battery_module", At(ModulePos(0)), 1.5, "Module A");          // before plate
            progress.TryInstall("cooling_plate", At(PlatePos + new Vector3(0.05f, 0f, 0f)), 4.0); // not seated
            progress.TryInstall("cooling_plate", At(PlatePos, 180f), 6.25);                     // wrong way round

            var e = progress.Mistakes.Entries;
            Assert.That(progress.Mistakes.Count, Is.EqualTo(3));
            Assert.That(e[0].Kind, Is.EqualTo(MistakeKind.WrongOrder));
            Assert.That(e[0].TimeSeconds, Is.EqualTo(1.5));
            Assert.That(e[0].SlotId, Is.EqualTo("module_1"));
            Assert.That(e[0].ItemId, Is.EqualTo("Module A"));
            Assert.That(e[0].TypeId, Is.EqualTo("battery_module"));
            Assert.That(e[0].Reason, Is.EqualTo("Cooling plate must be fitted before the battery modules."));
            Assert.That(e[1].Kind, Is.EqualTo(MistakeKind.NotSeated));
            Assert.That(e[1].ItemId, Is.EqualTo("cooling_plate"), "item defaults to the type id");
            Assert.That(e[2].Kind, Is.EqualTo(MistakeKind.WrongOrientation));
            Assert.That(e[2].TimeSeconds, Is.EqualTo(6.25));
        }

        [Test]
        public void SuccessesAndAlreadyInstalled_AreNotMistakes()
        {
            var progress = NewProgress();

            progress.TryInstall("cooling_plate", At(PlatePos), 1);
            progress.TryInstall("cooling_plate", At(PlatePos), 2);
            progress.TryInstall("pack_tray", PartPose.Identity, 3);

            Assert.That(progress.Mistakes.Count, Is.Zero);
        }

        [Test]
        public void Evaluate_IsNeverLogged()
        {
            var progress = NewProgress();

            progress.Evaluate("battery_module", At(ModulePos(0)));

            Assert.That(progress.Mistakes.Count, Is.Zero);
        }

        [Test]
        public void Counts_ByKindAndType()
        {
            var progress = NewProgress();
            progress.TryInstall("battery_module", At(ModulePos(0)), 1, "A");
            progress.TryInstall("battery_module", At(ModulePos(1)), 2, "B");
            progress.TryInstall("cooling_plate", At(PlatePos, 180f), 3);

            Assert.That(progress.Mistakes.CountOf(MistakeKind.WrongOrder), Is.EqualTo(2));
            Assert.That(progress.Mistakes.CountOf(MistakeKind.WrongOrientation), Is.EqualTo(1));
            Assert.That(progress.Mistakes.CountOf(MistakeKind.NotSeated), Is.Zero);
            Assert.That(progress.Mistakes.CountForType("battery_module"), Is.EqualTo(2));
        }

        [Test]
        public void FirstTimeFit_IsTrackedPerPhysicalPart()
        {
            var progress = NewProgress();
            progress.TryInstall("cooling_plate", At(PlatePos), 10);

            progress.TryInstall("battery_module", At(ModulePos(0), 180f), 11, "Module A"); // A: wrong way round
            progress.TryInstall("battery_module", At(ModulePos(0)), 12, "Module A");       // A: second attempt
            progress.TryInstall("battery_module", At(ModulePos(1)), 13, "Module B");       // B: first time

            var r = progress.InstallRecords;
            Assert.That(r.Count, Is.EqualTo(3), "pre-installed tray is not a record");
            Assert.That(r[0].SlotId, Is.EqualTo("cooling_plate"));
            Assert.That(r[0].IsFirstTimeFit, Is.True);
            Assert.That(r[1].ItemId, Is.EqualTo("Module A"));
            Assert.That(r[1].SlotId, Is.EqualTo("module_1"));
            Assert.That(r[1].Attempts, Is.EqualTo(2));
            Assert.That(r[1].IsFirstTimeFit, Is.False);
            Assert.That(r[1].TimeSeconds, Is.EqualTo(12));
            Assert.That(r[2].ItemId, Is.EqualTo("Module B"));
            Assert.That(r[2].IsFirstTimeFit, Is.True);
            Assert.That(progress.FirstTimeFitCount, Is.EqualTo(2));
        }

        [Test]
        public void LoggingIsTheSameInLearnAndTest()
        {
            foreach (var mode in new[] { TrainingMode.Learn, TrainingMode.Test })
            {
                var progress = NewProgress(mode);
                progress.TryInstall("battery_module", At(ModulePos(0)), 1, "A");
                Assert.That(progress.Mistakes.Count, Is.EqualTo(1), mode.ToString());
            }
        }

        [Test]
        public void TryGetKind_MapsOnlyFailures()
        {
            Assert.That(MistakeLog.TryGetKind(InstallOutcome.OutOfTolerance, out var k) && k == MistakeKind.NotSeated, Is.True);
            Assert.That(MistakeLog.TryGetKind(InstallOutcome.Installed, out _), Is.False);
            Assert.That(MistakeLog.TryGetKind(InstallOutcome.AlreadyInstalled, out _), Is.False);
        }
    }
}
