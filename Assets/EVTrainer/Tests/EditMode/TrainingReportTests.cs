using System;
using System.Linq;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    /// <summary>Scoring and the completion report, from runs driven through the real Core assembly and bolt rules.</summary>
    public class TrainingReportTests
    {
        private static readonly ScoringRules Rules = ScoringRules.Default; // 100 start, pass 80, target 360 s, ±1/min capped at 5
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.FromHours(5.5));

        private static PartSpec Part(string id, string name, string[] prerequisites = null, string type = null, bool pre = false) =>
            new PartSpec(id, id, PartPose.Identity, 0.01f, 5f, prerequisites: prerequisites, preInstalled: pre, typeId: type,
                partName: name, instruction: pre ? null : "Fit " + name.ToLowerInvariant());

        private static AssemblyDefinition Pack() => new AssemblyDefinition("battery_pack", "EV Battery Pack Assembly", new[]
        {
            Part("pack_tray", "Pack tray", pre: true),
            Part("cooling_plate", "Cooling plate", new[] { "pack_tray" }),
            Part("module_1", "Battery module", new[] { "cooling_plate" }, "battery_module"),
            Part("module_2", "Battery module", new[] { "cooling_plate" }, "battery_module"),
            Part("cover", "Pack cover", new[] { "module_1", "module_2" }),
        }, new[] { BoltSetTests.CoverBolts("cover") });

        private static readonly PartPose Fit = PartPose.Identity;

        /// <summary>A complete run: parts fitted, bolts seated then torqued to spec in the cross pattern. <paramref name="extra"/>
        /// injects mistakes at the right moment ("parts" before the first fit, "bolts" before seating, "torque" before torquing).</summary>
        private static AssemblyProgress Run(TrainingMode mode, Action<AssemblyProgress, string> extra = null)
        {
            var p = new AssemblyProgress(Pack(), mode);
            extra?.Invoke(p, "parts");
            p.TryInstall("cooling_plate", Fit, 30, "Cooling Plate");
            p.TryInstall("battery_module", Fit, 60, "Battery Module 1");
            p.TryInstall("battery_module", Fit, 80, "Battery Module 2");
            p.TryInstall("cover", Fit, 120, "Pack Cover");
            var set = p.BoltSets[0];
            extra?.Invoke(p, "bolts");
            double t = 130;
            foreach (var b in set.Bolts)
            {
                if (set.Get(b.Spec.HoleId).IsSeated) continue;
                set.Insert(b.Spec.HoleId);
                set.HandTurn(b.Spec.HoleId, 1080f, 0f, t += 10);
            }
            extra?.Invoke(p, "torque");
            foreach (var hole in set.Spec.PatternOrder)
            {
                if (set.Get(hole).IsFinished) continue;
                set.EngageWrench(hole, t += 5);
                set.WrenchTurn(hole, 80f, t += 5);
            }
            Assert.That(p.IsComplete, Is.True, "the driven run completes");
            return p;
        }

        private static TrainingReport Report(AssemblyProgress p, double seconds = 360) =>
            TrainingReportBuilder.Build(p, Rules, "session-1", Start, Start.AddSeconds(seconds), seconds);

        // ---------------- Score ----------------

        [Test]
        public void PerfectTestRun_Scores100_AndPasses()
        {
            var s = ScoreCalculator.Calculate(TrainingMode.Test, Run(TrainingMode.Test).Mistakes, null, 360, Rules);
            Assert.That(s.Score, Is.EqualTo(100));
            Assert.That(s.Result, Is.EqualTo(RunResult.Pass));
            Assert.That(s.CriticalCount, Is.EqualTo(0));
        }

        [TestCase(MistakeKind.WrongOrder, 10)]
        [TestCase(MistakeKind.WrongOrientation, 5)]
        [TestCase(MistakeKind.NotSeated, 2)]
        [TestCase(MistakeKind.CrossThread, 10)]
        [TestCase(MistakeKind.OverTorque, 10)]
        [TestCase(MistakeKind.WrongSequence, 5)]
        [TestCase(MistakeKind.TorqueBeforeAllSeated, 5)]
        public void EachMistake_CostsItsPenalty(MistakeKind kind, int points)
        {
            var p = Run(TrainingMode.Test, (run, phase) => Inject(run, phase, kind));
            Assert.That(p.Mistakes.CountOf(kind), Is.EqualTo(1), "exactly one mistake of the kind was made");
            Assert.That(p.Mistakes.Count, Is.EqualTo(1), "and nothing else");
            var s = ScoreCalculator.Calculate(TrainingMode.Test, p.Mistakes, p.BoltSets, 360, Rules);
            Assert.That(s.Score, Is.EqualTo(100 - points));
            Assert.That(s.PenaltyPoints, Is.EqualTo(points));
        }

        /// <summary>Makes one mistake of <paramref name="kind"/> through the real rules, in the right phase of the run.</summary>
        private static void Inject(AssemblyProgress p, string phase, MistakeKind kind)
        {
            var set = p.BoltSets[0];
            var turned = new PartPose(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Math.PI / 2f));
            var far = new PartPose(new Vector3(0.5f, 0f, 0f), Quaternion.Identity);
            switch (kind)
            {
                case MistakeKind.WrongOrder when phase == "parts":
                    p.TryInstall("battery_module", Fit, 10, "Battery Module 1"); // before the cooling plate
                    break;
                case MistakeKind.WrongOrientation when phase == "parts":
                    p.TryInstall("cooling_plate", turned, 20, "Cooling Plate");
                    break;
                case MistakeKind.NotSeated when phase == "parts":
                    p.TryInstall("cooling_plate", far, 20, "Cooling Plate");
                    break;
                case MistakeKind.CrossThread when phase == "bolts":
                    set.DropIn("h3", 30f, 125); // jams, then pulled out and started again
                    set.Remove("h3");
                    break;
                case MistakeKind.TorqueBeforeAllSeated when phase == "bolts":
                    set.Insert("h1");
                    set.HandTurn("h1", 1080f, 0f, 125);
                    set.EngageWrench("h1", 126); // others not seated yet
                    break;
                case MistakeKind.WrongSequence when phase == "torque":
                    set.EngageWrench("h5", 300); // bolt 2 of the pattern before bolt 1 (allowed in Test, logged)
                    break;
                case MistakeKind.OverTorque when phase == "torque":
                    set.EngageWrench("h1", 300);
                    set.WrenchTurn("h1", 100f, 301); // 10 N·m, over 8.4
                    break;
            }
        }

        [Test]
        public void Score_IsClampedAtZero()
        {
            var p = Run(TrainingMode.Test, (run, phase) =>
            {
                if (phase != "parts") return;
                for (int i = 0; i < 15; i++) run.TryInstall("cover", Fit, i, "Pack Cover"); // 15 wrong orders = 150 points
            });
            var s = ScoreCalculator.Calculate(TrainingMode.Test, p.Mistakes, p.BoltSets, 360, Rules);
            Assert.That(s.Score, Is.EqualTo(0));
            Assert.That(s.Result, Is.EqualTo(RunResult.Fail));
        }

        [Test]
        public void CriticalMistake_Fails_EvenWithAHighScore()
        {
            var p = Run(TrainingMode.Test, (run, phase) => Inject(run, phase, MistakeKind.OverTorque));
            var s = ScoreCalculator.Calculate(TrainingMode.Test, p.Mistakes, p.BoltSets, 360, Rules);
            Assert.That(s.Score, Is.EqualTo(90).And.GreaterThanOrEqualTo((int)Rules.PassScore));
            Assert.That(s.CriticalCount, Is.EqualTo(1));
            Assert.That(s.Result, Is.EqualTo(RunResult.Fail));
        }

        [Test]
        public void CrossThreadLeftIn_IsCritical()
        {
            var p = new AssemblyProgress(Pack(), TrainingMode.Test);
            p.BoltSets[0].DropIn("h2", 30f, 5); // jammed and never pulled out (run abandoned)
            var s = ScoreCalculator.Calculate(TrainingMode.Test, new MistakeLog(), p.BoltSets, 360, Rules);
            Assert.That(s.CriticalCount, Is.EqualTo(1));
            Assert.That(s.Result, Is.EqualTo(RunResult.Fail));
        }

        [Test]
        public void ScoreBelowPassMark_Fails_WithoutCriticalMistakes()
        {
            var p = Run(TrainingMode.Test, (run, phase) =>
            {
                if (phase != "parts") return;
                for (int i = 0; i < 3; i++) run.TryInstall("cover", Fit, i, "Pack Cover"); // 30 points
            });
            var s = ScoreCalculator.Calculate(TrainingMode.Test, p.Mistakes, p.BoltSets, 360, Rules);
            Assert.That(s.Score, Is.EqualTo(70));
            Assert.That(s.CriticalCount, Is.EqualTo(0));
            Assert.That(s.Result, Is.EqualTo(RunResult.Fail));
        }

        [Test]
        public void LearnRun_IsAPracticeSummary_NoPassFail_NoTimeEffect()
        {
            var p = Run(TrainingMode.Learn, (run, phase) => Inject(run, phase, MistakeKind.OverTorque));
            var s = ScoreCalculator.Calculate(TrainingMode.Learn, p.Mistakes, p.BoltSets, 3600, Rules); // an hour: way over target
            Assert.That(s.Result, Is.EqualTo(RunResult.Practice));
            Assert.That(s.IsAssessment, Is.False);
            Assert.That(s.TimeAdjustment, Is.EqualTo(0f));
            Assert.That(s.Score, Is.EqualTo(90), "same penalties as Test");
        }

        [Test]
        public void TimeEffect_IsSmallAndCapped()
        {
            Assert.That(Rules.TimeAdjustment(360), Is.EqualTo(0f));
            Assert.That(Rules.TimeAdjustment(420), Is.EqualTo(-1f).Within(1e-5f), "a minute slow: -1");
            Assert.That(Rules.TimeAdjustment(3600), Is.EqualTo(-5f), "capped");
            Assert.That(Rules.TimeAdjustment(10), Is.EqualTo(5f), "capped bonus");

            var p = Run(TrainingMode.Test, (run, phase) => Inject(run, phase, MistakeKind.WrongOrientation));
            Assert.That(ScoreCalculator.Calculate(TrainingMode.Test, p.Mistakes, p.BoltSets, 60, Rules).Score, Is.EqualTo(100), "95 + 5 bonus");
            Assert.That(ScoreCalculator.Calculate(TrainingMode.Test, p.Mistakes, p.BoltSets, 7200, Rules).Score, Is.EqualTo(90), "95 - 5");
            Assert.That(ScoreCalculator.Calculate(TrainingMode.Test, Run(TrainingMode.Test).Mistakes, null, 10, Rules).Score,
                Is.EqualTo(100), "never above 100");
        }

        [Test]
        public void InvalidRules_Throw()
        {
            var none = new System.Collections.Generic.Dictionary<MistakeKind, float>();
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoringRules(none, null, passScore: 120f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoringRules(none, null, targetSeconds: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ScoringRules(new System.Collections.Generic.Dictionary<MistakeKind, float> { [MistakeKind.NotSeated] = -1f }, null));
        }

        // ---------------- Report contents ----------------

        [Test]
        public void Report_HeaderAndSummary_MatchTheRun()
        {
            var p = Run(TrainingMode.Test, (run, phase) => Inject(run, phase, MistakeKind.WrongOrder));
            var r = Report(p, 288);

            Assert.That(r.moduleId, Is.EqualTo("battery_pack"));
            Assert.That(r.moduleTitle, Is.EqualTo("EV Battery Pack Assembly"));
            Assert.That(r.mode, Is.EqualTo("Test"));
            Assert.That(r.sessionId, Is.EqualTo("session-1"));
            Assert.That(r.startTime, Is.EqualTo("2026-11-15T10:00:00.0000000+05:30"));
            Assert.That(r.durationSeconds, Is.EqualTo(288));
            Assert.That(r.result, Is.EqualTo("PASS"));
            Assert.That(r.score, Is.EqualTo(91), "100 - 10 + 1.2 time bonus, rounded");

            var s = r.summary;
            Assert.That(s.stepCount, Is.EqualTo(5), "plate, modules, cover, hand-start, torque (the tray is pre-installed)");
            Assert.That(s.stepsInOrder, Is.EqualTo(4));
            Assert.That(s.wrongOrderAttempts, Is.EqualTo(1));
            Assert.That(s.installs, Is.EqualTo(4));
            Assert.That(s.firstTimeFits, Is.EqualTo(3), "module 1 needed a second try");
            Assert.That(s.firstTimeFitPercent, Is.EqualTo(75f));
            Assert.That(s.boltCount, Is.EqualTo(8));
            Assert.That(s.boltsInSpec, Is.EqualTo(8));
            Assert.That(s.crossPatternFollowed, Is.True);
            Assert.That(s.mistakeCount, Is.EqualTo(1));
            Assert.That(s.criticalCount, Is.EqualTo(0));
        }

        [Test]
        public void Report_Steps_HaveNamesTimesAttemptsAndFirstTimeFit()
        {
            var r = Report(Run(TrainingMode.Test, (run, phase) => Inject(run, phase, MistakeKind.WrongOrder)));

            Assert.That(r.steps.Select(s => s.name), Is.EqualTo(new[]
                { "Fit cooling plate", "Fit battery module", "Fit pack cover", "Hand-start cover bolts", "Torque bolts in cross pattern" }));
            var modules = r.steps[1];
            Assert.That(modules.attempts, Is.EqualTo(3), "two fits and one wrong-order try");
            Assert.That(modules.firstTimeFit, Is.False);
            Assert.That(modules.inOrder, Is.False);
            Assert.That(modules.startSeconds, Is.EqualTo(30));
            Assert.That(modules.endSeconds, Is.EqualTo(80));
            Assert.That(modules.durationSeconds, Is.EqualTo(50));
            Assert.That(r.steps[0].firstTimeFit, Is.True);
            Assert.That(r.steps[3].attempts, Is.EqualTo(8), "eight bolts seated");
            Assert.That(r.steps[3].endSeconds, Is.EqualTo(210), "last bolt seated at 210 s");
            Assert.That(r.steps[4].endSeconds, Is.GreaterThan(r.steps[3].endSeconds));
        }

        [Test]
        public void Report_Mistakes_NameTheTypeItemStepAndTime()
        {
            var p = Run(TrainingMode.Test, (run, phase) =>
            {
                Inject(run, phase, MistakeKind.WrongOrder);
                Inject(run, phase, MistakeKind.OverTorque);
            });
            var r = Report(p);

            Assert.That(r.mistakes.Count, Is.EqualTo(2));
            var order = r.mistakes[0];
            Assert.That(order.type, Is.EqualTo("WrongOrder"));
            Assert.That(order.item, Is.EqualTo("Battery Module 1"));
            Assert.That(order.step, Is.EqualTo("Fit battery module"));
            Assert.That(order.stepNumber, Is.EqualTo(2));
            Assert.That(order.timeSeconds, Is.EqualTo(10));
            Assert.That(order.penalty, Is.EqualTo(10f));
            Assert.That(order.critical, Is.False);
            var over = r.mistakes[1];
            Assert.That(over.type, Is.EqualTo("OverTorque"));
            Assert.That(over.item, Is.EqualTo("Bolt 1"));
            Assert.That(over.step, Is.EqualTo("Torque bolts in cross pattern"));
            Assert.That(over.critical, Is.True);
            Assert.That(r.result, Is.EqualTo("FAIL"));
        }

        [Test]
        public void Report_Bolts_ShowTorqueResultAndPatternPosition()
        {
            var r = Report(Run(TrainingMode.Test, (run, phase) => Inject(run, phase, MistakeKind.OverTorque)));

            Assert.That(r.bolts.Select(b => b.number), Is.EqualTo(Enumerable.Range(1, 8)));
            Assert.That(r.bolts.Select(b => b.hole), Is.EqualTo(new[] { "h1", "h5", "h3", "h7", "h2", "h6", "h4", "h8" }));
            Assert.That(r.bolts[0].torqueResult, Is.EqualTo("Over"));
            Assert.That(r.bolts[0].torqueNm, Is.EqualTo(10f).Within(0.01f));
            Assert.That(r.bolts.Skip(1).All(b => b.torqueResult == "InSpec"), Is.True);
            Assert.That(r.bolts.All(b => b.patternPositionCorrect), Is.True);
            Assert.That(r.bolts[7].torqueOrder, Is.EqualTo(8));
            Assert.That(r.summary.boltsInSpec, Is.EqualTo(7));
        }

        [Test]
        public void Report_OutOfPatternTorque_MarksPositionsAndPattern()
        {
            var p = Run(TrainingMode.Test, (run, phase) =>
            {
                if (phase != "torque") return;
                var set = run.BoltSets[0];
                set.EngageWrench("h5", 300);
                set.WrenchTurn("h5", 80f, 301); // bolt 2 torqued first
            });
            var r = Report(p);
            Assert.That(r.bolts[1].torqueOrder, Is.EqualTo(1));
            Assert.That(r.bolts[1].patternPositionCorrect, Is.False);
            Assert.That(r.summary.crossPatternFollowed, Is.False);
        }

        [Test]
        public void LearnReport_IsAPracticeSummary()
        {
            var r = Report(Run(TrainingMode.Learn), 3600);
            Assert.That(r.mode, Is.EqualTo("Learn"));
            Assert.That(r.result, Is.EqualTo("PRACTICE"));
            Assert.That(r.score, Is.EqualTo(100), "no time penalty in Learn");
            Assert.That(r.timeAdjustment, Is.EqualTo(0f));
        }

        [Test]
        public void Report_RoundTripsThroughJson()
        {
            var r = Report(Run(TrainingMode.Test, (run, phase) =>
            {
                Inject(run, phase, MistakeKind.WrongOrder);
                Inject(run, phase, MistakeKind.OverTorque);
            }), 301.25);

            string json = UnityEngine.JsonUtility.ToJson(r, true);
            var back = UnityEngine.JsonUtility.FromJson<TrainingReport>(json);

            Assert.That(json, Does.Contain("\"moduleId\": \"battery_pack\"").And.Contain("\"result\": \"FAIL\""));
            Assert.That(UnityEngine.JsonUtility.ToJson(back, true), Is.EqualTo(json), "nothing lost or changed");
            Assert.That(back.steps.Count, Is.EqualTo(r.steps.Count));
            Assert.That(back.mistakes[1].item, Is.EqualTo("Bolt 1"));
            Assert.That(back.bolts[0].torqueResult, Is.EqualTo("Over"));
            Assert.That(back.summary.criticalCount, Is.EqualTo(1));
            Assert.That(back.durationSeconds, Is.EqualTo(301.25));
        }

        // ---------------- Texts ----------------

        [Test]
        public void Texts_DurationAndLastResult()
        {
            Assert.That(ReportFormat.Duration(288), Is.EqualTo("4m 48s"));
            Assert.That(ReportFormat.Duration(52.4), Is.EqualTo("52s"));
            Assert.That(ReportFormat.Duration(302), Is.EqualTo("5m 02s"));
            Assert.That(ReportFormat.LastResult(null), Is.EqualTo("No results yet"));
            Assert.That(ReportFormat.LastResult(new TrainingReport { result = "PASS", score = 91, durationSeconds = 288 }),
                Is.EqualTo("Last result: PASS 91% · 4m 48s"));
            Assert.That(ReportFormat.LastResult(new TrainingReport { result = "PRACTICE", score = 85, durationSeconds = 302 }),
                Is.EqualTo("Last result: Practice 85% · 5m 02s"));
        }
    }
}
