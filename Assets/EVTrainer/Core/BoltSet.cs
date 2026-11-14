using System;
using System.Collections.Generic;
using System.Numerics;

namespace EVTrainer.Core
{
    public enum WrenchCheck
    {
        /// <summary>The wrench may turn this bolt.</summary>
        Ok,
        /// <summary>Out of the pattern order but allowed (Test mode): logged as WrongSequence.</summary>
        OutOfOrderAllowed,
        /// <summary>This bolt is not seated yet (or is cross-threaded).</summary>
        BoltNotSeated,
        /// <summary>Rule: hand-start every bolt before torquing any.</summary>
        NotAllSeated,
        /// <summary>Out of the pattern order and blocked (Learn mode).</summary>
        OutOfOrderBlocked,
    }

    public readonly struct WrenchEngageResult
    {
        public WrenchEngageResult(WrenchCheck check, string holeId, string reason)
        {
            Check = check;
            HoleId = holeId;
            Reason = reason ?? "";
        }

        public WrenchCheck Check { get; }
        public string HoleId { get; }
        public string Reason { get; }
        public bool Allowed => Check == WrenchCheck.Ok || Check == WrenchCheck.OutOfOrderAllowed;
    }

    /// <summary>
    /// Run-time state of one bolt set: which holes hold a bolt, each bolt's tightening, and the rules
    /// (all seated before any torque; pattern order, blocked or only logged). Mistakes go to the shared log.
    /// </summary>
    public sealed class BoltSet
    {
        public const string MistakeTypeId = "bolt";

        private readonly Dictionary<string, BoltTightening> _byHole = new Dictionary<string, BoltTightening>(StringComparer.Ordinal);
        private readonly HashSet<string> _occupied = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _torqueOrder = new List<string>();
        private readonly Dictionary<string, double> _seatedAt = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _finishedAt = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly List<BoltTightening> _bolts = new List<BoltTightening>();
        private readonly MistakeLog _log;
        private readonly Func<string, bool> _isFinishedHole; // one delegate, not one per call (read every frame)

        public BoltSet(BoltSetSpec spec, bool blockOutOfOrder, MistakeLog log, bool straightenCrooked = false,
            bool autoDetachWhenFinished = false)
        {
            AutoDetachWhenFinished = autoDetachWhenFinished;
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            BlockOutOfOrder = blockOutOfOrder;
            StraightensCrooked = straightenCrooked;
            Pattern = new TorquePatternValidator(spec.PatternOrder);
            _isFinishedHole = h => Get(h).IsFinished;
            foreach (var b in spec.Bolts)
            {
                var t = new BoltTightening(b);
                _bolts.Add(t);
                _byHole.Add(b.HoleId, t);
            }
        }

        public BoltSetSpec Spec { get; }
        public TorquePatternValidator Pattern { get; }

        /// <summary>Learn: out-of-order torquing is blocked. Test: allowed but logged.</summary>
        public bool BlockOutOfOrder { get; }

        /// <summary>Learn: a crooked drop-in is straightened (Started, not logged) instead of cross-threading.</summary>
        public bool StraightensCrooked { get; }

        /// <summary>
        /// Learn: the wrench comes off by itself once a bolt is finished. Test: it stays on; the trainee must lift it off
        /// at the click, and swinging on past the window over-torques (logged).
        /// </summary>
        public bool AutoDetachWhenFinished { get; }

        /// <summary>True when a drop at <paramref name="tiltDegrees"/> into this hole would cross-thread without straightening
        /// (Learn uses it to show "keep the bolt straight" while still straightening).</summary>
        public bool WouldCrossThread(string holeId, float tiltDegrees) =>
            BoltTightening.IsCrossThreaded(tiltDegrees, Get(holeId).Spec.MaxStartTiltDegrees);

        public IReadOnlyList<BoltTightening> Bolts => _bolts;
        public int Count => _bolts.Count;

        /// <summary>Holes in the order their bolts first reached Torqued / OverTorqued.</summary>
        public IReadOnlyList<string> TorqueOrder => _torqueOrder;

        /// <summary>Run time at which the bolt in <paramref name="holeId"/> was first seated (for the report).</summary>
        public bool TryGetSeatedTime(string holeId, out double timeSeconds) => _seatedAt.TryGetValue(holeId, out timeSeconds);

        /// <summary>Run time at which the bolt in <paramref name="holeId"/> was first finished with the wrench (for the report).</summary>
        public bool TryGetFinishedTime(string holeId, out double timeSeconds) => _finishedAt.TryGetValue(holeId, out timeSeconds);

        public BoltTightening Get(string holeId)
        {
            if (holeId == null || !_byHole.TryGetValue(holeId, out var t))
                throw new ArgumentException($"Unknown hole '{holeId}' in bolt set '{Spec.Id}'.", nameof(holeId));
            return t;
        }

        public bool IsOccupied(string holeId) => _occupied.Contains(holeId);

        public int SeatedCount => CountWhere(b => b.IsSeated);
        public int FinishedCount => CountWhere(b => b.IsFinished);
        public bool AllSeated => SeatedCount == _bolts.Count;
        public bool IsComplete => FinishedCount == _bolts.Count;

        /// <summary>Next hole in the pattern that is not finished (what Learn mode highlights), or null.</summary>
        public string NextInPattern => Pattern.NextExpected(_isFinishedHole);

        /// <summary>Nearest empty hole whose axis line passes within <paramref name="radiusMetres"/> of <paramref name="point"/>
        /// and which the point is within <paramref name="maxHeightMetres"/> above. Null if none.</summary>
        public string FindFreeHole(Vector3 point, float radiusMetres, float maxHeightMetres)
        {
            string best = null;
            float bestDist = float.MaxValue;
            foreach (var b in _bolts)
            {
                if (_occupied.Contains(b.Spec.HoleId)) continue;
                var axis = Vector3.Transform(Vector3.UnitY, b.Spec.HolePose.Rotation);
                var rel = point - b.Spec.HolePose.Position;
                float height = Vector3.Dot(rel, axis);
                if (height < -radiusMetres || height > maxHeightMetres) continue;
                float radial = (rel - axis * height).Length();
                if (radial <= radiusMetres && radial < bestDist)
                {
                    best = b.Spec.HoleId;
                    bestDist = radial;
                }
            }
            return best;
        }

        /// <summary>
        /// Hole a released bolt drops into: the nearest empty hole within <paramref name="zone"/>'s distance of the
        /// bolt (measured to the hole centre on the surface) whose axis the bolt is within the zone's angle of.
        /// Null if none (the bolt just falls).
        /// </summary>
        public string FindDropHole(Vector3 boltPosition, Vector3 boltAxis, CaptureZone zone)
        {
            string best = null;
            float bestDist = float.MaxValue;
            foreach (var b in _bolts)
            {
                if (_occupied.Contains(b.Spec.HoleId)) continue;
                float d = Vector3.Distance(boltPosition, b.Spec.HolePose.Position);
                if (d > zone.PositionMetres || d >= bestDist) continue;
                if (TiltDegrees(b.Spec.HoleId, boltAxis) > zone.AngleDegrees) continue;
                best = b.Spec.HoleId;
                bestDist = d;
            }
            return best;
        }

        /// <summary>Angle between a bolt's axis and the hole's axis (degrees).</summary>
        public float TiltDegrees(string holeId, Vector3 boltAxis) =>
            AxisAngle.AngleBetween(boltAxis, Vector3.Transform(Vector3.UnitY, Get(holeId).Spec.HolePose.Rotation));

        /// <summary>
        /// A released bolt drops into <paramref name="holeId"/>: Started when straight enough, otherwise it sits
        /// crooked, CrossThreaded (logged).
        /// </summary>
        public BoltState DropIn(string holeId, float tiltDegrees, double timeSeconds)
        {
            var bolt = Get(holeId);
            if (_occupied.Contains(holeId)) return bolt.State;
            _occupied.Add(holeId);
            var state = bolt.DropIn(StraightensCrooked ? 0f : tiltDegrees);
            if (state == BoltState.CrossThreaded) LogCrossThread(bolt, holeId, tiltDegrees, timeSeconds);
            return state;
        }

        /// <summary>A bolt is put into the hole (not yet turned).</summary>
        public void Insert(string holeId)
        {
            Get(holeId);
            _occupied.Add(holeId);
        }

        /// <summary>The bolt is pulled out: the hole is free again and the bolt state resets (clears a cross-thread).</summary>
        public void Remove(string holeId)
        {
            Get(holeId).Remove();
            _occupied.Remove(holeId);
            _torqueOrder.Remove(holeId);
            _seatedAt.Remove(holeId);
            _finishedAt.Remove(holeId);
        }

        /// <summary>Hand twist of the bolt in <paramref name="holeId"/>. Logs a CrossThread mistake when the thread jams.</summary>
        public BoltState HandTurn(string holeId, float deltaDegrees, float tiltDegrees, double timeSeconds)
        {
            var bolt = Get(holeId);
            var before = bolt.State;
            var after = bolt.HandTurn(deltaDegrees, tiltDegrees);
            if (bolt.IsSeated && !_seatedAt.ContainsKey(holeId)) _seatedAt[holeId] = timeSeconds;
            if (after == BoltState.CrossThreaded && before != BoltState.CrossThreaded)
                LogCrossThread(bolt, holeId, tiltDegrees, timeSeconds);
            return after;
        }

        private void LogCrossThread(BoltTightening bolt, string holeId, float tiltDegrees, double timeSeconds) =>
            _log.Record(new MistakeEntry(timeSeconds, MistakeKind.CrossThread, MistakeTypeId, holeId, holeId,
                $"Bolt started at {tiltDegrees:0} deg tilt (max {bolt.Spec.MaxStartTiltDegrees:0} deg): cross-threaded."));

        /// <summary>
        /// The wrench socket meets the bolt in <paramref name="holeId"/>. Checks the bolt is seated, that every bolt is
        /// seated (logged as TorqueBeforeAllSeated otherwise) and the pattern order (logged as WrongSequence; blocked
        /// when <see cref="BlockOutOfOrder"/>).
        /// </summary>
        public WrenchEngageResult EngageWrench(string holeId, double timeSeconds)
        {
            var bolt = Get(holeId);
            if (!bolt.IsSeated)
                return new WrenchEngageResult(WrenchCheck.BoltNotSeated, holeId,
                    bolt.State == BoltState.CrossThreaded
                        ? "This bolt is cross-threaded: pull it out and start it again, straight."
                        : "Hand-start this bolt until it is seated before using the wrench.");

            if (!AllSeated)
            {
                _log.Record(new MistakeEntry(timeSeconds, MistakeKind.TorqueBeforeAllSeated, MistakeTypeId, holeId, holeId,
                    "Wrench used before all bolts were hand-started."));
                return new WrenchEngageResult(WrenchCheck.NotAllSeated, holeId,
                    $"Hand-start all bolts first ({SeatedCount}/{Count} seated).");
            }

            if (Pattern.Check(holeId, _isFinishedHole) == PatternCheck.WrongSequence)
            {
                int next = Pattern.SequenceNumber(NextInPattern);
                int this_ = Pattern.SequenceNumber(holeId);
                string reason = $"Follow the cross pattern: bolt {next} is next (this is bolt {this_}).";
                _log.Record(new MistakeEntry(timeSeconds, MistakeKind.WrongSequence, MistakeTypeId, holeId, holeId, reason));
                return new WrenchEngageResult(BlockOutOfOrder ? WrenchCheck.OutOfOrderBlocked : WrenchCheck.OutOfOrderAllowed, holeId, reason);
            }
            return new WrenchEngageResult(WrenchCheck.Ok, holeId, "");
        }

        /// <summary>Wrench rotation on an engaged, seated bolt. Logs OverTorque once when the bolt goes over.</summary>
        public BoltState WrenchTurn(string holeId, float deltaDegrees, double timeSeconds)
        {
            var bolt = Get(holeId);
            if (!bolt.IsSeated) return bolt.State; // the wrench only fits seated bolts
            var before = bolt.State;
            var after = bolt.ApplyRotation(deltaDegrees);
            if (after != before && bolt.IsFinished && !_torqueOrder.Contains(holeId)) _torqueOrder.Add(holeId);
            if (bolt.IsFinished && !_finishedAt.ContainsKey(holeId)) _finishedAt[holeId] = timeSeconds;
            if (after == BoltState.OverTorqued && before != BoltState.OverTorqued)
                _log.Record(new MistakeEntry(timeSeconds, MistakeKind.OverTorque, MistakeTypeId, holeId, holeId,
                    $"Over-torqued: {bolt.TorqueNm:0.0} N·m (spec {bolt.Spec.TorqueSpecNm:0.0} ± {bolt.Spec.TorqueToleranceNm:0.0})."));
            return after;
        }

        private int CountWhere(Func<BoltTightening, bool> predicate)
        {
            int n = 0;
            foreach (var b in _bolts) if (predicate(b)) n++;
            return n;
        }
    }
}
