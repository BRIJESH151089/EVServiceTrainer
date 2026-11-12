using System;
using System.Collections;
using EVTrainer.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>A part released by the trainee: where it was let go and by what.</summary>
    public readonly struct PartRelease
    {
        public PartRelease(string partId, PartPose pose, InputSource source, string hand)
        {
            PartId = partId;
            Pose = pose;
            Source = source;
            Hand = hand;
        }

        public string PartId { get; }
        public PartPose Pose { get; }
        public InputSource Source { get; }

        /// <summary>"Left", "Right" or "Unknown".</summary>
        public string Hand { get; }
    }

    /// <summary>
    /// Wraps an XRGrabInteractable: exposes grab state and pose as plain Core data, reports
    /// releases (controller or hand), and can lock the part onto its target pose.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class GrabbablePartAdapter : MonoBehaviour
    {
        [Tooltip("Id of the part in the module definition.")]
        [SerializeField] private string _partId;

        private XRGrabInteractable _grab;
        private Rigidbody _rigidbody;
        private IXRSelectInteractor _lastInteractor;
        private Coroutine _lockRoutine;

        public string PartId => _partId;

        public bool IsHeld { get; private set; }

        /// <summary>"Left", "Right" or "" when not held.</summary>
        public string HeldBy { get; private set; } = "";

        /// <summary>True once the part has been locked onto its target (no longer grabbable).</summary>
        public bool IsLocked { get; private set; }

        /// <summary>Current world pose of the part.</summary>
        public PartPose CurrentPose => transform.ToPartPose();

        /// <summary>Raised when the trainee lets go of the part. Not raised for cancelled selections
        /// (e.g. hand tracking lost) so a drop caused by tracking loss never installs a part.</summary>
        public event Action<PartRelease> Released;

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            _grab.selectEntered.AddListener(OnSelectEntered);
            _grab.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            _grab.selectEntered.RemoveListener(OnSelectEntered);
            _grab.selectExited.RemoveListener(OnSelectExited);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            _lastInteractor = args.interactorObject;
            Refresh();
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            Refresh();
            if (args.isCanceled || IsLocked) return;

            var interactor = args.interactorObject;
            _lastInteractor = interactor;
            Released?.Invoke(new PartRelease(_partId, CurrentPose, SourceOf(interactor), HandOf(interactor)));
        }

        /// <summary>
        /// True when the part has come to rest under physics: its Rigidbody is sleeping, kinematic,
        /// or moving slower than the given thresholds.
        /// </summary>
        public bool IsSettled(float maxLinearSpeed, float maxAngularSpeed)
        {
            if (_rigidbody == null || _rigidbody.isKinematic || _rigidbody.IsSleeping()) return true;
            return _rigidbody.linearVelocity.magnitude <= maxLinearSpeed
                && _rigidbody.angularVelocity.magnitude <= maxAngularSpeed;
        }

        /// <summary>Haptic pulse on the controller that last released this part. Hands have no haptics.</summary>
        public void PulseReleasingController(float amplitude, float duration)
        {
            if (_lastInteractor == null || SourceOf(_lastInteractor) != InputSource.Controller) return;
            var player = _lastInteractor.transform.GetComponentInParent<HapticImpulsePlayer>();
            if (player != null) player.SendHapticImpulse(amplitude, duration);
        }

        /// <summary>
        /// Moves the part smoothly onto <paramref name="target"/> over <paramref name="duration"/> seconds
        /// and locks it there: kinematic and no longer grabbable.
        /// </summary>
        public void LockAt(PartPose target, float duration)
        {
            if (IsLocked) return;
            IsLocked = true;
            if (_lockRoutine != null) StopCoroutine(_lockRoutine);
            _lockRoutine = StartCoroutine(LockRoutine(target.Position.ToUnity(), target.Rotation.ToUnity(), duration));
        }

        private IEnumerator LockRoutine(Vector3 targetPosition, Quaternion targetRotation, float duration)
        {
            // Let XRI finish detaching this frame (it restores Rigidbody state and applies throw velocity).
            yield return null;

            _grab.enabled = false;
            if (_rigidbody != null)
            {
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                _rigidbody.isKinematic = true;
                _rigidbody.interpolation = RigidbodyInterpolation.None;
            }

            var startPosition = transform.position;
            var startRotation = transform.rotation;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, targetPosition, k),
                    Quaternion.Slerp(startRotation, targetRotation, k));
                yield return null;
            }
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            _lockRoutine = null;
        }

        private void Refresh()
        {
            IsHeld = _grab.isSelected;
            HeldBy = IsHeld ? HandOf(_grab.interactorsSelecting[0]) : "";
        }

        private static InputSource SourceOf(IXRInteractor interactor) =>
            interactor.transform.GetComponentInParent<HandTrackingAdapter>() != null ? InputSource.Hand : InputSource.Controller;

        private static string HandOf(IXRInteractor interactor) => interactor.handedness switch
        {
            InteractorHandedness.Left => "Left",
            InteractorHandedness.Right => "Right",
            _ => "Unknown",
        };
    }
}
