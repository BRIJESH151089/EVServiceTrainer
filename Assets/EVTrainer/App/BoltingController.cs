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

        [Header("Hand-start")]
        [Tooltip("A held bolt within this distance of a free hole's axis snaps onto it.")]
        [SerializeField, Min(0.001f)] private float _captureRadiusMetres = 0.02f;
        [SerializeField, Min(0.001f)] private float _captureHeightMetres = 0.06f;
        [Tooltip("More tilt than this and the bolt does not enter the hole at all.")]
        [SerializeField, Range(0f, 45f)] private float _maxEntryTiltDegrees = 20f;
        [Tooltip("Height of the bolt head above the surface when the thread first engages.")]
        [SerializeField, Min(0f)] private float _engageHeightMetres = 0.012f;
        [Tooltip("Pulling the hand this far from a jammed (or just inserted) bolt takes it out of the hole.")]
        [SerializeField, Min(0.01f)] private float _pullOutMetres = 0.07f;

        [Header("Torque wrench")]
        [SerializeField] private WrenchAdapter _wrench;
        [SerializeField] private TorqueGaugeView _gauge;
        [Tooltip("The wrench fits when its socket is this close to a bolt head...")]
        [SerializeField, Min(0.001f)] private float _fitRadiusMetres = 0.025f;
        [Tooltip("...and its axis is within this angle of the bolt axis.")]
        [SerializeField, Range(0f, 90f)] private float _fitMaxAngleDegrees = 35f;
        [Tooltip("Height of the bolt head above the surface (the socket sits on top of it).")]
        [SerializeField, Min(0f)] private float _boltHeadHeightMetres = 0.007f;
        [Tooltip("Pulling the hand this far off the swing plane, or this far from the bolt, takes the wrench off.")]
        [SerializeField, Min(0.02f)] private float _detachOffPlaneMetres = 0.12f;
        [SerializeField, Min(0.1f)] private float _detachRadiusMetres = 0.5f;
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
        private readonly AxisRotationTracker _wrenchTracker = new AxisRotationTracker();
        private bool _grabbableApplied;
        private bool _grabbable;
        private string _wrenchHole;        // bolt the wrench is fitted on
        private string _refusedHole;       // last bolt the wrench was refused on (don't repeat the message while it stays there)
        private float _hapticAccumulator;
        private AudioSource _audio;
        private AudioClip _click;

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

            foreach (var rt in _runtimes)
            {
                if (rt.Adapter.IsHeld) UpdateHeldBolt(rt);
                UpdateTint(rt);
            }
            UpdateWrench();
            UpdateNumbers();
        }

        // ---------------- Torque wrench ----------------

        private void UpdateWrench()
        {
            if (_wrench == null) return;
            var set = Set;
            if (!_wrench.IsHeld || !Progress.AreBoltsAvailable(set))
            {
                if (_wrenchHole != null) Unfit();
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
                Fit(hole, set, check);
            }

            // Fitted: the wrench pivots on the bolt; the handle points at the hand; clockwise swing turns the bolt.
            var bolt = set.Get(_wrenchHole);
            HolePose(bolt.Spec, out var holePos, out var holeRot, out var axis);
            var pivot = holePos + axis * _boltHeadHeightMetres;
            var toHand = _wrench.HandPosition - pivot;
            float offPlane = Vector3.Dot(toHand, axis);
            var inPlane = toHand - axis * offPlane;
            if (Mathf.Abs(offPlane) > _detachOffPlaneMetres || inPlane.magnitude > _detachRadiusMetres)
            {
                Unfit(); // pulled away: the wrench comes off (keeps the last bolt message on the HUD)
                return;
            }
            if (inPlane.sqrMagnitude > 1e-6f) _wrench.SetPose(pivot, Quaternion.LookRotation(inPlane.normalized, axis));

            float delta = _wrenchTracker.Update(axis.ToNumerics(), inPlane.ToNumerics());
            if (delta > 0f)
            {
                var before = bolt.State;
                var after = set.WrenchTurn(_wrenchHole, delta, _training.RunTime);
                SpinBolt(_wrenchHole, delta, holePos, holeRot, axis);
                RampHaptics(bolt, delta);
                if (after != before) OnTorqueStateChanged(set, bolt, after);
            }
            ShowGauge(set);
        }

        private string FindBoltUnderSocket(BoltSet set)
        {
            string best = null;
            float bestDist = _fitRadiusMetres;
            foreach (var rt in _runtimes)
            {
                if (rt.Hole == null) continue;
                var spec = set.Get(rt.Hole).Spec;
                HolePose(spec, out var holePos, out _, out var axis);
                float d = Vector3.Distance(_wrench.SocketPosition, holePos + axis * _boltHeadHeightMetres);
                if (d <= bestDist && Vector3.Angle(_wrench.SocketAxis, axis) <= _fitMaxAngleDegrees)
                {
                    best = rt.Hole;
                    bestDist = d;
                }
            }
            return best;
        }

        private void Fit(string hole, BoltSet set, WrenchEngageResult check)
        {
            _wrenchHole = hole;
            _refusedHole = null;
            _wrenchTracker.Reset();
            _hapticAccumulator = 0f;
            _wrench.SetFitted(true);
            _wrench.Pulse(0.4f, 0.05f);
            _audio.transform.position = _wrench.SocketPosition;
            _audio.PlayOneShot(_click, 0.4f);
            var bolt = set.Get(hole);
            int n = set.Pattern.SequenceNumber(hole);
            if (Learn)
                _training.ShowStatus($"Wrench on bolt {n}. Swing the handle clockwise to {bolt.Spec.TorqueSpecNm:0.0} N·m; stop at the click.", good: true);
            else if (check.Check == WrenchCheck.OutOfOrderAllowed)
                _training.NotifyProgressChanged(); // logged; Test mode does not explain
        }

        private void Unfit()
        {
            _wrenchHole = null;
            _wrenchTracker.Reset();
            _wrench.SetFitted(false);
        }

        private void OnWrenchReleased(WrenchAdapter wrench)
        {
            if (_wrenchHole != null) Unfit();
            _refusedHole = null;
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
                _audio.transform.position = _wrench.SocketPosition;
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

            if (rt.Hole == null && !TryCapture(rt, set)) return;

            var bolt = set.Get(rt.Hole);
            HolePose(bolt.Spec, out var holePos, out var holeRot, out var axis);
            var virtualRot = rt.Adapter.VirtualRotation;
            float tilt = Vector3.Angle(virtualRot * Vector3.up, axis);

            switch (bolt.State)
            {
                case BoltState.Loose:
                    if (tilt > _maxEntryTiltDegrees || PulledOut(rt))
                    {
                        TakeOut(rt, set, message: null);
                        return;
                    }
                    rt.Adapter.SetPosition(holePos + axis * _engageHeightMetres); // on the axis; tilt follows the hand
                    float firstTwist = rt.Tracker.Update(axis.ToNumerics(), (virtualRot * Vector3.forward).ToNumerics());
                    if (firstTwist <= 0f) return;
                    var state = set.HandTurn(rt.Hole, firstTwist, tilt, _training.RunTime);
                    if (state == BoltState.CrossThreaded)
                    {
                        rt.Adapter.SetTracking(false, false); // jammed at its crooked angle
                        rt.Adapter.SetLockedInPlace(true);
                        rt.Adapter.Pulse(0.9f, 0.25f);
                        _training.ShowStatus(Learn
                            ? $"Cross-threaded: the bolt started {tilt:0}° off straight (max {bolt.Spec.MaxStartTiltDegrees:0}°). Pull it out and start it again, straight."
                            : "Bolt jammed: cross-threaded.", good: false);
                        _training.NotifyProgressChanged();
                    }
                    else if (state == BoltState.Started)
                    {
                        rt.Adapter.SetTracking(false, false); // the thread now holds it straight
                        rt.Adapter.SetLockedInPlace(true);
                        rt.SpinDegrees = firstTwist;
                        PlaceThreaded(rt, bolt, holePos, holeRot, axis);
                        if (Learn) _training.ShowStatus($"Thread caught. Keep twisting clockwise ({bolt.Spec.ThreadTurnsToSeat:0} turns).", good: true);
                    }
                    return;

                case BoltState.Started:
                    float twist = rt.Tracker.Update(axis.ToNumerics(), (virtualRot * Vector3.forward).ToNumerics());
                    if (twist > 0f)
                    {
                        rt.SpinDegrees += twist;
                        set.HandTurn(rt.Hole, twist, tilt, _training.RunTime);
                    }
                    PlaceThreaded(rt, bolt, holePos, holeRot, axis);
                    if (bolt.State == BoltState.Seated) OnSeated(rt, set);
                    return;

                case BoltState.CrossThreaded:
                    if (PulledOut(rt))
                        TakeOut(rt, set, Learn ? "Bolt removed. Start it again, straight over the hole." : null);
                    return;
            }
        }

        private bool TryCapture(BoltRuntime rt, BoltSet set)
        {
            var hole = set.FindFreeHole(rt.Adapter.transform.position.ToNumerics(), _captureRadiusMetres, _captureHeightMetres);
            if (hole == null) return false;
            HolePose(set.Get(hole).Spec, out _, out _, out var axis);
            if (Vector3.Angle(rt.Adapter.VirtualRotation * Vector3.up, axis) > _maxEntryTiltDegrees) return false; // too crooked to enter
            set.Insert(hole);
            rt.Hole = hole;
            rt.SpinDegrees = 0f;
            rt.Tracker.Reset();
            rt.Adapter.SetTracking(false, true); // position pinned to the hole axis, tilt still follows the hand
            return true;
        }

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
            if (!_byAdapter.TryGetValue(adapter, out var rt) || rt.Hole == null || Set == null) return;
            var state = Set.Get(rt.Hole).State;
            if (state == BoltState.Loose) TakeOut(rt, Set, null); // never turned: it just drops
            // Started / cross-threaded / seated bolts stay in the hole (locked in place).
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

        /// <summary>Label for the held (else hovered) bolt, from the module data.</summary>
        public bool TryGetLabel(out string title, out string hint, out Vector3 position)
        {
            title = hint = null;
            position = default;
            var data = Data;
            if (data == null) return false;
            if (_wrench != null && (_wrench.IsHeld || _wrench.IsHovered))
            {
                title = data.wrenchName;
                hint = data.wrenchHint;
                position = _wrench.transform.position + Vector3.up * 0.08f;
                return true;
            }
            BoltRuntime focus = null;
            foreach (var rt in _runtimes) if (rt.Adapter.IsHeld) { focus = rt; break; }
            if (focus == null) foreach (var rt in _runtimes) if (rt.Adapter.IsHovered) { focus = rt; break; }
            if (focus == null) return false;
            title = data.boltName;
            hint = data.boltHint;
            position = focus.Adapter.transform.position + Vector3.up * 0.06f;
            return true;
        }
    }
}
