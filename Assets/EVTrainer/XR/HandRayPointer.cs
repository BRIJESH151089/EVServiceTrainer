using EVTrainer.Core;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>
    /// UI pointer for a tracked hand: positions this transform (the origin of a UI-only XRRayInteractor)
    /// and supplies the ray's UI press.
    /// Preferred source: the OpenXR "Meta Hand Tracking Aim" pose (XR Hands <see cref="MetaAimHand"/>), with its
    /// index-pinch flag as the press. Fallback, when that aim pose is unavailable: a ray from an estimated
    /// shoulder through the index knuckle, pressed by our own pinch measure from <see cref="HandTrackingAdapter"/>.
    /// </summary>
    [DefaultExecutionOrder(-29970)] // after HandTrackingAdapter (-29980), before XRI's interaction manager
    public sealed class HandRayPointer : MonoBehaviour, IXRInputButtonReader
    {
        [SerializeField] private HandTrackingAdapter _hand;
        [Tooltip("XR Origin's Camera Offset: the Meta aim pose is relative to it.")]
        [SerializeField] private Transform _trackingSpace;
        [Tooltip("The XR camera (head). Used only by the fallback shoulder estimate.")]
        [SerializeField] private Transform _head;
        [Tooltip("UI-only ray interactor on this GameObject; its UI press is set to this reader at start.")]
        [SerializeField] private XRRayInteractor _ray;
        [Tooltip("The Meta aim pose is only trusted if it starts within this distance of the tracked palm.")]
        [SerializeField, Min(0.05f)] private float _maxAimOffsetFromHand = 0.25f;
        [Header("Fallback aim (no Meta aim pose)")]
        [SerializeField] private float _shoulderDrop = 0.15f;
        [SerializeField] private float _shoulderWidth = 0.18f;
        [Header("Fallback press: pinch 0..1")]
        [SerializeField, Range(0f, 1f)] private float _press = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _release = 0.5f;

        private ButtonHysteresis _pinch;
        private bool _pressed;
        private int _pressedFrame = -1;
        private int _releasedFrame = -1;
        private float _value;
        private bool _tracked;
        private bool _warnedFarAim;

        /// <summary>True while the ray is driven by the Meta aim pose (false = fallback estimate or untracked).</summary>
        public bool UsingMetaAim { get; private set; }

        private void Awake()
        {
            _pinch = new ButtonHysteresis(_press, _release);
            if (_ray != null)
            {
                _ray.uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ObjectReference;
                _ray.uiPressInput.SetObjectReference(this);
                // Same pinch selects 3D push buttons at a distance (the ray only sees the UI button layer).
                _ray.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ObjectReference;
                _ray.selectInput.SetObjectReference(this);
            }
        }

        private void Update()
        {
            bool pressedNow;
            if (TryReadMetaAim(out var aimPose, out bool aimPinching, out float aimStrength))
            {
                UsingMetaAim = true;
                _tracked = true;
                transform.SetPositionAndRotation(aimPose.position, aimPose.rotation);
                _value = aimStrength;
                pressedNow = aimPinching;
                _pinch.Reset(); // keep the fallback in a clean state for when it takes over
            }
            else
            {
                UsingMetaAim = false;
                _tracked = _hand != null && _hand.isActiveAndEnabled && _hand.Current.IsTracked;
                _value = _tracked ? _hand.Current.Pinch : 0f;
                _pinch.Update(_value);
                pressedNow = _pinch.IsPressed;
                if (_tracked) AimFromShoulder();
            }

            if (pressedNow && !_pressed)
            {
                _pressedFrame = Time.frameCount;
                Debug.Log($"[EVTrainer] {(_hand != null ? _hand.Handedness.ToString() : "?")} hand UI pinch ({(UsingMetaAim ? "Meta aim pose" : "fallback aim")}).");
            }
            if (!pressedNow && _pressed) _releasedFrame = Time.frameCount;
            _pressed = pressedNow;
        }

        /// <summary>Reads the Meta aim device for this hand. False if missing, untracked, or its pose is not valid.</summary>
        private bool TryReadMetaAim(out Pose worldPose, out bool pinching, out float strength)
        {
            worldPose = default;
            pinching = false;
            strength = 0f;
#if ENABLE_VR
            if (_hand == null || !_hand.isActiveAndEnabled) return false;
            var device = _hand.Handedness == Handedness.Left ? MetaAimHand.left : MetaAimHand.right;
            if (device == null || !device.added || !device.isTracked.isPressed) return false;

            var flags = (MetaAimFlags)(ulong)device.aimFlags.ReadValue();
            const MetaAimFlags usable = MetaAimFlags.Computed | MetaAimFlags.Valid;
            if ((flags & usable) != usable) return false;

            var position = device.devicePosition.ReadValue();
            var rotation = device.deviceRotation.ReadValue();
            if (_trackingSpace != null)
            {
                position = _trackingSpace.TransformPoint(position);
                rotation = _trackingSpace.rotation * rotation;
            }
            worldPose = new Pose(position, rotation);

            // Sanity check: the aim ray must start at the hand. If the pose is far from the tracked hand
            // (e.g. a space mismatch), ignore it and use the fallback rather than draw a ray from nowhere.
            if (_hand.Current.IsTracked)
            {
                var palm = _hand.JointPositions[(int)HandJoint.Palm].ToUnity();
                if ((position - palm).sqrMagnitude > _maxAimOffsetFromHand * _maxAimOffsetFromHand)
                {
                    if (!_warnedFarAim)
                    {
                        Debug.LogWarning($"[EVTrainer] {_hand.Handedness} Meta aim pose is {(position - palm).magnitude:0.00} m from the hand; using fallback aim.");
                        _warnedFarAim = true;
                    }
                    return false;
                }
            }

            // While the system gesture (palm-up menu) is active the pinch belongs to the OS, not our UI.
            pinching = (flags & MetaAimFlags.IndexPinching) != 0 && (flags & MetaAimFlags.SystemGesture) == 0;
            strength = device.pinchStrengthIndex.ReadValue();
            return true;
#else
            return false;
#endif
        }

        private void AimFromShoulder()
        {
            if (_head == null) return;
            var knuckle = _hand.JointPositions[(int)HandJoint.IndexProximal].ToUnity();
            var right = Vector3.ProjectOnPlane(_head.right, Vector3.up).normalized;
            float side = _hand.Handedness == Handedness.Left ? -1f : 1f;
            var shoulder = _head.position + Vector3.down * _shoulderDrop + right * (_shoulderWidth * side);
            var direction = knuckle - shoulder;
            if (direction.sqrMagnitude < 1e-6f) return;
            transform.SetPositionAndRotation(knuckle, Quaternion.LookRotation(direction.normalized, Vector3.up));
        }

        public bool ReadIsPerformed() => _pressed;
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
