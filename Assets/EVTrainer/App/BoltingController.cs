using System.Collections.Generic;
using EVTrainer.Core;
using EVTrainer.Rendering;
using EVTrainer.UI;
using EVTrainer.XR;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// Bolt flow for the module's bolt set. The XR adapters only measure (hand orientation about a hole axis);
    /// Core <see cref="BoltSet"/> decides seating, cross-threading, torque and pattern rules; this class moves the
    /// bolts accordingly and reports to the HUD.
    /// </summary>
    public sealed class BoltingController : MonoBehaviour
    {
        [SerializeField] private AssemblyTrainingController _training;
        [Tooltip("Module data (bolt and wrench label texts).")]
        [SerializeField] private AssemblyModuleDefinition _module;
        [SerializeField] private BoltAdapter[] _bolts;

        [Header("Hand-start (drop-in distance and angle: the module's capture zone)")]
        [Tooltip("Bolt origin (under the head) to the shank tip; the drop-in distance is measured from the tip.")]
        [SerializeField, Min(0f)] private float _shankLengthMetres = 0.025f;
        [Tooltip("Learn: upright ghost bolt shown in every free hole.")]
        [SerializeField] private PartTint _boltGhostPrefab;
        [SerializeField] private Color _ghostReadyColor = new Color(0.25f, 1f, 0.4f, 0.5f);
        [Tooltip("Height of the bolt head above the surface when the thread first engages.")]
        [SerializeField, Min(0f)] private float _engageHeightMetres = 0.012f;
        [Tooltip("Pulling the hand this far from a jammed (or just inserted) bolt takes it out of the hole.")]
        [SerializeField, Min(0.01f)] private float _pullOutMetres = 0.07f;

        [Header("Torque wrench")]
        [SerializeField] private WrenchAdapter _wrench;
        [SerializeField] private TorqueGaugeView _gauge;
        [Tooltip("Height of the bolt head above the surface (the socket mouth attaches at its top). Attach and " +
                 "detach distances are module data (bolt set).")]
        [SerializeField, Min(0f)] private float _boltHeadHeightMetres = 0.007f;
        [Tooltip("A haptic tick every this many degrees of swing, stronger as torque rises toward spec.")]
        [SerializeField, Min(0.5f)] private float _hapticStepDegrees = 4f;

        [Header("Learn: pattern numbers")]
        [SerializeField] private FloatingNumberView _numberPrefab;
        [SerializeField, Min(0f)] private float _numberHeightMetres = 0.05f;

        [Header("Bolt colours")]
        [SerializeField] private Color _startedColor = new Color(0.95f, 0.8f, 0.15f);
        [SerializeField] private Color _torquedColor = new Color(0.25f, 0.85f, 0.35f);
        [SerializeField] private Color _faultColor = new Color(0.9f, 0.18f, 0.12f);

        private sealed class BoltRuntime
        {
            public BoltAdapter Adapter;
            public PartTint Tint;
            public string Hole;
            public readonly AxisRotationTracker Tracker = new AxisRotationTracker();
            public float SpinDegrees;
        }

        private readonly List<BoltRuntime> _runtimes = new List<BoltRuntime>();
        private readonly Dictionary<BoltAdapter, BoltRuntime> _byAdapter = new Dictionary<BoltAdapter, BoltRuntime>();
        private readonly Dictionary<string, FloatingNumberView> _numbers = new Dictionary<string, FloatingNumberView>();
        private readonly Dictionary<string, PartTint> _ghosts = new Dictionary<string, PartTint>();
        private string _readyHole; // free hole a held bolt would drop into (its ghost turns green)
        private RatchetSwing _swing;
        private bool _grabbableApplied;
        private bool _grabbable;
        private string _wrenchHole;        // bolt the wrench is fitted on
        private string _refusedHole;       // last bolt the wrench was refused on (don't repeat the message while it stays there)
        private float _hapticAccumulator;
        private AudioSource _audio;
        private AudioClip _click;
        private AudioClip _ratchetClick;
        private float _lastHandleAngle;

        private AssemblyProgress Progress => _training != null ? _training.Progress : null;
        private BoltSet Set => Progress != null && Progress.BoltSets.Count > 0 ? Progress.BoltSets[0] : null;
        private bool Learn => Progress != null && Progress.Mode == TrainingMode.Learn;
        private AssemblyModuleDefinition.BoltSetEntry Data => _module != null && _module.BoltSets.Count > 0 ? _module.BoltSets[0] : null;

        private void Awake()
        {
            foreach (var b in _bolts)
            {
                var rt = new BoltRuntime { Adapter = b, Tint = b.GetComponent<PartTint>() };
                _runtimes.Add(rt);
                _byAdapter[b] = rt;
            }

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            const int rate = 44100;
            var samples = ProceduralClick.CreateSamples(rate, 0.06f, 1800f, 120f);
            _click = AudioClip.Create("TorqueClick", samples.Length, 1, rate, false);
            _click.SetData(samples, 0);
            var soft = ProceduralClick.CreateSamples(rate, 0.02f, 4200f, 260f);
            _ratchetClick = AudioClip.Create("RatchetClick", soft.Length, 1, rate, false);
            _ratchetClick.SetData(soft, 0);
        }

        private void OnEnable()
        {
            foreach (var b in _bolts)
            {
                b.Grabbed += OnBoltGrabbed;
                b.Released += OnBoltReleased;
            }
            if (_wrench != null) _wrench.Released += OnWrenchReleased;
        }

        private void OnDisable()
        {
            foreach (var b in _bolts)
            {
                b.Grabbed -= OnBoltGrabbed;
                b.Released -= OnBoltReleased;
            }
            if (_wrench != null) _wrench.Released -= OnWrenchReleased;
        }

        private void Update()
        {
            bool running = _training != null && _training.IsRunning;
            if (!_grabbableApplied || running != _grabbable)
            {
                // Nothing can be picked up before a mode is chosen (same as the parts).
                _grabbable = running;
                _grabbableApplied = true;
                foreach (var rt in _runtimes) rt.Adapter.SetGrabbable(running && !IsSeatedInHole(rt));
                if (_wrench != null) _wrench.SetGrabbable(running);
            }
            if (!running || Set == null) return;

            _readyHole = null;
            foreach (var rt in _runtimes)
            {
                if (rt.Adapter.IsHeld) UpdateHeldBolt(rt);
                UpdateTint(rt);
            }
            UpdateWrench();
            UpdateNumbers();
            UpdateGhosts();
        }

        // ---------------- Learn: bolt ghosts ----------------

        private void UpdateGhosts()
        {
            if (_boltGhostPrefab == null) return;
            var set = Set;
            bool show = Learn && Progress.AreBoltsAvailable(set);
            foreach (var bolt in set.Bolts)
            {
                var hole = bolt.Spec.HoleId;
                bool visible = show && !set.IsOccupied(hole);
                if (!_ghosts.TryGetValue(hole, out var ghost))
                {
                    if (!visible) continue;
                    HolePose(bolt.Spec, out var pos, out var rot, out var axis);
                    ghost = Instantiate(_boltGhostPrefab, pos + axis * _engageHeightMetres, rot, transform);
                    ghost.name = "Bolt Ghost " + hole;
                    _ghosts[hole] = ghost;
                }
                if (ghost.gameObject.activeSelf != visible) ghost.gameObject.SetActive(visible);
                if (visible) ghost.SetTint(hole == _readyHole ? _ghostReadyColor : (Color?)null);
            }
        }

        // ---------------- Torque wrench ----------------

        private void UpdateWrench()
        {
            if (_wrench == null) return;
            var set = Set;
            if (!_wrench.IsHeld || !Progress.AreBoltsAvailable(set))
            {
                if (_wrenchHole != null) Detach();
                ShowGauge(set);
                return;
            }

            if (_wrenchHole == null)
            {
                var hole = FindBoltUnderSocket(set);
                if (hole == null) { _refusedHole = null; ShowGauge(set); return; }
                if (hole == _refusedHole) { ShowGauge(set); return; }
                var check = set.EngageWrench(hole, _training.RunTime);
                if (!check.Allowed)
                {
                    _refusedHole = hole;
                    _wrench.Pulse(0.8f, 0.15f);
                    _training.ShowStatus(Learn ? check.Reason : "Cannot torque this bolt yet.", good: false);
                    _training.NotifyProgressChanged(); // a logged mistake shows on the debug panel
                    ShowGauge(set);
                    return;
                }
                Attach(hole, set, check);
            }

            // Attached: the script alone poses the wrench. Socket on the bolt, handle pointing at the hand
            // (hand projected onto the swing plane, atan2 about the bolt axis); clockwise swing drives the bolt.
            var bolt = set.Get(_wrenchHole);
            HolePose(bolt.Spec, out var holePos, out var holeRot, out var axis);
            var headTop = holePos + axis * _boltHeadHeightMetres;
            var hand = _wrench.HandPosition;
            var wrenchSpec = set.Spec.Wrench;
            if (wrenchSpec.ShouldDetach(hand.ToNumerics(), headTop.ToNumerics(), axis.ToNumerics()))
            {
                Detach(); // pulled off the swing plane (keeps the last bolt message on the HUD)
                ShowGauge(set);
                return;
            }

            var step = _swing.Update(axis.ToNumerics(), (hand - headTop).ToNumerics());
            if (step.HasAngle) _lastHandleAngle = step.HandleAngleDegrees;
            PoseAttachedWrench(holePos, axis);

            if (step.DriveDegrees > 0f)
            {
                var before = bolt.State;
                var after = set.WrenchTurn(_wrenchHole, step.DriveDegrees, _training.RunTime);
                SpinBolt(_wrenchHole, step.DriveDegrees, holePos, holeRot, axis);
                RampHaptics(bolt, step.DriveDegrees);
                if (after != before) OnTorqueStateChanged(set, bolt, after);
            }
            if (step.BackClicks > 0)
            {
                _audio.transform.position = headTop;
                _audio.PlayOneShot(_ratchetClick, 0.25f);
                _wrench.Pulse(0.05f, 0.01f);
            }
            ShowGauge(set);
        }

        /// <summary>Socket over the bolt head (its mouth at the surface), handle along the current handle angle.</summary>
        private void PoseAttachedWrench(Vector3 holePos, Vector3 axis)
        {
            var reference = Mathf.Abs(Vector3.Dot(axis, Vector3.right)) < 0.9f ? Vector3.right : Vector3.forward;
            var u = Vector3.ProjectOnPlane(reference, axis).normalized;
            var handle = Quaternion.AngleAxis(_lastHandleAngle, axis) * u; // same basis and sign as Core AxisAngle
            _wrench.Drive(holePos + axis * _wrench.MouthToOrigin, Quaternion.LookRotation(handle, axis));
        }

        private string FindBoltUnderSocket(BoltSet set)
        {
            string best = null;
            float bestDist = float.MaxValue;
            var wrenchSpec = set.Spec.Wrench;
            var mouth = _wrench.SocketMouth;
            foreach (var rt in _runtimes)
            {
                if (rt.Hole == null) continue;
                HolePose(set.Get(rt.Hole).Spec, out var holePos, out _, out var axis);
                var headTop = holePos + axis * _boltHeadHeightMetres;
                if (!wrenchSpec.IsOnHead(mouth.ToNumerics(), _wrench.SocketAxis.ToNumerics(), headTop.ToNumerics(), axis.ToNumerics()))
                    continue;
                float d = Vector3.Distance(mouth, headTop);
                if (d < bestDist)
                {
                    best = rt.Hole;
                    bestDist = d;
                }
            }
            return best;
        }

        private void Attach(string hole, BoltSet set, WrenchEngageResult check)
        {
            _wrenchHole = hole;
            _refusedHole = null;
            _swing = new RatchetSwing(set.Spec.Wrench);
            _hapticAccumulator = 0f;
            HolePose(set.Get(hole).Spec, out var holePos, out _, out var axis);
            var first = _swing.Update(axis.ToNumerics(), (_wrench.HandPosition - holePos).ToNumerics());
            _lastHandleAngle = first.HasAngle ? first.HandleAngleDegrees : AngleOfWrenchHandle(axis);
            _wrench.SetAttached(true);
            PoseAttachedWrench(holePos, axis);
            _wrench.Pulse(0.4f, 0.05f);
            _audio.transform.position = holePos;
            _audio.PlayOneShot(_click, 0.4f);
            var bolt = set.Get(hole);
            int n = set.Pattern.SequenceNumber(hole);
            if (Learn)
                _training.ShowStatus($"Wrench on bolt {n}. Swing the handle clockwise to {bolt.Spec.TorqueSpecNm:0.0} N·m; stop at the click.", good: true);
            else if (check.Check == WrenchCheck.OutOfOrderAllowed)
                _training.NotifyProgressChanged(); // logged; Test mode does not explain
        }

        /// <summary>Hand directly on the axis at attach time: use the wrench's own handle direction instead.</summary>
        private float AngleOfWrenchHandle(Vector3 axis) =>
            AxisAngle.TryAngleAbout(axis.ToNumerics(), _wrench.transform.forward.ToNumerics(), out float a) ? a : 0f;

        private void Detach()
        {
            _wrenchHole = null;
            _swing = null;
            _wrench.SetAttached(false);
        }

        private void OnWrenchReleased(WrenchAdapter wrench)
        {
            if (_wrenchHole != null) Detach();
            _refusedHole = null;
        }

        /// <summary>One debug-panel line: wrench state, attached bolt, handle angle, torque.</summary>
        public string DescribeWrench()
        {
            if (_wrench == null) return "Wrench: none";
            var set = Set;
            if (_wrenchHole != null && set != null)
            {
                var b = set.Get(_wrenchHole);
                return $"Wrench: ATTACHED bolt {set.Pattern.SequenceNumber(_wrenchHole)} ({_wrenchHole}) | " +
                       $"angle {_lastHandleAngle:0}° | torque {b.TorqueNm:0.0} N·m | {b.State}";
            }
            if (!_wrench.IsHeld) return "Wrench: not held";
            string nearest = "";
            if (set != null)
            {
                float best = float.MaxValue;
                string hole = null;
                foreach (var b in set.Bolts)
                {
                    HolePose(b.Spec, out var p, out _, out var axis);
                    float d = Vector3.Distance(_wrench.SocketMouth, p + axis * _boltHeadHeightMetres);
                    if (d < best) { best = d; hole = b.Spec.HoleId; }
                }
                if (hole != null) nearest = $" | nearest head {hole} {best * 100f:0.0} cm, tilt {Vector3.Angle(_wrench.SocketAxis, Vector3.up):0}°";
            }
            return $"Wrench: held ({(_wrench.HeldByHand ? "hand" : "controller")}), not on a bolt{nearest}" +
                   (_refusedHole != null ? $" | refused {_refusedHole}" : "");
        }

        private void SpinBolt(string hole, float delta, Vector3 holePos, Quaternion holeRot, Vector3 axis)
        {
            foreach (var rt in _runtimes)
            {
                if (rt.Hole != hole) continue;
                rt.SpinDegrees += delta;
                rt.Adapter.SetPose(holePos, holeRot * Quaternion.Euler(0f, rt.SpinDegrees, 0f));
                return;
            }
        }

        /// <summary>Controller haptics: a tick every few degrees, stronger as torque approaches spec.</summary>
        private void RampHaptics(BoltTightening bolt, float delta)
        {
            _hapticAccumulator += delta;
            if (_hapticAccumulator < _hapticStepDegrees) return;
            _hapticAccumulator = 0f;
            float k = Mathf.Clamp01(bolt.TorqueNm / bolt.Spec.TorqueSpecNm);
            _wrench.Pulse(Mathf.Lerp(0.05f, 0.45f, k * k), 0.02f);
        }

        private void OnTorqueStateChanged(BoltSet set, BoltTightening bolt, BoltState state)
        {
            int n = set.Pattern.SequenceNumber(bolt.Spec.HoleId);
            if (state == BoltState.Torqued)
            {
                _wrench.Pulse(1f, 0.12f);
                _audio.transform.position = _wrench.SocketMouth;
                _audio.PlayOneShot(_click, 1f);
                var next = set.NextInPattern;
                string msg;
                if (set.IsComplete) msg = "All bolts torqued.";
                else if (Learn) msg = $"Bolt {n} torqued ({bolt.TorqueNm:0.0} N·m). Next: bolt {set.Pattern.SequenceNumber(next)}.";
                else msg = "Bolt torqued.";
                _training.ShowStatus(msg, good: true);
            }
            else if (state == BoltState.OverTorqued)
            {
                _wrench.Pulse(1f, 0.4f);
                _training.ShowStatus(Learn
                    ? $"Over-torqued: bolt {n} at {bolt.TorqueNm:0.0} N·m (max {bolt.Spec.MaxTorqueNm:0.0}). Stop at the click."
                    : "Over-torqued.", good: false);
            }
            _training.NotifyProgressChanged();
        }

        private void ShowGauge(BoltSet set)
        {
            if (_gauge == null || set == null) return;
            if (_wrenchHole == null) { _gauge.ShowIdle(set.Bolts[0].Spec.TorqueSpecNm); return; }
            var bolt = set.Get(_wrenchHole);
            _gauge.Show(bolt.TorqueNm, bolt.Spec.TorqueSpecNm, bolt.State);
        }

        // ---------------- Learn: pattern numbers ----------------

        private void UpdateNumbers()
        {
            if (_numberPrefab == null) return;
            var set = Set;
            bool show = Learn && Progress.AreBoltsAvailable(set) && !set.IsComplete;
            string next = set.AllSeated ? set.NextInPattern : null; // highlight only once torquing starts
            foreach (var bolt in set.Bolts)
            {
                var hole = bolt.Spec.HoleId;
                if (!_numbers.TryGetValue(hole, out var view))
                {
                    if (!show) continue;
                    view = Instantiate(_numberPrefab, transform);
                    view.name = "Pattern Number " + hole;
                    _numbers[hole] = view;
                }
                if (!show) { view.Hide(); continue; }
                HolePose(bolt.Spec, out var pos, out _, out var axis);
                var look = bolt.State == BoltState.OverTorqued || bolt.State == BoltState.CrossThreaded ? FloatingNumberView.Look.Fault
                    : bolt.IsFinished ? FloatingNumberView.Look.Done
                    : hole == next ? FloatingNumberView.Look.Next
                    : FloatingNumberView.Look.Normal;
                view.Show(set.Pattern.SequenceNumber(hole), pos + axis * _numberHeightMetres, look);
            }
        }

        // ---------------- Hand-start ----------------

        private void UpdateHeldBolt(BoltRuntime rt)
        {
            var set = Set;
            if (!Progress.AreBoltsAvailable(set)) return;

            if (rt.Hole == null)
            {
                // Carried freely: the hole it would drop into if let go now (its Learn ghost turns green).
                var hole = DropHoleFor(rt, set);
                if (hole != null) _readyHole = hole;
                return;
            }

            var bolt = set.Get(rt.Hole);
            HolePose(bolt.Spec, out var holePos, out var holeRot, out var axis);
            switch (bolt.State)
            {
                case BoltState.Started:
                    // Twisting the bolt in its hole: wrist degrees about the hole axis, geared in Core.
                    var virtualRot = rt.Adapter.VirtualRotation;
                    float twist = rt.Tracker.Update(axis.ToNumerics(), (virtualRot * Vector3.forward).ToNumerics());
                    if (twist > 0f)
                    {
                        rt.SpinDegrees += twist * bolt.Spec.HandTurnGearing;
                        set.HandTurn(rt.Hole, twist, 0f, _training.RunTime);
                    }
                    PlaceThreaded(rt, bolt, holePos, holeRot, axis);
                    if (bolt.State == BoltState.Seated) OnSeated(rt, set);
                    return;

                case BoltState.CrossThreaded:
                    if (PulledOut(rt))
                        TakeOut(rt, set, Learn ? "Bolt removed. Drop it in again, upright over the hole." : null);
                    return;
            }
        }

        /// <summary>Free hole within the mode's capture zone of the bolt's shank tip and axis, or null.</summary>
        private string DropHoleFor(BoltRuntime rt, BoltSet set)
        {
            var t = rt.Adapter.transform;
            var tip = t.position - t.up * _shankLengthMetres;
            return set.FindDropHole(tip.ToNumerics(), t.up.ToNumerics(), Progress.CaptureZone);
        }

        /// <summary>
        /// A free bolt let go near a hole drops in: straight enough it stands upright, Started (twist to thread);
        /// more tilted it sits crooked, cross-threaded (pull it out). Otherwise it just falls.
        /// </summary>
        private void TryDropIn(BoltRuntime rt, BoltSet set)
        {
            if (!Progress.AreBoltsAvailable(set)) return;
            var hole = DropHoleFor(rt, set);
            if (hole == null) return;

            float tilt = set.TiltDegrees(hole, rt.Adapter.transform.up.ToNumerics());
            var state = set.DropIn(hole, tilt, _training.RunTime);
            rt.Hole = hole;
            rt.SpinDegrees = 0f;
            rt.Tracker.Reset();
            rt.Adapter.SetLockedInPlace(true);
            var bolt = set.Get(hole);
            HolePose(bolt.Spec, out var holePos, out var holeRot, out var axis);
            if (state == BoltState.Started)
            {
                PlaceThreaded(rt, bolt, holePos, holeRot, axis);
                _training.ShowStatus(Learn
                    ? $"Bolt in. Grab it and twist clockwise to thread it ({bolt.Spec.ThreadTurnsToSeat:0} turns, about {TwistsToSeat(bolt.Spec)} wrist twists)."
                    : "Bolt in.", good: true);
            }
            else
            {
                // Sits crooked in the hole at the angle it was dropped.
                var crooked = Quaternion.FromToRotation(axis, rt.Adapter.transform.up) * holeRot;
                rt.Adapter.SetPose(holePos + axis * _engageHeightMetres, crooked);
                _training.ShowStatus(Learn
                    ? $"Cross-threaded: the bolt went in {tilt:0}° off straight (max {bolt.Spec.MaxStartTiltDegrees:0}°). Grab it, pull it out and drop it in upright."
                    : "Bolt jammed: cross-threaded.", good: false);
            }
            _training.NotifyProgressChanged();
        }

        private static int TwistsToSeat(BoltSpec spec) =>
            Mathf.Max(1, Mathf.RoundToInt(spec.ThreadTurnsToSeat * 360f / spec.HandTurnGearing / 135f)); // ~135° per wrist twist

        private void PlaceThreaded(BoltRuntime rt, BoltTightening bolt, Vector3 holePos, Quaternion holeRot, Vector3 axis)
        {
            float height = _engageHeightMetres * (1f - bolt.SeatProgress01);
            rt.Adapter.SetPose(holePos + axis * height, holeRot * Quaternion.Euler(0f, rt.SpinDegrees, 0f));
        }

        private void OnSeated(BoltRuntime rt, BoltSet set)
        {
            rt.Adapter.Pulse(0.5f, 0.08f);
            rt.Adapter.ForceRelease();
            rt.Adapter.SetGrabbable(false); // finger-tight: only the wrench works on it now
            int n = set.Pattern.SequenceNumber(rt.Hole);
            _training.ShowStatus(Learn
                ? $"Bolt {n} seated ({set.SeatedCount}/{set.Count}). " + (set.AllSeated ? "All seated: now torque in the cross pattern." : "Hand-start the rest.")
                : $"Bolt seated ({set.SeatedCount}/{set.Count}).", good: true);
            _training.NotifyProgressChanged();
        }

        private void TakeOut(BoltRuntime rt, BoltSet set, string message)
        {
            set.Remove(rt.Hole);
            rt.Hole = null;
            rt.Tracker.Reset();
            rt.Adapter.SetTracking(true, true);
            rt.Adapter.SetLockedInPlace(false);
            rt.Adapter.RecaptureGrip();
            if (message != null) _training.ShowStatus(message, good: true);
            _training.NotifyProgressChanged();
        }

        private bool PulledOut(BoltRuntime rt) =>
            Vector3.Distance(rt.Adapter.HandPosition, rt.Adapter.transform.position) > _pullOutMetres;

        private void OnBoltGrabbed(BoltAdapter adapter)
        {
            if (!_byAdapter.TryGetValue(adapter, out var rt) || rt.Hole == null) return;
            // Picking a started or jammed bolt up again: it stays in its hole; twisting continues from here.
            adapter.SetTracking(false, false);
            rt.Tracker.Reset();
        }

        private void OnBoltReleased(BoltAdapter adapter)
        {
            if (!_byAdapter.TryGetValue(adapter, out var rt) || Set == null) return;
            if (rt.Hole == null) TryDropIn(rt, Set);
            // A bolt already in a hole (started / cross-threaded) stays there, locked in place.
        }

        private bool IsSeatedInHole(BoltRuntime rt) => rt.Hole != null && Set != null && Set.Get(rt.Hole).IsSeated;

        private void UpdateTint(BoltRuntime rt)
        {
            if (rt.Tint == null) return;
            if (rt.Hole == null) { rt.Tint.SetTint(null); return; }
            switch (Set.Get(rt.Hole).State)
            {
                case BoltState.Started:
                case BoltState.Seated: rt.Tint.SetTint(_startedColor); break;
                case BoltState.Torqued: rt.Tint.SetTint(_torquedColor); break;
                case BoltState.OverTorqued:
                case BoltState.CrossThreaded: rt.Tint.SetTint(_faultColor); break;
                default: rt.Tint.SetTint(null); break;
            }
        }

        private static void HolePose(BoltSpec spec, out Vector3 position, out Quaternion rotation, out Vector3 axis)
        {
            position = spec.HolePose.Position.ToUnity();
            rotation = spec.HolePose.Rotation.ToUnity();
            axis = rotation * Vector3.up;
        }

        // ---------------- Labels (Learn) ----------------

        /// <summary>Label for the held wrench or bolt (only while held), from the module data.</summary>
        public bool TryGetLabel(out string title, out string hint, out Vector3 position)
        {
            title = hint = null;
            position = default;
            var data = Data;
            if (data == null) return false;
            if (_wrench != null && _wrench.IsHeld)
            {
                title = data.wrenchName;
                hint = data.wrenchHint;
                position = _wrench.transform.position + Vector3.up * 0.08f;
                return true;
            }
            BoltRuntime focus = null;
            foreach (var rt in _runtimes) if (rt.Adapter.IsHeld) { focus = rt; break; }
            if (focus == null) return false;
            title = data.boltName;
            hint = data.boltHint;
            position = focus.Adapter.transform.position + Vector3.up * 0.06f;
            return true;
        }
    }
}
