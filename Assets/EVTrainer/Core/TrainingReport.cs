using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    // The completion report as plain data: public fields with stable camelCase names (the App saves it as JSON).
    // Times: run times are seconds since the run started; wall-clock times are ISO 8601 with offset.

    [Serializable]
    public sealed class TrainingReport
    {
        public int schemaVersion = 1;
        public string moduleId;
        public string moduleTitle;
        /// <summary>"Learn" or "Test".</summary>
        public string mode;
        public string sessionId;
        public string startTime;
        public string endTime;
        public double durationSeconds;
        /// <summary>"PASS", "FAIL" or "PRACTICE" (Learn).</summary>
        public string result;
        public int score;
        public double penaltyPoints;
        public double timeAdjustment;
        public double passScore;
        public double targetSeconds;
        public ReportSummary summary = new ReportSummary();
        public List<StepResult> steps = new List<StepResult>();
        /// <summary>Chronological.</summary>
        public List<MistakeRecord> mistakes = new List<MistakeRecord>();
        /// <summary>By pattern number.</summary>
        public List<BoltResult> bolts = new List<BoltResult>();
    }

    [Serializable]
    public sealed class ReportSummary
    {
        public int stepCount;
        /// <summary>Steps completed with no wrong-order attempt.</summary>
        public int stepsInOrder;
        public int wrongOrderAttempts;
        public int installs;
        public int firstTimeFits;
        public double firstTimeFitPercent;
        public int boltCount;
        public int boltsInSpec;
        public bool crossPatternFollowed;
        public int mistakeCount;
        public int criticalCount;
    }

    [Serializable]
    public sealed class StepResult
    {
        public int number;
        public string name;
        public double startSeconds;
        public double endSeconds;
        public double durationSeconds;
        /// <summary>Tries: successful fits plus failed attempts in this step.</summary>
        public int attempts;
        public int mistakes;
        public bool firstTimeFit;
        public bool inOrder;
    }

    [Serializable]
    public sealed class MistakeRecord
    {
        /// <summary><see cref="MistakeKind"/> name, or "CrossThreadLeftIn".</summary>
        public string type;
        /// <summary>The physical part (e.g. "Battery Module 3") or "Bolt 5" (pattern number).</summary>
        public string item;
        public string step;
        public int stepNumber;
        public double timeSeconds;
        public string reason;
        public bool critical;
        public double penalty;
    }

    [Serializable]
    public sealed class BoltResult
    {
        /// <summary>Position in the tightening pattern (1 = first).</summary>
        public int number;
        public string hole;
        public double torqueNm;
        public double specNm;
        public double toleranceNm;
        /// <summary>"InSpec", "Over", "Under" or "CrossThreaded".</summary>
        public string torqueResult;
        /// <summary>When it was actually torqued (1 = first), 0 if never.</summary>
        public int torqueOrder;
        public bool patternPositionCorrect;
    }

    /// <summary>Builds the plain report from a finished (or abandoned) run.</summary>
    public static class TrainingReportBuilder
    {
        public const string CrossThreadLeftIn = "CrossThreadLeftIn";

        public static TrainingReport Build(AssemblyProgress progress, ScoringRules rules, string sessionId,
            DateTimeOffset start, DateTimeOffset end, double durationSeconds)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            var definition = progress.Definition;
            var score = ScoreCalculator.Calculate(progress.Mode, progress.Mistakes, progress.BoltSets, durationSeconds, rules);
            var report = new TrainingReport
            {
                moduleId = definition.Id,
                moduleTitle = definition.Title,
                mode = progress.Mode.ToString(),
                sessionId = sessionId ?? "",
                startTime = start.ToString("o"),
                endTime = end.ToString("o"),
                durationSeconds = Math.Round(durationSeconds, 2),
                result = score.Result == RunResult.Practice ? "PRACTICE" : score.Result == RunResult.Pass ? "PASS" : "FAIL",
                score = score.Score,
                penaltyPoints = Math.Round(score.PenaltyPoints, 2),
                timeAdjustment = Math.Round(score.TimeAdjustment, 2),
                passScore = Math.Round(rules.PassScore, 2),
                targetSeconds = rules.TargetSeconds,
            };

            // Steps the trainee does (the pre-installed tray is not one), with the mistakes attributed to each.
            var steps = new List<AssemblyStep>();
            foreach (var s in definition.Steps) if (!s.IsPreInstalled) steps.Add(s);
            var stepOf = new int[progress.Mistakes.Count];
            for (int i = 0; i < stepOf.Length; i++) stepOf[i] = StepIndexOf(progress.Mistakes.Entries[i], steps, progress);

            double previousEnd = 0;
            for (int s = 0; s < steps.Count; s++)
            {
                var step = steps[s];
                int stepMistakes = 0, wrongOrder = 0;
                for (int i = 0; i < stepOf.Length; i++)
                {
                    if (stepOf[i] != s) continue;
                    stepMistakes++;
                    if (progress.Mistakes.Entries[i].Kind == MistakeKind.WrongOrder) wrongOrder++;
                }
                double end1 = StepEnd(step, progress, previousEnd);
                report.steps.Add(new StepResult
                {
                    number = s + 1,
                    name = step.Instruction,
                    startSeconds = Math.Round(previousEnd, 2),
                    endSeconds = Math.Round(end1, 2),
                    durationSeconds = Math.Round(Math.Max(0, end1 - previousEnd), 2),
                    attempts = Successes(step, progress) + stepMistakes,
                    mistakes = stepMistakes,
                    firstTimeFit = stepMistakes == 0,
                    inOrder = wrongOrder == 0,
                });
                previousEnd = Math.Max(previousEnd, end1);
            }

            for (int i = 0; i < progress.Mistakes.Count; i++)
            {
                var m = progress.Mistakes.Entries[i];
                report.mistakes.Add(new MistakeRecord
                {
                    type = m.Kind.ToString(),
                    item = ItemName(m, progress),
                    step = stepOf[i] >= 0 ? steps[stepOf[i]].Instruction : "",
                    stepNumber = stepOf[i] + 1,
                    timeSeconds = Math.Round(m.TimeSeconds, 2),
                    reason = m.Reason,
                    critical = rules.IsCritical(m.Kind),
                    penalty = Math.Round(rules.Penalty(m.Kind), 2),
                });
            }

            bool patternOk = true;
            foreach (var set in progress.BoltSets)
            {
                patternOk &= set.Pattern.Evaluate(set.TorqueOrder) == PatternCheck.Ok;
                var expected = set.Spec.PatternOrder;
                for (int n = 0; n < expected.Count; n++)
                {
                    var bolt = set.Get(expected[n]);
                    int order = IndexOf(set.TorqueOrder, expected[n]) + 1;
                    report.bolts.Add(new BoltResult
                    {
                        number = n + 1,
                        hole = expected[n],
                        torqueNm = Math.Round(bolt.TorqueNm, 2),
                        specNm = Math.Round(bolt.Spec.TorqueSpecNm, 2),
                        toleranceNm = Math.Round(bolt.Spec.TorqueToleranceNm, 2),
                        torqueResult = TorqueResult(bolt),
                        torqueOrder = order,
                        patternPositionCorrect = order == n + 1,
                    });
                    if (bolt.State == BoltState.CrossThreaded && rules.CrossThreadLeftInIsCritical)
                        report.mistakes.Add(new MistakeRecord
                        {
                            type = CrossThreadLeftIn,
                            item = "Bolt " + (n + 1),
                            step = "",
                            timeSeconds = Math.Round(durationSeconds, 2),
                            reason = "Bolt left cross-threaded in its hole.",
                            critical = true,
                        });
                }
            }

            var summary = report.summary;
            summary.stepCount = report.steps.Count;
            foreach (var s in report.steps) if (s.inOrder) summary.stepsInOrder++;
            summary.wrongOrderAttempts = progress.Mistakes.CountOf(MistakeKind.WrongOrder);
            summary.installs = progress.InstallRecords.Count;
            summary.firstTimeFits = progress.FirstTimeFitCount;
            summary.firstTimeFitPercent = summary.installs == 0 ? 0
                : Math.Round(100.0 * summary.firstTimeFits / summary.installs, 1);
            summary.boltCount = report.bolts.Count;
            foreach (var b in report.bolts) if (b.torqueResult == "InSpec") summary.boltsInSpec++;
            summary.crossPatternFollowed = patternOk && progress.Mistakes.CountOf(MistakeKind.WrongSequence) == 0;
            summary.mistakeCount = report.mistakes.Count;
            summary.criticalCount = score.CriticalCount;
            return report;
        }

        private static string TorqueResult(BoltTightening bolt)
        {
            if (bolt.State == BoltState.CrossThreaded) return "CrossThreaded";
            if (bolt.State == BoltState.OverTorqued || bolt.TorqueNm > bolt.Spec.MaxTorqueNm) return "Over";
            if (bolt.TorqueNm < bolt.Spec.MinTorqueNm) return "Under";
            return "InSpec";
        }

        private static int Successes(AssemblyStep step, AssemblyProgress progress)
        {
            switch (step.Kind)
            {
                case StepKind.HandStartBolts: return progress.GetBoltSet(step.BoltSet.Id).SeatedCount;
                case StepKind.TorqueBolts: return progress.GetBoltSet(step.BoltSet.Id).FinishedCount;
                default:
                    int n = 0;
                    foreach (var r in progress.InstallRecords) if (Contains(step, r.SlotId)) n++;
                    return n;
            }
        }

        /// <summary>When the step was finished: its last fit, last bolt seated or last bolt torqued.</summary>
        private static double StepEnd(AssemblyStep step, AssemblyProgress progress, double fallback)
        {
            double end = fallback;
            if (step.Kind == StepKind.Parts)
            {
                foreach (var r in progress.InstallRecords) if (Contains(step, r.SlotId)) end = Math.Max(end, r.TimeSeconds);
                return end;
            }
            var set = progress.GetBoltSet(step.BoltSet.Id);
            foreach (var b in set.Bolts)
            {
                bool known = step.Kind == StepKind.HandStartBolts
                    ? set.TryGetSeatedTime(b.Spec.HoleId, out double t)
                    : set.TryGetFinishedTime(b.Spec.HoleId, out t);
                if (known) end = Math.Max(end, t);
            }
            return end;
        }

        /// <summary>Part mistakes belong to the step of their slot; bolt jams to hand-start, wrench mistakes to torque.</summary>
        private static int StepIndexOf(MistakeEntry m, List<AssemblyStep> steps, AssemblyProgress progress)
        {
            bool bolt = m.TypeId == BoltSet.MistakeTypeId;
            for (int s = 0; s < steps.Count; s++)
            {
                var step = steps[s];
                if (!bolt)
                {
                    if (step.Kind == StepKind.Parts && Contains(step, m.SlotId)) return s;
                    continue;
                }
                if (step.Kind == StepKind.Parts || !HasHole(step.BoltSet, m.SlotId)) continue;
                bool handStart = m.Kind == MistakeKind.CrossThread;
                if ((step.Kind == StepKind.HandStartBolts) == handStart) return s;
            }
            return -1;
        }

        private static string ItemName(MistakeEntry m, AssemblyProgress progress)
        {
            if (m.TypeId != BoltSet.MistakeTypeId) return m.ItemId;
            foreach (var set in progress.BoltSets)
            {
                int n = set.Pattern.SequenceNumber(m.SlotId);
                if (n > 0) return "Bolt " + n;
            }
            return m.ItemId;
        }

        private static bool Contains(AssemblyStep step, string slotId)
        {
            foreach (var slot in step.Slots) if (slot.Id == slotId) return true;
            return false;
        }

        private static bool HasHole(BoltSetSpec set, string holeId)
        {
            foreach (var b in set.Bolts) if (b.HoleId == holeId) return true;
            return false;
        }

        private static int IndexOf(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == value) return i;
            return -1;
        }
    }

    /// <summary>Short texts for the report panel and the "last result" line.</summary>
    public static class ReportFormat
    {
        /// <summary>"4m 48s", "52s".</summary>
        public static string Duration(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
            int total = (int)Math.Round(seconds, MidpointRounding.AwayFromZero);
            return total >= 60 ? $"{total / 60}m {total % 60:00}s" : $"{total}s";
        }

        /// <summary>Plain name of a mistake type for the trainee.</summary>
        public static string MistakeTitle(string type) => type switch
        {
            nameof(MistakeKind.WrongOrder) => "Wrong order",
            nameof(MistakeKind.WrongOrientation) => "Wrong way round",
            nameof(MistakeKind.NotSeated) => "Not seated",
            nameof(MistakeKind.CrossThread) => "Cross-threaded",
            nameof(MistakeKind.WrongSequence) => "Out of pattern",
            nameof(MistakeKind.OverTorque) => "Over-torqued",
            nameof(MistakeKind.TorqueBeforeAllSeated) => "Torqued too early",
            TrainingReportBuilder.CrossThreadLeftIn => "Cross-thread left in",
            _ => type ?? "",
        };

        /// <summary>Most important first: critical, then by penalty, then in the order they happened.</summary>
        public static List<MistakeRecord> ByImportance(IReadOnlyList<MistakeRecord> mistakes)
        {
            var list = new List<MistakeRecord>(mistakes ?? Array.Empty<MistakeRecord>());
            var order = new Dictionary<MistakeRecord, int>();
            for (int i = 0; i < list.Count; i++) order[list[i]] = i;
            list.Sort((a, b) =>
            {
                if (a.critical != b.critical) return a.critical ? -1 : 1;
                int byPenalty = b.penalty.CompareTo(a.penalty);
                return byPenalty != 0 ? byPenalty : order[a].CompareTo(order[b]);
            });
            return list;
        }

        /// <summary>One line per mistake: "Over-torqued · Bolt 1 · 5m 01s".</summary>
        public static string MistakeLine(MistakeRecord m) =>
            $"{MistakeTitle(m.type)} · {m.item} · {Duration(m.timeSeconds)}";

        /// <summary>The key rows of the report panel, label and value.</summary>
        public static List<KeyValuePair<string, string>> KeyRows(TrainingReport r)
        {
            var s = r.summary;
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Steps in correct order", $"{s.stepsInOrder}/{s.stepCount}"),
                new KeyValuePair<string, string>("Wrong-order attempts", s.wrongOrderAttempts.ToString()),
                new KeyValuePair<string, string>("First-time fits", $"{Math.Round(s.firstTimeFitPercent, MidpointRounding.AwayFromZero)}%"),
                new KeyValuePair<string, string>("Torque in spec", $"{s.boltsInSpec}/{s.boltCount}"),
                new KeyValuePair<string, string>("Cross pattern followed", s.crossPatternFollowed ? "Yes" : "No"),
                new KeyValuePair<string, string>("Critical mistakes", s.criticalCount.ToString()),
                new KeyValuePair<string, string>("Total time", Duration(r.durationSeconds)),
            };
        }

        /// <summary>Number of pages of <paramref name="perPage"/> lines (at least 1).</summary>
        public static int PageCount(int lines, int perPage) => perPage <= 0 ? 1 : Math.Max(1, (lines + perPage - 1) / perPage);

        /// <summary>"Last result: PASS 91% · 4m 48s", "Last result: Practice 85% · 5m 02s" or "No results yet".</summary>
        public static string LastResult(TrainingReport report)
        {
            if (report == null || string.IsNullOrEmpty(report.result)) return "No results yet";
            string label = report.result == "PRACTICE" ? "Practice" : report.result;
            return $"Last result: {label} {report.score}% · {Duration(report.durationSeconds)}";
        }
    }
}
