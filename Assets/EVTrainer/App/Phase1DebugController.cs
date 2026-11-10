using EVTrainer.Core;
using EVTrainer.UI;
using EVTrainer.XR;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// Composition root for the Phase1_Debug scene: builds a one-part Core assembly from the
    /// ghost's pose, then each frame evaluates the held part against it and updates the panel.
    /// No snapping (Phase 2).
    /// </summary>
    public sealed class Phase1DebugController : MonoBehaviour
    {
        [SerializeField] private ControllerInputAdapter _leftInput;
        [SerializeField] private ControllerInputAdapter _rightInput;
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
            LastEvaluation = SnapEvaluator.Evaluate(_partSpec, _part.CurrentPose);

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 1f / _panelRefreshHz;

            _panel.Render(new DebugPanelData(
                _leftInput != null ? _leftInput.Current : InputFrame.None,
                _rightInput != null ? _rightInput.Current : InputFrame.None,
                _part.IsHeld, _part.HeldBy,
                LastEvaluation,
                _partSpec.PositionToleranceMetres, _partSpec.AngleToleranceDegrees));
        }
    }
}
