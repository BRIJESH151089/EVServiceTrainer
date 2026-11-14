using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    /// <summary>
    /// Scoring and pass rules (module data): a run starts at 100 and loses the penalty of every logged mistake; Test mode
    /// adds a small time bonus or penalty against a target time (capped); the score is clamped to 0..100. A critical
    /// mistake fails the run regardless of the score. PASS = score ≥ <see cref="PassScore"/> and no critical mistakes.
    /// </summary>
    public sealed class ScoringRules
    {
        public const float StartScore = 100f;

        private readonly Dictionary<MistakeKind, float> _penalties;
        private readonly HashSet<MistakeKind> _critical;

        public ScoringRules(IReadOnlyDictionary<MistakeKind, float> penalties, IEnumerable<MistakeKind> criticalKinds,
            bool crossThreadLeftInIsCritical = true, float passScore = 80f, double targetSeconds = 360,
            float timePointsPerMinute = 1f, float timeCapPoints = 5f)
        {
            if (penalties == null) throw new ArgumentNullException(nameof(penalties));
            if (float.IsNaN(passScore) || passScore < 0f || passScore > StartScore) throw new ArgumentOutOfRangeException(nameof(passScore));
            if (double.IsNaN(targetSeconds) || targetSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(targetSeconds));
            if (float.IsNaN(timePointsPerMinute) || timePointsPerMinute < 0f) throw new ArgumentOutOfRangeException(nameof(timePointsPerMinute));
            if (float.IsNaN(timeCapPoints) || timeCapPoints < 0f) throw new ArgumentOutOfRangeException(nameof(timeCapPoints));
            _penalties = new Dictionary<MistakeKind, float>();
            foreach (var kv in penalties)
            {
                if (float.IsNaN(kv.Value) || kv.Value < 0f) throw new ArgumentOutOfRangeException(nameof(penalties));
                _penalties[kv.Key] = kv.Value;
            }
            _critical = new HashSet<MistakeKind>(criticalKinds ?? Array.Empty<MistakeKind>());
            CrossThreadLeftInIsCritical = crossThreadLeftInIsCritical;
            PassScore = passScore;
            TargetSeconds = targetSeconds;
            TimePointsPerMinute = timePointsPerMinute;
            TimeCapPoints = timeCapPoints;
        }

        /// <summary>The trainer's default rules (the battery pack module uses these unless its data changes them).</summary>
        public static ScoringRules Default { get; } = new ScoringRules(
            new Dictionary<MistakeKind, float>
            {
                [MistakeKind.WrongOrder] = 10f,
                [MistakeKind.WrongOrientation] = 5f,
                [MistakeKind.NotSeated] = 2f,
                [MistakeKind.CrossThread] = 10f,
                [MistakeKind.OverTorque] = 10f,
                [MistakeKind.WrongSequence] = 5f,
                [MistakeKind.TorqueBeforeAllSeated] = 5f,
            },
            new[] { MistakeKind.OverTorque });

        public bool CrossThreadLeftInIsCritical { get; }
        public float PassScore { get; }
        public double TargetSeconds { get; }
        public float TimePointsPerMinute { get; }
        public float TimeCapPoints { get; }

        public float Penalty(MistakeKind kind) => _penalties.TryGetValue(kind, out var p) ? p : 0f;
        public bool IsCritical(MistakeKind kind) => _critical.Contains(kind);

        /// <summary>Points added (faster than the target) or taken (slower), capped at ± <see cref="TimeCapPoints"/>.</summary>
        public float TimeAdjustment(double durationSeconds)
        {
            if (double.IsNaN(durationSeconds) || durationSeconds < 0) return 0f;
            double points = (TargetSeconds - durationSeconds) / 60.0 * TimePointsPerMinute;
            return (float)Math.Max(-TimeCapPoints, Math.Min(TimeCapPoints, points));
        }
    }

    public enum RunResult { Practice, Pass, Fail }

    /// <summary>The outcome of scoring one run.</summary>
    public readonly struct ScoreResult
    {
        public ScoreResult(int score, float penaltyPoints, float timeAdjustment, int criticalCount, RunResult result)
        {
            Score = score;
            PenaltyPoints = penaltyPoints;
            TimeAdjustment = timeAdjustment;
            CriticalCount = criticalCount;
            Result = result;
        }

        /// <summary>0..100, rounded.</summary>
        public int Score { get; }
        public float PenaltyPoints { get; }
        public float TimeAdjustment { get; }
        public int CriticalCount { get; }

        /// <summary>Practice (Learn), otherwise Pass or Fail.</summary>
        public RunResult Result { get; }
        public bool IsAssessment => Result != RunResult.Practice;
    }

    /// <summary>
    /// Scores a run from its mistakes and final bolt states. Learn mode gives a practice score (same penalties, no time
    /// effect, no pass/fail); Test mode gives the assessment.
    /// </summary>
    public static class ScoreCalculator
    {
        public static ScoreResult Calculate(TrainingMode mode, MistakeLog mistakes, IReadOnlyList<BoltSet> boltSets,
            double durationSeconds, ScoringRules rules)
        {
            if (mistakes == null) throw new ArgumentNullException(nameof(mistakes));
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            float penalty = 0f;
            int critical = 0;
            foreach (var m in mistakes.Entries)
            {
                penalty += rules.Penalty(m.Kind);
                if (rules.IsCritical(m.Kind)) critical++;
            }
            critical += CrossThreadsLeftIn(boltSets, rules);

            bool assessment = mode == TrainingMode.Test;
            float time = assessment ? rules.TimeAdjustment(durationSeconds) : 0f;
            float raw = ScoringRules.StartScore - penalty + time;
            int score = (int)Math.Round(Math.Max(0f, Math.Min(ScoringRules.StartScore, raw)), MidpointRounding.AwayFromZero);
            var result = !assessment ? RunResult.Practice
                : score >= rules.PassScore && critical == 0 ? RunResult.Pass : RunResult.Fail;
            return new ScoreResult(score, penalty, time, critical, result);
        }

        /// <summary>Bolts still jammed in their holes at the end (critical when the rules say so).</summary>
        public static int CrossThreadsLeftIn(IReadOnlyList<BoltSet> boltSets, ScoringRules rules)
        {
            if (boltSets == null || !rules.CrossThreadLeftInIsCritical) return 0;
            int n = 0;
            foreach (var set in boltSets)
                foreach (var b in set.Bolts)
                    if (b.State == BoltState.CrossThreaded) n++;
            return n;
        }
    }
}
