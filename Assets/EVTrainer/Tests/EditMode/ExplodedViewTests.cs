using System;
using System.Linq;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class ExplodedViewTests
    {
        private static PartSpec Part(string id, string group, string[] prerequisites = null, string type = null, bool pre = false) =>
            new PartSpec(id, id, PartPose.Identity, 0.01f, 5f, prerequisites: prerequisites, preInstalled: pre, typeId: type, groupName: group);

        /// <summary>The battery pack procedure: tray (on the fixture), plate, 2 modules, busbar, BMS, HV connector, cover, cover bolts.</summary>
        private static AssemblyDefinition Pack() => new AssemblyDefinition("pack", "Pack", new[]
        {
            Part("pack_tray", "pack tray", pre: true),
            Part("cooling_plate", "cooling plate", new[] { "pack_tray" }),
            Part("module_1", "battery modules", new[] { "cooling_plate" }, "battery_module"),
            Part("module_2", "battery modules", new[] { "cooling_plate" }, "battery_module"),
            Part("busbar_1", "busbars", new[] { "module_1", "module_2" }, "busbar"),
            Part("bms", "BMS board", new[] { "busbar_1" }),
            Part("hv_connector", "HV connector", new[] { "bms" }),
            Part("cover", "pack cover", new[] { "hv_connector" }),
        }, new[] { BoltSetTests.CoverBolts("cover") });

        private static readonly ExplodeTiming Timing = new ExplodeTiming(moveSeconds: 0.8f, staggerSeconds: 0.1f, stepSeconds: 1f, pauseSeconds: 0.5f);

        [Test]
        public void Disassembly_IsTheProcedureInReverse_BoltsFirst_TrayStays()
        {
            var steps = DisassemblySequence.Build(Pack());

            Assert.That(steps.Select(s => s.Name), Is.EqualTo(new[]
            {
                "Remove cover bolts", "Remove pack cover", "Remove HV connector", "Remove BMS board",
                "Remove busbars", "Remove battery modules", "Remove cooling plate",
            }));
            Assert.That(steps[0].IsBolts, Is.True);
            Assert.That(steps[0].BoltSet.Bolts.Count, Is.EqualTo(8));
            Assert.That(steps[5].Slots.Select(s => s.Id), Is.EqualTo(new[] { "module_1", "module_2" }));
            Assert.That(steps.SelectMany(s => s.Slots).Any(s => s.Id == "pack_tray"), Is.False, "the tray stays on the fixture");
            Assert.That(steps.Select(s => s.Index), Is.EqualTo(Enumerable.Range(0, steps.Count)));
        }

        [Test]
        public void Disassembly_WithoutBolts_StartsWithTheLastPart()
        {
            var def = new AssemblyDefinition("m", "M", new[] { Part("a", "plate"), Part("b", "lid", new[] { "a" }) });
            var steps = DisassemblySequence.Build(def);

            Assert.That(steps.Select(s => s.Name), Is.EqualTo(new[] { "Remove lid", "Remove plate" }));
            Assert.Throws<ArgumentNullException>(() => DisassemblySequence.Build(null));
        }

        [Test]
        public void ExplodedView_TopLayerLeavesFirst_AllOutByTheEnd()
        {
            var t = new ExplodeTimeline(7, Timing);

            Assert.That(t.ExplodeSeconds, Is.EqualTo(0.8f + 6 * 0.1f).Within(1e-5f));
            Assert.That(t.ExplodeAmount(0, 0f), Is.EqualTo(0f), "starts assembled");
            Assert.That(t.ExplodeAmount(0, 0.4f), Is.GreaterThan(t.ExplodeAmount(6, 0.4f)), "bolts lead, plate trails");
            Assert.That(t.ExplodeAmount(6, 0.5f), Is.EqualTo(0f), "the plate has not started yet");
            for (int layer = 0; layer < 7; layer++)
                Assert.That(t.ExplodeAmount(layer, t.ExplodeSeconds), Is.EqualTo(1f), $"layer {layer} fully out");
        }

        [Test]
        public void Assemble_RunsTheOtherWay_BottomLayerFirst()
        {
            var t = new ExplodeTimeline(7, Timing);

            for (int layer = 0; layer < 7; layer++) Assert.That(t.AssembleAmount(layer, 0f), Is.EqualTo(1f), "starts exploded");
            Assert.That(t.AssembleAmount(6, 0.4f), Is.LessThan(t.AssembleAmount(0, 0.4f)), "plate goes back first");
            for (int layer = 0; layer < 7; layer++)
                Assert.That(t.AssembleAmount(layer, t.ExplodeSeconds), Is.EqualTo(0f), $"layer {layer} back in place");
        }

        [Test]
        public void DisassemblyReview_OneStepAtATime_WithAPauseToRead()
        {
            var t = new ExplodeTimeline(3, Timing); // each step: 1 s move + 0.5 s pause

            Assert.That(t.DisassemblySeconds, Is.EqualTo(4.5f).Within(1e-5f));
            Assert.That(t.DisassemblyStepAt(0f), Is.EqualTo(0));
            Assert.That(t.DisassemblyStepAt(1.2f), Is.EqualTo(0), "still reading step 1");
            Assert.That(t.DisassemblyStepAt(1.6f), Is.EqualTo(1));
            Assert.That(t.DisassemblyStepAt(4.5f), Is.EqualTo(3), "finished");
            Assert.That(t.DisassemblyStepAt(99f), Is.EqualTo(3));

            Assert.That(t.DisassemblyAmount(0, 0.5f), Is.EqualTo(0.5f).Within(1e-5f), "half way through its own step");
            Assert.That(t.DisassemblyAmount(1, 0.5f), Is.EqualTo(0f), "next layer waits");
            Assert.That(t.DisassemblyAmount(0, 1.2f), Is.EqualTo(1f), "stays off during the pause");
            Assert.That(t.DisassemblyAmount(1, 2f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Ease_IsSmoothAndClamped()
        {
            Assert.That(ExplodeTimeline.Ease(-1f), Is.EqualTo(0f));
            Assert.That(ExplodeTimeline.Ease(float.NaN), Is.EqualTo(0f));
            Assert.That(ExplodeTimeline.Ease(0.5f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(ExplodeTimeline.Ease(0.1f), Is.LessThan(0.1f), "slow start");
            Assert.That(ExplodeTimeline.Ease(2f), Is.EqualTo(1f));
        }

        [Test]
        public void InvalidTimingOrLayer_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplodeTiming(0f, 0.1f, 1f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplodeTiming(1f, -0.1f, 1f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplodeTimeline(0, Timing));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplodeTimeline(3, Timing).ExplodeAmount(3, 0f));
        }
    }
}
