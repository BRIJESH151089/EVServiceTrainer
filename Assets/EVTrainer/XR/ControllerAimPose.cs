using UnityEngine;
using UnityEngine.InputSystem;

namespace EVTrainer.XR
{
    /// <summary>
    /// Puts this transform on the controller's AIM (pointer) pose, the pose XRI rays use, instead of the grip pose
    /// that drives the controller object (whose forward axis runs along the handle). Reads the OpenXR
    /// pointerPosition / pointerRotation controls (tracking space) and converts to world via the XR Origin's
    /// Camera Offset. Place it on the UI ray / poke object under the controller so it shows and hides with it.
    /// </summary>
    [DefaultExecutionOrder(-29970)] // before XRI's interaction manager reads the ray origin
    public sealed class ControllerAimPose : MonoBehaviour
    {
        [Tooltip("e.g. <XRController>{LeftHand}/pointerPosition")]
        [SerializeField] private InputActionProperty _position = new InputActionProperty(new InputAction("Aim Position", expectedControlType: "Vector3"));
        [Tooltip("e.g. <XRController>{LeftHand}/pointerRotation")]
        [SerializeField] private InputActionProperty _rotation = new InputActionProperty(new InputAction("Aim Rotation", expectedControlType: "Quaternion"));
        [Tooltip("XR Origin's Camera Offset: tracked poses are relative to it.")]
        [SerializeField] private Transform _trackingSpace;

        /// <summary>True when the aim pose was read this frame (false = keeps the last pose / the parent's pose).</summary>
        public bool HasPose { get; private set; }

        private void OnEnable()
        {
            _position.action?.Enable();
            _rotation.action?.Enable();
            Application.onBeforeRender += UpdatePose;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= UpdatePose;
            if (_position.reference == null) _position.action?.Disable();
            if (_rotation.reference == null) _rotation.action?.Disable();
        }

        private void Update() => UpdatePose();

        private void UpdatePose()
        {
            var p = _position.action;
            var r = _rotation.action;
            HasPose = p != null && r != null && p.controls.Count > 0 && r.controls.Count > 0;
            if (!HasPose) return;

            var position = p.ReadValue<Vector3>();
            var rotation = r.ReadValue<Quaternion>();
            if (_trackingSpace != null)
            {
                position = _trackingSpace.TransformPoint(position);
                rotation = _trackingSpace.rotation * rotation;
            }
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
