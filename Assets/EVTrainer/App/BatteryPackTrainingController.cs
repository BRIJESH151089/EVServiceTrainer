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
    /// the snap (lock, haptics, sound, highlight) and the panels from the result.
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

        [Header("Settle re-check (part dropped just above its seat)")]
        [SerializeField, Min(0f)] private float _settleMaxHeightMetres = SettleRetryRule.DefaultMaxHeightAboveTargetMetres;
        [Tooltip("Give the part at least this long to fall before testing whether it has settled.")]
        [SerializeField, Min(0f)] private float _settleMinSeconds = 0.15f;
        [SerializeField, Min(0.1f)] private float _settleMaxSeconds = 1f;
        [SerializeField, Min(0f)] private float _settleLinearSpeed = 0.02f;
        [SerializeField, Min(0f)] private float _settleAngularSpeed = 0.3f;

        [Header("HUD")]
        [SerializeField] private TrainingHudView _hud;

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

        private readonly Dictionary<string, GrabbablePartAdapter> _partsById = new Dictionary<string, GrabbablePartAdapter>();
        private float _nextPanelRefresh;
        private string _lastInstallText;

        public AssemblyProgress Progress { get; private set; }
        public InstallResult? LastResult { get; private set; }

        private void Awake()
        {
            var definition = _module.ToCore(_assemblyOrigin);
            Progress = new AssemblyProgress(definition, TrainingMode.Learn);

            foreach (var part in _parts)
            {
                if (!definition.TryGetPart(part.PartId, out _))
                {
                    Debug.LogError($"Part '{part.PartId}' on {part.name} is not in module '{definition.Id}'.", part);
                    continue;
                }
                _partsById[part.PartId] = part;
            }

            if (_ghosts != null)
            {
                foreach (var spec in definition.Parts)
                    _ghosts.Place(spec.Id, spec.TargetPose.Position.ToUnity(), spec.TargetPose.Rotation.ToUnity());
            }
        }

        private void Start()
        {
            RefreshGhost();
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (_hud != null) _hud.Render(Progress, LastResult);
        }

        /// <summary>Learn mode: outline only the current part, and only while it can be fitted.</summary>
        private void RefreshGhost()
        {
            if (_ghosts == null) return;
            var current = Progress.CurrentPart;
            bool show = Progress.Mode == TrainingMode.Learn && current != null && Progress.IsAvailable(current.Id);
            _ghosts.Show(show ? current.Id : null);
        }

        private void OnEnable()
        {
            foreach (var part in _parts) part.Released += OnPartReleased;
        }

        private void OnDisable()
        {
            foreach (var part in _parts) part.Released -= OnPartReleased;
        }

        private void OnPartReleased(PartRelease release)
        {
            if (!_partsById.TryGetValue(release.PartId, out var part)) return;
            Progress.Definition.TryGetPart(release.PartId, out var spec);

            var result = Progress.TryInstall(release.PartId, release.Pose);

            // Dropped just above its seat: let physics settle it, then check once more.
            if (SettleRetryRule.IsEligible(result, spec, release.Pose, _settleMaxHeightMetres))
            {
                StartCoroutine(RetryWhenSettled(part, spec, release));
                return;
            }

            ApplyResult(part, spec, release, result, afterSettling: false);
        }

        /// <summary>One retry per release: waits until the part rests (max <see cref="_settleMaxSeconds"/>),
        /// then calls TryInstall with the settled pose. Cancelled if the part is grabbed again.</summary>
        private System.Collections.IEnumerator RetryWhenSettled(GrabbablePartAdapter part, PartSpec spec,
            PartRelease release)
        {
            var waitPhysics = new WaitForFixedUpdate();
            float start = Time.time;
            int settledSteps = 0;
            while (Time.time - start < _settleMaxSeconds)
            {
                yield return waitPhysics;
                if (part.IsHeld || part.IsLocked) yield break; // trainee took it back; that release is void
                if (Time.time - start < _settleMinSeconds) continue;
                settledSteps = part.IsSettled(_settleLinearSpeed, _settleAngularSpeed) ? settledSteps + 1 : 0;
                if (settledSteps >= 3) break;
            }

            var result = Progress.TryInstall(spec.Id, part.CurrentPose);
            ApplyResult(part, spec, release, result, afterSettling: true);
        }

        private void ApplyResult(GrabbablePartAdapter part, PartSpec spec, PartRelease release, InstallResult result, bool afterSettling)
        {
            LastResult = result;
            _lastInstallText = $"{InstallResultText.Describe(result, Progress.Definition)} " +
                               $"({release.Source}, {release.Hand}{(afterSettling ? ", after settling" : "")})";

            if (result.IsInstalled)
            {
                part.LockAt(spec.TargetPose, _snapSeconds);
                part.PulseReleasingController(_hapticAmplitude, _hapticSeconds);
                var feedback = part.GetComponent<SnapFeedback>();
                if (feedback != null) feedback.PlayInstalled();
                RefreshGhost();
            }

            RefreshHud();
            _nextPanelRefresh = 0f; // show the result immediately
        }

        private void Update()
        {
            RenderHand(_leftHand, _leftHandVisual);
            RenderHand(_rightHand, _rightHandVisual);
            UpdateHoverPreview();

            if (_debugToggle != null && _debugToggle.WasPressedThisFrame && _debugPanel != null)
            {
                _debugPanel.gameObject.SetActive(!_debugPanel.gameObject.activeSelf);
                _nextPanelRefresh = 0f;
            }
            UpdateDebugPanel();
        }

        /// <summary>
        /// Learn mode: while the trainee holds the part whose ghost is shown, evaluate its pose each frame
        /// and tint the ghost green when a release now would install it. Feedback only; nothing is installed.
        /// One SnapEvaluator call per frame for the held part, so no throttling is needed.
        /// </summary>
        private void UpdateHoverPreview()
        {
            if (_ghosts == null || Progress.Mode != TrainingMode.Learn) return;
            string id = _ghosts.VisiblePartId;
            bool ready = false;
            if (id != null && _partsById.TryGetValue(id, out var part) && part.IsHeld && !part.IsLocked
                && Progress.Definition.TryGetPart(id, out var spec))
            {
                ready = SnapEvaluator.Evaluate(spec, part.CurrentPose).IsWithinTolerance;
            }
            _ghosts.SetReady(ready);
        }

        private void UpdateDebugPanel()
        {
            if (_debugPanel == null || !_debugPanel.isActiveAndEnabled) return;
            if (Time.unscaledTime < _nextPanelRefresh) return;
            _nextPanelRefresh = Time.unscaledTime + 1f / _panelRefreshHz;

            // Live error of the current part (or the last part once complete).
            var spec = Progress.CurrentPart ?? Progress.Definition.Parts[Progress.Definition.Parts.Count - 1];
            var evaluation = _partsById.TryGetValue(spec.Id, out var part)
                ? SnapEvaluator.Evaluate(spec, part.CurrentPose)
                : default;
            bool held = part != null && part.IsHeld;

            _debugPanel.Render(new DebugPanelData(
                ActiveFrame(_leftHand, _leftInput),
                ActiveFrame(_rightHand, _rightInput),
                held, held ? part.HeldBy : "",
                evaluation, spec.PositionToleranceMetres, spec.AngleToleranceDegrees,
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
