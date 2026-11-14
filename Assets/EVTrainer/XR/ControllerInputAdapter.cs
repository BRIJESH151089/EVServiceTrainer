using EVTrainer.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>
    /// Reads one XRI controller interactor and publishes a plain <see cref="InputFrame"/>.
    /// Grip comes from the interactor's select input, trigger from its activate input,
    /// so the adapter reads exactly what XRI uses to grab.
    /// </summary>
    public sealed class ControllerInputAdapter : MonoBehaviour
    {
        [SerializeField] private XRBaseInputInteractor _interactor;
        [Tooltip("Transform whose world pose is reported as the hand pose. Defaults to this transform.")]
        [SerializeField] private Transform _poseSource;

        public InputFrame Current { get; private set; } = InputFrame.None;

        public InteractorHandedness Handedness => _interactor != null ? _interactor.handedness : InteractorHandedness.None;

        /// <summary>True while this controller is holding something (XRI selection).</summary>
        public bool IsHolding => _interactor != null && _interactor.hasSelection;

        /// <summary>Haptic pulse on this controller.</summary>
        public void Pulse(HapticPulse pulse) => HapticOutput.Send(_interactor, pulse);

        private void Reset()
        {
            _interactor = GetComponentInChildren<XRBaseInputInteractor>();
            _poseSource = transform;
        }

        private void Update()
        {
            if (_interactor == null || !_interactor.isActiveAndEnabled)
            {
                Current = InputFrame.None;
                return;
            }

            float grip = _interactor.selectInput != null ? _interactor.selectInput.ReadValue() : 0f;
            float trigger = _interactor.activateInput != null ? _interactor.activateInput.ReadValue() : 0f;
            var source = _poseSource != null ? _poseSource : transform;

            Current = new InputFrame(InputSource.Controller, grip, trigger, 0f, source.ToPartPose(), isTracked: true);
        }
    }
}
