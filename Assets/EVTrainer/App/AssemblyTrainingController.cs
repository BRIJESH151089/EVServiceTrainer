using System.Collections;
using System.Collections.Generic;
using EVTrainer.Core;
using EVTrainer.Rendering;
using EVTrainer.UI;
using EVTrainer.XR;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// Composition root for a training scene. Loads ONE assembly module (data asset), builds the Core assembly from the
    /// module data, sends every part release to <see cref="AssemblyProgress.TryInstall"/> and drives
    /// the snap (lock, haptics, sound, highlight), ghosts and panels from the result.
    /// </summary>
    public sealed class AssemblyTrainingController : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private AssemblyModuleDefinition _module;
        [Tooltip("Assembly origin: target poses in the module are relative to this (fixture top centre).")]
        [SerializeField] private Transform _assemblyOrigin;
        [SerializeField] private GrabbablePartAdapter[] _parts;
        [SerializeField] private GhostPresenter _ghosts;

        [Header("Snap")]
        [SerializeField, Min(0f)] private float _snapSeconds = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _hapticAmplitude = 0.6f;
        [SerializeField, Min(0f)] private float _hapticSeconds = 0.08f;

        [Header("Wrong order")]
        [Tooltip("Time to carry a wrong-order part back to its staging position.")]
        [SerializeField, Min(0f)] private float _returnSeconds = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _rejectHapticAmplitude = 1f;
        [SerializeField, Min(0f)] private float _rejectHapticSeconds = 0.25f;

        [Header("Settle re-check (part dropped just above its seat)")]
        [SerializeField, Min(0f)] private float _settleMaxHeightMetres = SettleRetryRule.DefaultMaxHeightAboveTargetMetres;
        [Tooltip("Give the part at least this long to fall before testing whether it has settled.")]
        [SerializeField, Min(0f)] private float _settleMinSeconds = 0.15f;
        [SerializeField, Min(0.1f)] private float _settleMaxSeconds = 1f;
        [SerializeField, Min(0f)] private float _settleLinearSpeed = 0.02f;
        [SerializeField, Min(0f)] private float _settleAngularSpeed = 0.3f;

        [Header("HUD and mode selection")]
        [SerializeField] private TrainingHudView _hud;
        [Tooltip("Learn / Test choice shown before each run. Without it the run starts in Learn mode.")]
        [SerializeField] private StartPanelView _startPanel;
        [Tooltip("Learn mode: template for the staged-part name labels (one per part group, the bolt tray and the wrench).")]
        [SerializeField] private PartLabelView _partLabel;
        [SerializeField, Min(0f)] private float _labelGapMetres = 0.04f;
        [Tooltip("Label size relative to the template.")]
        [SerializeField, Range(0.2f, 2f)] private float _labelScale = 0.7f;
        [Tooltip("Bolt hand-start and torque wrench flow (optional; modules without bolts leave it empty).")]
        [SerializeField] private BoltingController _bolting;
        [Tooltip("Physical Restart push button (within reach). Back to mode selection with all parts staged.")]
        [SerializeField] private PushButtonView _restartButton;

        [Header("Debug panel")]
        [SerializeField] private DebugPanelView _debugPanel;
        [Tooltip("Shows/hides the debug panel (left X button; F2 in the Editor).")]
        [SerializeField] private ButtonActionAdapter _debugToggle;
        [Tooltip("Per-interactor hover/hold lines on the debug panel.")]
        [SerializeField] private InteractorStatusReporter _interactorStatus;
        [SerializeField] private ControllerInputAdapter _leftInput;
        [SerializeField] private ControllerInputAdapter _rightInput;
        [SerializeField] private HandTrackingAdapter _leftHand;
        [SerializeField] private HandTrackingAdapter _rightHand;
        [SerializeField] private HandJointVisual _leftHandVisual;
        [SerializeField] private HandJointVisual _rightHandVisual;
        [Tooltip("Debug: draw the tracked joints as spheres (the skinned hand meshes are the normal visual).")]
        [SerializeField] private bool _showJointDebug;
        [SerializeField, Min(1f)] private float _panelRefreshHz = 15f;

        private readonly List<string> _ghostSlots = new List<string>();
        private AssemblyDefinition _definition;
        private float _nextPanelRefresh;
        private string _lastInstallText;
        private float _runStartTime;
        private InstallResult? _lastEvaluation;

        public AssemblyProgress Progress { get; private set; }
        public InstallResult? LastResult { get; private set; }

        private void Awake()
        {
            _definition = _module.ToCore(_assemblyOrigin);
            foreach (var part in _parts)
            {
                var slots = _definition.SlotsOfType(part.PartTypeId);
                if (slots.Count == 0)
                {
                    Debug.LogError($"Part type '{part.PartTypeId}' on {part.name} is not in module '{_definition.Id}'.", part);
                    continue;
                }
                // Heavy parts (module data) can only be lifted with two hands.
                part.SetRequiresTwoHands(slots[0].RequiresTwoHands);
            }

            if (_ghosts != null)
            {
                foreach (var spec in _definition.Parts)
                    _ghosts.Place(spec.Id, spec.TargetPose.Position.ToUnity(), spec.TargetPose.Rotation.ToUnity());
            }

            // Placeholder progress so the HUD can list the steps while the trainee picks a mode.
            Progress = new AssemblyProgress(_definition, TrainingMode.Learn);
        }

        /// <summary>True once a mode has been chosen and the run has started.</summary>
        public bool IsRunning { get; private set; }

        private void BeginRun(TrainingMode mode)
        {
            Progress = new AssemblyProgress(_definition, mode);
            LastResult = null;
            _lastInstallText = null;
            _runStartTime = Time.time;
            IsRunning = true;
            foreach (var part in _parts) part.SetGrabbable(true);
            // The HUD and Restart only appear once the start panel has gone, so the two never overlap.
            SetRunUiVisible(true);
            RefreshGhosts();
            RefreshHud();
            Debug.Log($"[EVTrainer] Run started: mode={mode}, start panel active={(_startPanel != null && _startPanel.gameObject.activeInHierarchy)}, " +
                      $"grabbable parts={GrabbableCount()}/{_parts.Length}");
            _nextPanelRefresh = 0f;
        }

        private int GrabbableCount()
        {
            int n = 0;
            foreach (var part in _parts) if (part.IsGrabbable) n++;
            return n;
        }

        /// <summary>Seconds since the run started (for the mistake log).</summary>
        public double RunTime => Time.time - _runStartTime;

        /// <summary>Parts the trainee is carrying right now (for contact shadows).</summary>
        public void CollectCarried(List<Transform> into)
        {
            foreach (var part in _parts)
                if (part.IsCarried && !part.IsLocked) into.Add(part.transform);
        }

        /// <summary>Called by other run-time parts (e.g. bolting) when progress changed: redraw HUD and debug panel.</summary>
        public void NotifyProgressChanged()
        {
            RefreshHud();
            _nextPanelRefresh = 0f;
        }

        /// <summary>Status line from another run-time part (e.g. a bolt event), also shown on the debug panel.</summary>
        public void ShowStatus(string text, bool good)
        {
            _extraStatus = (text, good);
            if (_hud != null && _hud.isActiveAndEnabled) _hud.ShowResult(text, good);
            _lastInstallText = text;
            _nextPanelRefresh = 0f;
        }

        private (string text, bool good)? _extraStatus;

        private void Start()
        {
            if (_startPanel == null)
            {
                BeginRun(TrainingMode.Learn);
                return;
            }

            // Nothing can be picked up until a mode is chosen.
            foreach (var part in _parts) part.SetGrabbable(false);
            SetRunUiVisible(false);
            _startPanel.Show(_definition.Title);
            if (_hud != null)
            {
                // The read-only info panel is always up: steps preview, neutral badge, where to choose.
                _hud.Render(Progress, null);
                _hud.ShowModeNotChosen();
                _hud.ShowNotice("Choose Learn or Test with the buttons below.", warning: false);
            }
            RefreshGhosts();
            Debug.Log($"[EVTrainer] Waiting for mode selection ({_definition.Title}); parts locked until then.");
        }

        /// <summary>Restart only appears once a run has started (the info panel is always shown).</summary>
        private void SetRunUiVisible(bool visible)
        {
            if (_restartButton != null) _restartButton.gameObject.SetActive(visible);
        }

        private void OnModeChosen(TrainingMode mode) => BeginRun(mode);

        /// <summary>Restart: reload the scene, which brings back the mode selection with every part on its rack.</summary>
        private void OnRestartRequested()
        {
            Debug.Log("[EVTrainer] Restart pressed: reloading the scene.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
        }

        private void RefreshHud()
        {
            if (_hud == null) return;
            _hud.Render(Progress, LastResult);
            // A bolt message stays on top until the next part result (or until the module is complete).
            if (_extraStatus.HasValue && !Progress.IsComplete) _hud.ShowResult(_extraStatus.Value.text, _extraStatus.Value.good);
        }

        /// <summary>Learn mode: outline the free, fittable slots of the current step.</summary>
        private void RefreshGhosts()
        {
            if (_ghosts == null) return;
            _ghostSlots.Clear();
            var step = Progress.CurrentStep;
            if (IsRunning && Progress.Mode == TrainingMode.Learn && step != null)
            {
                foreach (var slot in step.Slots)
                {
                    if (Progress.IsAvailable(slot.Id)) _ghostSlots.Add(slot.Id);
                }
            }
            _ghosts.Show(_ghostSlots);
        }

        private void OnEnable()
        {
            foreach (var part in _parts)
            {
                part.Released += OnPartReleased;
                part.SecondHandNeeded += OnSecondHandNeeded;
            }
            if (_startPanel != null) _startPanel.ModeChosen += OnModeChosen;
            if (_restartButton != null) _restartButton.Clicked += OnRestartRequested;
        }

        private void OnDisable()
        {
            foreach (var part in _parts)
            {
                part.Released -= OnPartReleased;
                part.SecondHandNeeded -= OnSecondHandNeeded;
            }
            if (_startPanel != null) _startPanel.ModeChosen -= OnModeChosen;
            if (_restartButton != null) _restartButton.Clicked -= OnRestartRequested;
        }

        private void OnSecondHandNeeded(GrabbablePartAdapter part)
        {
            if (_hud != null) _hud.ShowNotice("Heavy part: use both hands.", warning: true);
            _lastInstallText = $"Heavy part: use both hands ({part.ItemId}).";
            _nextPanelRefresh = 0f;
        }

        private void OnPartReleased(GrabbablePartAdapter part, PartRelease release)
        {
            var preview = Progress.Evaluate(release.TypeId, release.Pose);
            _definition.TryGetPart(preview.PartId, out var slot);

            // Dropped just above its seat: let physics settle it, then judge (and log) only the settled pose.
            if (slot != null && SettleRetryRule.IsEligible(preview, slot, release.Pose, _settleMaxHeightMetres))
            {
                StartCoroutine(RetryWhenSettled(part, release));
                if (_hud != null) _hud.ShowNotice("Checking fit…", warning: false);
                _lastInstallText = $"Checking fit… ({release.ItemId})";
                _nextPanelRefresh = 0f;
                return;
            }

            ApplyResult(part, release, Progress.TryInstall(release.TypeId, release.Pose, RunTime, release.ItemId), afterSettling: false);
        }

        /// <summary>One check per release: waits until the part rests (max <see cref="_settleMaxSeconds"/>),
        /// then calls TryInstall with the settled pose. Cancelled if the part is grabbed again.</summary>
        private IEnumerator RetryWhenSettled(GrabbablePartAdapter part, PartRelease release)
        {
            var waitPhysics = new WaitForFixedUpdate();
            float start = Time.time;
            int settledSteps = 0;
            while (Time.time - start < _settleMaxSeconds)
            {
                yield return waitPhysics;
                if (part.IsCarried || part.IsLocked)
                {
                    RefreshHud(); // trainee took it back; that release is void
                    yield break;
                }
                if (Time.time - start < _settleMinSeconds) continue;
                settledSteps = part.IsSettled(_settleLinearSpeed, _settleAngularSpeed) ? settledSteps + 1 : 0;
                if (settledSteps >= 3) break;
            }

            var result = Progress.TryInstall(release.TypeId, part.CurrentPose, RunTime, release.ItemId);
            ApplyResult(part, release, result, afterSettling: true);
        }

        private void ApplyResult(GrabbablePartAdapter part, PartRelease release, InstallResult result, bool afterSettling)
        {
            LastResult = result;
            _extraStatus = null;
            _lastInstallText = $"{InstallResultText.Describe(result, Progress.Definition)} [{result.Outcome}] " +
                               $"({release.ItemId}, {release.Source}, {release.Hand}{(afterSettling ? ", after settling" : "")})";

            if (result.IsInstalled)
            {
                part.LockAt(result.SnapPose, _snapSeconds);
                part.PulseReleasingController(_hapticAmplitude, _hapticSeconds);
                var feedback = part.GetComponent<SnapFeedback>();
                if (feedback != null) feedback.PlayInstalled();
                RefreshGhosts();
            }
            else if (result.Outcome == InstallOutcome.WrongOrder)
            {
                // Not allowed yet: the part goes back where it came from, with a strong buzz.
                part.ReturnToStaging(_returnSeconds);
                part.PulseReleasingController(_rejectHapticAmplitude, _rejectHapticSeconds);
            }
            // Learn: a rejected release (wrong order, or turned the wrong way) flashes that slot's ghost red.
            if ((result.Outcome == InstallOutcome.WrongOrder || result.Outcome == InstallOutcome.WrongOrientation) && _ghosts != null)
                _ghosts.FlashReject(result.PartId);

            RefreshHud();
            _nextPanelRefresh = 0f; // show the result immediately
        }

        private void Update()
        {
            RenderHand(_leftHand, _leftHandVisual);
            RenderHand(_rightHand, _rightHandVisual);
            UpdateHoverHighlights();
            UpdateHeldPreview();
            UpdatePartLabels();

            if (_debugToggle != null && _debugToggle.WasPressedThisFrame && _debugPanel != null)
            {
                _debugPanel.gameObject.SetActive(!_debugPanel.gameObject.activeSelf);
                _nextPanelRefresh = 0f;
            }
            UpdateDebugPanel();
        }

        private readonly Dictionary<GrabbablePartAdapter, PartTint> _tints = new Dictionary<GrabbablePartAdapter, PartTint>();

        /// <summary>Subtle highlight on a free part while a hand or controller is close enough to grab it.</summary>
        private void UpdateHoverHighlights()
        {
            foreach (var part in _parts)
            {
                if (!_tints.TryGetValue(part, out var tint))
                {
                    tint = part.GetComponent<PartTint>();
                    if (tint == null) tint = part.gameObject.AddComponent<PartTint>();
                    _tints[part] = tint;
                }
                tint.SetHighlighted(part.IsHovered && !part.IsHeld && !part.IsLocked);
            }
        }

        /// <summary>
        /// Evaluates the held part (Core, no side effects) every frame. Learn mode: tints the ghost of the slot
        /// it would install into green ("release now"). A few Evaluate calls per frame; no throttling needed.
        /// </summary>
        private void UpdateHeldPreview()
        {
            string ready = null;
            _lastEvaluation = null;
            foreach (var part in _parts)
            {
                if (!part.IsCarried || part.IsLocked) continue;
                var e = Progress.Evaluate(part.PartTypeId, part.CurrentPose);
                _lastEvaluation = e;
                if (e.IsInstalled) ready = e.PartId;
            }
            if (_ghosts != null) _ghosts.SetReady(Progress.Mode == TrainingMode.Learn ? ready : null);
        }

        private readonly Dictionary<string, PartLabelView> _labels = new Dictionary<string, PartLabelView>();
        private readonly List<string> _labelKeys = new List<string>();
        private readonly List<string> _labelTexts = new List<string>();
        private readonly List<System.Numerics.Vector3> _labelAnchors = new List<System.Numerics.Vector3>();
        private readonly Dictionary<string, (int count, Vector3 sum, float top)> _groups = new Dictionary<string, (int, Vector3, float)>();
        private float _nextLabelUpdate;

        /// <summary>
        /// Learn mode: a small name label over every group of staged parts ("Battery module ×4"), the bolt tray and the
        /// wrench, so the trainee can identify them on the bench. A part leaves its group's label while held, moving or
        /// installed; labels that would overlap from the trainee's view are lifted apart (Core <see cref="LabelLayout"/>).
        /// Test mode: no labels.
        /// </summary>
        private void UpdatePartLabels()
        {
            if (_partLabel == null || Time.unscaledTime < _nextLabelUpdate) return;
            _nextLabelUpdate = Time.unscaledTime + 0.1f; // staged parts barely move: 10 Hz is plenty
            _labelKeys.Clear();
            _labelTexts.Clear();
            _labelAnchors.Clear();

            if (IsRunning && Progress.Mode == TrainingMode.Learn)
            {
                _groups.Clear();
                foreach (var part in _parts)
                {
                    if (part.IsLocked || part.IsHeld || part.IsReturning) continue;
                    var b = RendererBounds(part);
                    _groups.TryGetValue(part.PartTypeId, out var g);
                    _groups[part.PartTypeId] = (g.count + 1, g.sum + b.center, Mathf.Max(g.count == 0 ? float.MinValue : g.top, b.max.y));
                }
                foreach (var kv in _groups)
                {
                    var slots = _definition.SlotsOfType(kv.Key);
                    if (slots.Count == 0) continue;
                    var c = kv.Value.sum / kv.Value.count;
                    _labelKeys.Add(kv.Key);
                    _labelTexts.Add(LabelLayout.GroupText(slots[0].PartName, kv.Value.count));
                    _labelAnchors.Add(new Vector3(c.x, kv.Value.top + _labelGapMetres, c.z).ToNumerics());
                }
                if (_bolting != null) _bolting.CollectStagedLabels(_labelKeys, _labelTexts, _labelAnchors);
            }

            var viewer = Camera.main != null ? Camera.main.transform.position : Vector3.up * 1.6f;
            var placed = LabelLayout.Place(_labelAnchors, viewer.ToNumerics());
            for (int i = 0; i < _labelKeys.Count; i++)
            {
                if (!_labels.TryGetValue(_labelKeys[i], out var label))
                {
                    label = Instantiate(_partLabel, _partLabel.transform.parent);
                    label.name = "Label " + _labelKeys[i];
                    label.transform.localScale = _partLabel.transform.localScale * _labelScale;
                    _labels[_labelKeys[i]] = label;
                }
                label.Show(_labelTexts[i], null, placed[i].ToUnity());
            }
            foreach (var kv in _labels)
                if (!_labelKeys.Contains(kv.Key)) kv.Value.Hide();
        }

        private readonly Dictionary<GrabbablePartAdapter, Renderer[]> _renderers = new Dictionary<GrabbablePartAdapter, Renderer[]>();

        private Bounds RendererBounds(GrabbablePartAdapter part)
        {
            if (!_renderers.TryGetValue(part, out var rs)) _renderers[part] = rs = part.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(part.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private void UpdateDebugPanel()
        {
            if (_debugPanel == null || !_debugPanel.isActiveAndEnabled) return;
            if (Time.unscaledTime < _nextPanelRefresh) return;
            _nextPanelRefresh = Time.unscaledTime + 1f / _panelRefreshHz;

            // Live error of the held part against the slot it would go into.
            GrabbablePartAdapter held = null;
            foreach (var part in _parts) if (part.IsHeld) { held = part; break; }
            var evaluation = _lastEvaluation?.Evaluation ?? default;
            float posTol = 0f, angTol = 0f;
            if (_lastEvaluation.HasValue && _definition.TryGetPart(_lastEvaluation.Value.PartId, out var slot))
            {
                posTol = SnapEvaluator.PositionTolerance(slot, Progress.CaptureZone);
                angTol = SnapEvaluator.AngleTolerance(slot, Progress.CaptureZone);
            }

            _debugPanel.Render(new DebugPanelData(
                ActiveFrame(_leftHand, _leftInput),
                ActiveFrame(_rightHand, _rightInput),
                held != null, held != null ? $"{held.HeldBy}: {held.ItemId}" : "",
                evaluation, posTol, angTol,
                _lastInstallText, DiagnosticsText()));
        }

        /// <summary>Mode, start-panel state, grabbable parts and what every interactor hovers/holds.</summary>
        private string DiagnosticsText()
        {
            string mode = IsRunning ? Progress.Mode.ToString() : "not chosen";
            bool panelActive = _startPanel != null && _startPanel.gameObject.activeInHierarchy;
            string head = $"Mode: {mode} | start panel: {(panelActive ? "shown" : "hidden")} | grabbable parts: {GrabbableCount()}/{_parts.Length}";
            if (_bolting != null) head += "\n" + _bolting.DescribeWrench();
            return _interactorStatus != null ? head + "\n" + _interactorStatus.Describe() : head;
        }

        private void RenderHand(HandTrackingAdapter hand, HandJointVisual visual)
        {
            if (visual == null) return;
            bool tracked = _showJointDebug && hand != null && hand.isActiveAndEnabled && hand.Current.IsTracked;
            visual.Render(tracked ? hand.JointPositions : null, tracked);
        }

        /// <summary>The tracked hand wins; otherwise the active controller; otherwise the untracked hand frame.</summary>
        private static InputFrame ActiveFrame(HandTrackingAdapter hand, ControllerInputAdapter controller)
        {
            bool handActive = hand != null && hand.isActiveAndEnabled;
            if (handActive && hand.Current.IsTracked) return hand.Current;
            if (controller != null && controller.isActiveAndEnabled) return controller.Current;
            return handActive ? hand.Current : InputFrame.None;
        }
    }
}
