using EVTrainer.Core;
using EVTrainer.Rendering;
using EVTrainer.UI;
using EVTrainer.XR;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// Composition root for the Phase1_Debug scene: builds a one-part Core assembly from the
    /// ghost's pose, then each frame evaluates the held part against it and updates the panel
    /// and the hand visuals. No snapping (Phase 2).
    /// </summary>
    public sealed class Phase1DebugController : MonoBehaviour
    {
        [SerializeField] private ControllerInputAdapter _leftInput;
        [SerializeField] private ControllerInputAdapter _rightInput;
        [SerializeField] private HandTrackingAdapter _leftHand;
        [SerializeField] private HandTrackingAdapter _rightHand;
        [SerializeField] private HandJointVisual _leftHandVisual;
        [SerializeField] private HandJointVisual _rightHandVisual;
        [SerializeField] private GrabbablePartAdapter _part;
        [SerializeField] private Transform _ghostTarget;
        [SerializeField] private DebugPanelView _panel;
        [SerializeField, Min(0f)] private float _positionToleranceMetres = 0.02f;
        [SerializeField, Range(0f, 180f)] private float _angleToleranceDegrees = 10f;
        [SerializeField, Min(1f)] private float _panelRefreshHz = 15f;

        private AssemblyDefinition _assembly;
        private PartSpec _partSpec;
        private float _nextRefresh;

        public SnapEvaluation LastEvaluation { get; private set; }

        private void Awake()
        {
            _partSpec = new PartSpec(
                "test_cooling_plate", "Test Cooling Plate",
                _ghostTarget.ToPartPose(),
                _positionToleranceMetres, _angleToleranceDegrees);
            _assembly = new AssemblyDefinition("phase1_debug", "Phase 1 Debug", new[] { _partSpec });
        }

        private void Update()
        {
            RenderHand(_leftHand, _leftHandVisual);
            RenderHand(_rightHand, _rightHandVisual);

            LastEvaluation = SnapEvaluator.Evaluate(_partSpec, _part.CurrentPose);

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 1f / _panelRefreshHz;

            _panel.Render(new DebugPanelData(
                ActiveFrame(_leftHand, _leftInput),
                ActiveFrame(_rightHand, _rightInput),
                _part.IsHeld, _part.HeldBy,
                LastEvaluation,
                _partSpec.PositionToleranceMetres, _partSpec.AngleToleranceDegrees));
        }

        private static void RenderHand(HandTrackingAdapter hand, HandJointVisual visual)
        {
            if (visual == null) return;
            bool tracked = hand != null && hand.isActiveAndEnabled && hand.Current.IsTracked;
            visual.Render(tracked ? hand.JointPositions : null, tracked);
        }

        /// <summary>
        /// The tracked hand wins; otherwise the active controller; otherwise the (untracked) hand
        /// frame if hand mode is active; otherwise an empty frame.
        /// </summary>
        private static InputFrame ActiveFrame(HandTrackingAdapter hand, ControllerInputAdapter controller)
        {
            bool handActive = hand != null && hand.isActiveAndEnabled;
            if (handActive && hand.Current.IsTracked) return hand.Current;
            if (controller != null && controller.isActiveAndEnabled) return controller.Current;
            return handActive ? hand.Current : InputFrame.None;
        }
    }
}
