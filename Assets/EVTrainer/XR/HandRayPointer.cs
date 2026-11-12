using EVTrainer.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>
    /// UI pointer for a tracked hand: aims this transform (the origin of a UI-only XRRayInteractor) from an
    /// estimated shoulder through the index knuckle, and supplies pinch as the ray's UI press.
    /// Self-made so no Meta aim-pose dependency is needed; it reads the plain joints from <see cref="HandTrackingAdapter"/>.
    /// </summary>
    [DefaultExecutionOrder(-29970)] // after HandTrackingAdapter (-29980), before XRI's interaction manager
    public sealed class HandRayPointer : MonoBehaviour, IXRInputButtonReader
    {
        [SerializeField] private HandTrackingAdapter _hand;
        [Tooltip("The XR camera (head). Used to estimate the shoulder position.")]
        [SerializeField] private Transform _head;
        [Tooltip("UI-only ray interactor on this GameObject; its UI press is set to this pinch reader at start.")]
        [SerializeField] private XRRayInteractor _ray;
        [SerializeField] private float _shoulderDrop = 0.15f;
        [SerializeField] private float _shoulderWidth = 0.18f;
        [Header("Pinch to press (0..1)")]
        [SerializeField, Range(0f, 1f)] private float _press = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _release = 0.5f;

        private ButtonHysteresis _pinch;
        private int _pressedFrame = -1;
        private int _releasedFrame = -1;
        private float _value;
        private bool _tracked;

        private void Awake()
        {
            _pinch = new ButtonHysteresis(_press, _release);
            if (_ray != null)
            {
                _ray.uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ObjectReference;
                _ray.uiPressInput.SetObjectReference(this);
            }
        }

        private void Update()
        {
            _tracked = _hand != null && _hand.isActiveAndEnabled && _hand.Current.IsTracked;
            _value = _tracked ? _hand.Current.Pinch : 0f;
            _pinch.Update(_value);
            if (_pinch.WasPressedThisUpdate) _pressedFrame = Time.frameCount;
            if (_pinch.WasReleasedThisUpdate) _releasedFrame = Time.frameCount;
            if (!_tracked || _head == null) return;

            var knuckle = _hand.JointPositions[(int)HandJoint.IndexProximal].ToUnity();
            var right = Vector3.ProjectOnPlane(_head.right, Vector3.up).normalized;
            float side = _hand.Handedness == UnityEngine.XR.Hands.Handedness.Left ? -1f : 1f;
            var shoulder = _head.position + Vector3.down * _shoulderDrop + right * (_shoulderWidth * side);
            var direction = knuckle - shoulder;
            if (direction.sqrMagnitude < 1e-6f) return;
            transform.SetPositionAndRotation(knuckle, Quaternion.LookRotation(direction.normalized, Vector3.up));
        }

        public bool ReadIsPerformed() => _pinch != null && _pinch.IsPressed;
        public bool ReadWasPerformedThisFrame() => _pressedFrame == Time.frameCount;
        public bool ReadWasCompletedThisFrame() => _releasedFrame == Time.frameCount;
        public float ReadValue() => _value;

        public bool TryReadValue(out float value)
        {
            value = _value;
            return _tracked;
        }
    }
}
