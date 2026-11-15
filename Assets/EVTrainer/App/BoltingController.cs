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
        [SerializeField] private Color _ghostReadyColor = new Color(0.3f, 1f, 0.45f, 0.8f);
        [Tooltip("Learn: label prefab for the short \"keep the bolt straight\" tip (text from the module data).")]
        [SerializeField] private PartLabelView _tipLabelPrefab;
        [SerializeField, Min(0.5f)] private float _tipSeconds = 4f;
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
        [Tooltip("Hand-start: a thread tick (sound + light haptic) every this many degrees of bolt spin.")]
        [SerializeField, Min(5f)] private float _threadTickDegrees = 45f;
        [Tooltip("A haptic tick every this many degrees of swing, stronger as torque rises toward spec.")]
        [SerializeField, Min(0.5f)] private float _hapticStepDegrees = 4f;
        [Tooltip("Let go below this height (the bench top), the wrench goes back to its stand instead of floating.")]
        [SerializeField] private float _minFloatHeightMetres = 0.92f;

        [Header("Torque guidance (ghost wrench + arrow in Learn; progress ring in both modes)")]
        [Tooltip("Scene object: the wrench as a ghost, shown seated on the next bolt in Learn.")]
        [SerializeField] private Transform _ghostWrench;
        [SerializeField] private SwingArrowView _swingArrow;
        [SerializeField] private TorqueRingView _torqueRing;
        [Tooltip("The finished bolt keeps its green (or red) ring this long after the wrench comes off.")]
        [SerializeField, Min(0f)] private float _doneShowSeconds = 2.5f;

        [Header("Learn: pattern numbers")]
        [SerializeField] private FloatingNumberView _numberPrefab;
        [SerializeField, Min(0f)] private float _numberHeightMetres = 0.05f;
        [Tooltip("While the wrench is on a bolt, that bolt's number sits this far above the top of the wrench head.")]
        [SerializeField, Min(0f)] private float _numberAboveWrenchMetres = 0.008f;

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
            public float ThreadTickDegrees; // bolt spin since the last thread tick
        }

        private readonly List<BoltRuntime> _runtimes = new List<BoltRuntime>();
        private readonly Dictionary<BoltAdapter, BoltRuntime> _byAdapter = new Dictionary<BoltAdapter, BoltRuntime>();
        private readonly Dictionary<string, FloatingNumberView> _numbers = new Dictionary<string, FloatingNumberView>();
        private readonly Dictionary<string, PartTint> _ghosts = new Dictionary<string, PartTint>();
        private string _readyHole; // free hole a held bolt would drop into (its ghost turns green)
        private PartLabelView _tipLabel;
        private float _tipHideTime;
        private RatchetSwing _swing;
        private string _doneHole;   // bolt just finished (its ring shows done for a moment)
        private float _doneUntil;
        private ToolRelease? _wrenchRelease; // how the wrench was last let go (debug panel)
        private PartTint _wrenchTint;
        private bool _grabbableApplied;
        private bool _grabbable;
        private string _wrenchHole;        // bolt the wrench is fitted on
        private string _refusedHole;       // last bolt the wrench was refused on (don't repeat the message while it stays there)
        private float _hapticAccumulator;
        private float _lastHandleAngle;
        private float _wrenchTopAboveOrigin; // top of the wrench mesh along its socket axis, from its origin (measured once)
        private string _boltGroupName;     // label texts are refreshed every few frames: build each once
        private readonly GroupTextCache _groupTexts = new GroupTextCache();

        private AssemblyProgress Progress => _training != null ? _training.Progress : null;
        private BoltSet Set => Progress != null && Progress.BoltSets.Count > 0 ? Progress.BoltSets[0] : null;
        private bool Learn => Progress != null && Progress.Mode == TrainingMode.Learn;
        private HapticProfiles Haptics => _training != null ? _training.Haptics : HapticProfiles.Default;
        private AssemblyModuleDefinition.BoltSetEntry Data => _module != null && _module.BoltSets.Count > 0 ? _module.BoltSets[0] : null;

        private void Awake()
        {
            foreach (var b in _bolts)
            {
                var rt = new BoltRuntime { Adapter = b, Tint = b.GetComponent<PartTint>() };
                _runtimes.Add(rt);
                _byAdapter[b] = rt;
            }

            if (_wrench != null)
            {
                _wrenchTint = _wrench.GetComponent<PartTint>();
                if (_wrenchTint == null) _wrenchTint = _wrench.gameObject.AddComponent<PartTint>();
                _wrenchTopAboveOrigin = TopAboveOrigin(_wrench.transform);
            }
        }

        /// <summary>Highest point of <paramref name="root"/>'s meshes along its up axis (its socket axis), from its origin.</summary>
        private static float TopAboveOrigin(Transform root)
        {
            float top = 0f;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    top = Mathf.Max(top, root.InverseTransformPoint(filter.transform.TransformPoint(corner)).y);
                }
            }
            return top;
        }

        private void OnEnable()
        {
            foreach (var b in _bolts)
            {
                b.Grabbed += OnBoltGrabbed;
                b.Released += OnBoltReleased;
            }
            if (_wrench != null)
            {
                _wrench.Released += OnWrenchReleased;
                _wrench.Grabbed += OnWrenchGrabbed;
            }
        }

        private void OnDisable()
        {
            foreach (var b in _bolts)
            {
                b.Grabbed -= OnBoltGrabbed;
                b.Released -= OnBoltReleased;
            }
            if (_wrench != null)
            {
                _wrench.Released -= OnWrenchReleased;
                _wrench.Grabbed -= OnWrenchGrabbed;
            }
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
                if (rt.Tint != null) rt.Tint.SetHighlighted(rt.Adapter.IsHovered && !rt.Adapter.IsHeld);
            }
            if (_wrenchTint != null) _wrenchTint.SetHighlighted(_wrench.IsHovered && !_wrench.IsHeld);
            UpdateWrench();
            UpdateNumbers();
            UpdateGhosts();
            UpdateGuidance();
            if (_tipLabel != null && Time.time > _tipHideTime) _tipLabel.Hide();
        }

        // ---------------- Torque guidance and progress ring ----------------

        /// <summary>
        /// Learn: a ghost wrench seated on the next bolt in the pattern (handle towards the trainee) and an arrow showing
        /// the clockwise swing, both hidden while the real wrench is on a bolt. Both modes: a ring round the bolt being
        /// torqued fills with torque up to the green target zone; it shows done (or over) for a moment afterwards.
        /// </summary>
        private void UpdateGuidance()
        {
            var set = Set;
            bool torquing = Progress.AreBoltsAvailable(set) && set.AllSeated && !set.IsComplete;
            string next = torquing && Learn && _wrenchHole == null ? set.NextInPattern : null;
            if (next != null)
            {
                HolePose(set.Get(next).Spec, out var pos, out _, out var axis);
                var toUser = TowardUser(pos, axis);
                if (_ghostWrench != null)
                {
                    if (!_ghostWrench.gameObject.activeSelf) _ghostWrench.gameObject.SetActive(true);
                    float mouthToOrigin = _wrench != null ? _wrench.MouthToOrigin : 0f;
                    _ghostWrench.SetPositionAndRotation(pos + axis * mouthToOrigin, Quaternion.LookRotation(toUser, axis));
                }
                if (_swingArrow != null) _swingArrow.Show(pos, axis, toUser);
            }
            else
            {
                if (_ghostWrench != null && _ghostWrench.gameObject.activeSelf) _ghostWrench.gameObject.SetActive(false);
                if (_swingArrow != null) _swingArrow.Hide();
            }

            if (_torqueRing == null) return;
            string ringHole = _wrenchHole ?? (_doneHole != null && Time.time < _doneUntil ? _doneHole : null);
            if (ringHole == null)
            {
                _torqueRing.Hide();
                return;
            }
            var bolt = set.Get(ringHole);
            HolePose(bolt.Spec, out var center, out _, out var boltAxis);
            float scale = Data != null ? Data.torqueRingFullScale : TorqueDial.DefaultFullScaleFactor;
            var (zoneStart, zoneEnd) = TorqueDial.Zone01(bolt.Spec, scale);
            var look = bolt.State == BoltState.OverTorqued ? TorqueRingView.Look.Over
                : bolt.State == BoltState.Torqued ? TorqueRingView.Look.Done
                : TorqueRingView.Look.Rising;
            _torqueRing.Show(center, boltAxis, TowardUser(center, boltAxis), TorqueDial.Fill01(bolt.Spec, bolt.TorqueNm, scale),
                zoneStart, zoneEnd, look);
        }

        private void MarkDone(string hole)
        {
            _doneHole = hole;
            _doneUntil = Time.time + _doneShowSeconds;
        }

        /// <summary>Direction in the bolt's plane from the bolt towards the trainee (where a handle naturally starts).</summary>
        private static Vector3 TowardUser(Vector3 boltPosition, Vector3 axis)
        {
            var head = Camera.main != null ? Camera.main.transform.position : boltPosition + Vector3.back;
            var dir = Vector3.ProjectOnPlane(head - boltPosition, axis);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.ProjectOnPlane(Vector3.back, axis);
            return dir.normalized;
        }

        // ---------------- Learn: bolt ghosts ----------------

        private void UpdateGhosts()
        {
            if (_boltGhostPrefab == null) return;
            var set = Set;
            bool show = Learn && Progress.AreBoltsAvailable(set);
            var bolts = set.Bolts;
            for (int b = 0; b < bolts.Count; b++) // index loop: foreach over the interface would allocate every frame
            {
                var bolt = bolts[b];
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
            // Not held: resting on the stand, floating where it was let go, or parked on a bolt (the adapter keeps its pose).
            if (!_wrench.IsHeld || !Progress.AreBoltsAvailable(set))
            {
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
                    _training.PlaySound(FeedbackCue.Reject, _wrench.SocketMouth);
                    _wrench.Pulse(Haptics.ForCue(FeedbackCue.Reject));
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
                if (bolt.IsFinished) MarkDone(_wrenchHole); // lifted off a finished bolt (Test): ring shows done a moment
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
                if (bolt.IsFinished && set.AutoDetachWhenFinished)
                {
                    // Learn: done, the wrench comes off by itself and stays in the hand, ready for the next bolt;
                    // the ring stays a moment on the finished bolt (green with a check, or red). Test: it stays on
                    // until the trainee lifts it off; swinging on past the window over-torques.
                    MarkDone(_wrenchHole);
                    Detach();
                    ShowGauge(set);
                    return;
                }
            }
            if (step.BackClicks > 0)
            {
                _training.PlaySound(FeedbackCue.RatchetBack, headTop);
                _wrench.Pulse(Haptics.ForCue(FeedbackCue.RatchetBack));
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
                if (rt.Hole == null || set.Get(rt.Hole).IsFinished) continue; // a finished bolt never takes the wrench again
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
            _wrench.Pulse(Haptics.ForCue(FeedbackCue.WrenchAttach));
            _training.PlaySound(FeedbackCue.WrenchAttach, holePos);
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

        /// <summary>
        /// Tools never fall (Core <see cref="ToolReleaseRule"/>): let go on an unfinished bolt it stays parked there; let go in
        /// reach it floats where it is; let go low (below the bench top) or far away it glides back to the stand.
        /// </summary>
        private void OnWrenchReleased(WrenchAdapter wrench)
        {
            _refusedHole = null;
            var set = Set;
            var data = Data;
            bool attached = _wrenchHole != null && set != null;
            bool finished = attached && set.Get(_wrenchHole).IsFinished;
            var position = _wrench.transform.position;
            var head = Camera.main != null ? Camera.main.transform.position : position;
            var release = ToolReleaseRule.ForWrench(attached, finished, position.y, _minFloatHeightMetres,
                Vector3.Distance(head, position), data != null ? data.toolFloatMaxDistanceMetres : 1f);
            _wrenchRelease = release;
            switch (release)
            {
                case ToolRelease.ParkOnBolt:
                    _swing = null; // restarts from the handle when grabbed again
                    if (Learn) _training.ShowStatus($"Wrench left on bolt {set.Pattern.SequenceNumber(_wrenchHole)}. Grab the handle to continue.", good: true);
                    break;
                case ToolRelease.FloatInPlace:
                    if (attached) Detach();
                    _wrench.HoldStill();
                    break;
                default:
                    if (attached) Detach();
                    _wrench.ReturnHome(data != null ? data.toolReturnSeconds : 0.6f);
                    break;
            }
        }

        /// <summary>Picked up again while parked on a bolt: continue from where the handle is.</summary>
        private void OnWrenchGrabbed(WrenchAdapter wrench)
        {
            _wrenchRelease = null;
            _training.PlaySound(FeedbackCue.PickUp, wrench.transform.position);
            wrench.Pulse(Haptics.ForCue(FeedbackCue.PickUp));
            if (_wrenchHole != null && Set != null) _swing = new RatchetSwing(Set.Spec.Wrench);
        }

        /// <summary>The bolt standing in a hole, or null.</summary>
        public BoltAdapter BoltInHole(string hole)
        {
            foreach (var rt in _runtimes) if (rt.Hole == hole) return rt.Adapter;
            return null;
        }

        /// <summary>
        /// Review (exploded view / disassembly) on or off: the wrench comes off any bolt (back to its stand unless held)
        /// and cannot be picked up while the assembly is apart.
        /// </summary>
        public void SetReviewing(bool reviewing)
        {
            if (_wrench == null) return;
            if (reviewing && _wrenchHole != null)
            {
                Detach();
                if (!_wrench.IsHeld) _wrench.ReturnHome(Data != null ? Data.toolReturnSeconds : 0.6f);
            }
            _wrench.SetGrabbable(!reviewing && _training != null && _training.IsRunning);
        }

        /// <summary>One debug-panel line: wrench state, attached bolt, handle angle, torque.</summary>
        public string DescribeWrench()
        {
            if (_wrench == null) return "Wrench: none";
            var set = Set;
            if (_wrenchHole != null && set != null)
            {
                var b = set.Get(_wrenchHole);
                return $"Wrench: {(_wrench.IsHeld ? "ATTACHED" : "PARKED on")} bolt {set.Pattern.SequenceNumber(_wrenchHole)} ({_wrenchHole}) | " +
                       $"angle {_lastHandleAngle:0}° | torque {b.TorqueNm:0.0} N·m | {b.State}";
            }
            if (!_wrench.IsHeld)
                return _wrench.IsReturningHome ? "Wrench: returning to the stand"
                    : _wrenchRelease == ToolRelease.FloatInPlace ? "Wrench: floating where it was let go"
                    : "Wrench: not held";
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
            _wrench.Pulse(Haptics.TorqueRamp(bolt.TorqueNm, bolt.Spec.TorqueSpecNm));
        }

        private void OnTorqueStateChanged(BoltSet set, BoltTightening bolt, BoltState state)
        {
            int n = set.Pattern.SequenceNumber(bolt.Spec.HoleId);
            if (state == BoltState.Torqued)
            {
                _wrench.Pulse(Haptics.TorqueClick(wrenchStaysOn: !set.AutoDetachWhenFinished)); // Test: longer, "stop and lift off"
                _training.PlaySound(FeedbackCue.TorqueClick, _wrench.SocketMouth);
                var next = set.NextInPattern;
                string msg;
                if (set.IsComplete) msg = "All bolts torqued.";
                else if (Learn) msg = $"Bolt {n} done ({bolt.TorqueNm:0.0} N·m). Next: bolt {set.Pattern.SequenceNumber(next)}.";
                else msg = "Bolt done."; // Test: no hint about the next bolt
                _training.ShowStatus(msg, good: true);
            }
            else if (state == BoltState.OverTorqued)
            {
                _training.PlaySound(FeedbackCue.OverTorque, _wrench.SocketMouth);
                _wrench.Pulse(Haptics.ForCue(FeedbackCue.OverTorque));
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
            var bolts = set.Bolts;
            for (int b = 0; b < bolts.Count; b++)
            {
                var bolt = bolts[b];
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
                float height = _numberHeightMetres;
                if (hole == _wrenchHole && _wrench != null)
                {
                    // The wrench sits on this bolt (driven to hole + axis * MouthToOrigin): lift the number clear of
                    // the head, its lower edge just above the wrench top.
                    float halfNumber = view.transform is RectTransform rect ? rect.rect.height * 0.5f * view.transform.lossyScale.y : 0f;
                    height = Mathf.Max(height, _wrench.MouthToOrigin + _wrenchTopAboveOrigin + _numberAboveWrenchMetres + halfNumber);
                }
                view.Show(set.Pattern.SequenceNumber(hole), pos + axis * height, look);
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
                        rt.ThreadTickDegrees += twist * bolt.Spec.HandTurnGearing;
                        if (rt.ThreadTickDegrees >= _threadTickDegrees)
                        {
                            rt.ThreadTickDegrees = 0f;
                            _training.PlaySound(FeedbackCue.BoltThreadTick, holePos);
                            rt.Adapter.Pulse(Haptics.ForCue(FeedbackCue.BoltThreadTick));
                        }
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
            return set.FindDropHole(tip.ToNumerics(), t.up.ToNumerics(), Progress.BoltCaptureZone(set));
        }

        /// <summary>
        /// A free bolt let go near a hole drops in: straight enough it stands upright, Started (twist to thread);
        /// more tilted it sits crooked, cross-threaded (pull it out). Otherwise it just falls.
        /// </summary>
        private bool TryDropIn(BoltRuntime rt, BoltSet set)
        {
            if (!Progress.AreBoltsAvailable(set)) return false;
            var hole = DropHoleFor(rt, set);
            if (hole == null) return false;

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
                PlaceThreaded(rt, bolt, holePos, holeRot, axis); // upright on the hole axis, even if dropped crooked (Learn)
                _training.ShowStatus(Learn
                    ? $"Bolt in. Grab it and twist clockwise to thread it ({bolt.Spec.ThreadTurnsToSeat:0} turns, about {TwistsToSeat(bolt.Spec)} wrist twists)."
                    : "Bolt in.", good: true);
                if (set.StraightensCrooked && set.WouldCrossThread(hole, tilt)) ShowTip(holePos + axis * 0.07f);
            }
            else
            {
                // Sits crooked in the hole at the angle it was dropped: a jam.
                _training.PlaySound(FeedbackCue.Reject, holePos);
                var crooked = Quaternion.FromToRotation(axis, rt.Adapter.transform.up) * holeRot;
                rt.Adapter.SetPose(holePos + axis * _engageHeightMetres, crooked);
                _training.ShowStatus(Learn
                    ? $"Cross-threaded: the bolt went in {tilt:0}° off straight (max {bolt.Spec.MaxStartTiltDegrees:0}°). Grab it, pull it out and drop it in upright."
                    : "Bolt jammed: cross-threaded.", good: false);
            }
            _training.NotifyProgressChanged();
            return true;
        }

        /// <summary>Learn: short "keep it straight" tip floating over a bolt that was straightened for the trainee.</summary>
        private void ShowTip(Vector3 position)
        {
            var data = Data;
            if (_tipLabelPrefab == null || data == null || string.IsNullOrEmpty(data.straightenTip)) return;
            if (_tipLabel == null)
            {
                _tipLabel = Instantiate(_tipLabelPrefab, transform);
                _tipLabel.name = "Bolt Tip Label";
            }
            _tipLabel.Show("Tip", data.straightenTip, position);
            _tipHideTime = Time.time + _tipSeconds;
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
            _training.PlaySound(FeedbackCue.BoltSeated, rt.Adapter.transform.position);
            rt.Adapter.Pulse(Haptics.ForCue(FeedbackCue.BoltSeated));
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
            _training.PlaySound(FeedbackCue.PickUp, adapter.transform.position);
            adapter.Pulse(Haptics.ForCue(FeedbackCue.PickUp));
            if (!_byAdapter.TryGetValue(adapter, out var rt) || rt.Hole == null) return;
            // Picking a started or jammed bolt up again: it stays in its hole; twisting continues from here.
            adapter.SetTracking(false, false);
            rt.Tracker.Reset();
        }

        private void OnBoltReleased(BoltAdapter adapter)
        {
            if (!_byAdapter.TryGetValue(adapter, out var rt) || Set == null) return;
            if (rt.Hole != null) return; // already in a hole (started / cross-threaded): stays there, locked in place
            bool inHole = TryDropIn(rt, Set);
            if (ToolReleaseRule.ForBolt(inHole) == BoltRelease.ReturnToTray)
                adapter.ReturnHome(Data != null ? Data.toolReturnSeconds : 0.6f); // bolts never fall
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

        /// <summary>
        /// Learn labels for the bench: one for the loose bolts in the tray ("Cover bolts ×8") and one for the wrench
        /// while it rests (not held, not on a bolt). Appends key, text and anchor for each.
        /// </summary>
        /// <summary>Bolts carried freely and the wrench while held off a bolt (for contact shadows).</summary>
        public void CollectHeld(List<Transform> into)
        {
            foreach (var rt in _runtimes)
                if (rt.Adapter.IsHeld && rt.Hole == null) into.Add(rt.Adapter.transform);
            if (_wrench != null && _wrench.IsHeld && _wrenchHole == null) into.Add(_wrench.transform);
        }

        public void CollectStagedLabels(List<string> keys, List<string> texts, List<System.Numerics.Vector3> anchors)
        {
            var data = Data;
            if (data == null) return;
            int loose = 0;
            var sum = Vector3.zero;
            float top = float.MinValue;
            foreach (var rt in _runtimes)
            {
                if (rt.Hole != null || rt.Adapter.IsHeld || rt.Adapter.IsReturningHome) continue;
                loose++;
                sum += rt.Adapter.transform.position;
                top = Mathf.Max(top, rt.Adapter.transform.position.y);
            }
            if (loose > 0)
            {
                var c = sum / loose;
                if (_boltGroupName == null) _boltGroupName = LabelLayout.Capitalise(string.IsNullOrEmpty(data.groupName) ? "bolts" : data.groupName);
                string name = _boltGroupName;
                keys.Add("bolts");
                texts.Add(_groupTexts.Get(name, loose));
                anchors.Add(new Vector3(c.x, top + 0.05f, c.z).ToNumerics());
            }
            if (_wrench != null && !_wrench.IsHeld && _wrenchHole == null && !_wrench.IsReturningHome)
            {
                keys.Add("wrench");
                texts.Add(data.wrenchName);
                anchors.Add((_wrench.transform.position + _wrench.transform.forward * 0.15f + Vector3.up * 0.06f).ToNumerics()); // over the handle middle
            }
        }
    }
}
