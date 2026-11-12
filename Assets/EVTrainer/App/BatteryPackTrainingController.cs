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
    /// Composition root for the battery pack training scene. Builds the Core assembly from the
    /// module data, sends every part release to <see cref="AssemblyProgress.TryInstall"/> and drives
    /// the snap (lock, haptics, sound, highlight), ghosts and panels from the result.
    /// </summary>
    public sealed class BatteryPackTrainingController : MonoBehaviour
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

        [Header("Debug panel")]
        [SerializeField] private DebugPanelView _debugPanel;
        [Tooltip("Shows/hides the debug panel (left X button; F2 in the Editor).")]
        [SerializeField] private ButtonActionAdapter _debugToggle;
        [SerializeField] private ControllerInputAdapter _leftInput;
        [SerializeField] private ControllerInputAdapter _rightInput;
        [SerializeField] private HandTrackingAdapter _leftHand;
        [SerializeField] private HandTrackingAdapter _rightHand;
        [SerializeField] private HandJointVisual _leftHandVisual;
        [SerializeField] private HandJointVisual _rightHandVisual;
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
            RefreshGhosts();
            RefreshHud();
        }

        private double RunTime => Time.time - _runStartTime;

        private void Start()
        {
            if (_startPanel == null)
            {
                BeginRun(TrainingMode.Learn);
                return;
            }

            // Nothing can be picked up until a mode is chosen.
            foreach (var part in _parts) part.SetGrabbable(false);
            _startPanel.Show();
            RefreshGhosts();
            RefreshHud();
            if (_hud != null)
            {
                _hud.ShowModeNotChosen();
                _hud.ShowNotice("Choose Learn mode or Test mode on the panel to begin.", warning: false);
            }
        }

        private void OnModeChosen(TrainingMode mode) => BeginRun(mode);

        /// <summary>Restart: reload the scene, which brings back the mode selection with every part on its rack.</summary>
        private void OnRestartRequested() =>
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);

        private void RefreshHud()
        {
            if (_hud != null) _hud.Render(Progress, LastResult);
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
            if (_hud != null) _hud.RestartRequested += OnRestartRequested;
        }

        private void OnDisable()
        {
            foreach (var part in _parts)
            {
                part.Released -= OnPartReleased;
                part.SecondHandNeeded -= OnSecondHandNeeded;
            }
            if (_startPanel != null) _startPanel.ModeChosen -= OnModeChosen;
            if (_hud != null) _hud.RestartRequested -= OnRestartRequested;
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
                if (part.IsCarried || part.IsLocked) yield break; // trainee took it back; that release is void
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

            RefreshHud();
            _nextPanelRefresh = 0f; // show the result immediately
        }

        private void Update()
        {
            RenderHand(_leftHand, _leftHandVisual);
            RenderHand(_rightHand, _rightHandVisual);
            UpdateHeldPreview();

            if (_debugToggle != null && _debugToggle.WasPressedThisFrame && _debugPanel != null)
            {
                _debugPanel.gameObject.SetActive(!_debugPanel.gameObject.activeSelf);
                _nextPanelRefresh = 0f;
            }
            UpdateDebugPanel();
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
                posTol = slot.PositionToleranceMetres;
                angTol = slot.AngleToleranceDegrees;
            }

            _debugPanel.Render(new DebugPanelData(
                ActiveFrame(_leftHand, _leftInput),
                ActiveFrame(_rightHand, _rightInput),
                held != null, held != null ? $"{held.HeldBy}: {held.ItemId}" : "",
                evaluation, posTol, angTol,
                _lastInstallText));
        }

        private static void RenderHand(HandTrackingAdapter hand, HandJointVisual visual)
        {
            if (visual == null) return;
            bool tracked = hand != null && hand.isActiveAndEnabled && hand.Current.IsTracked;
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
