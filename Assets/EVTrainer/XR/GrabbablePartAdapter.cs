using System;
using System.Collections;
using EVTrainer.Core;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>A part released by the trainee: what it is, where it was let go and by what.</summary>
    public readonly struct PartRelease
    {
        public PartRelease(string typeId, string itemId, PartPose pose, InputSource source, string hand)
        {
            TypeId = typeId;
            ItemId = itemId;
            Pose = pose;
            Source = source;
            Hand = hand;
        }

        /// <summary>Part type in the module definition (identical parts share it).</summary>
        public string TypeId { get; }

        /// <summary>This physical part (its GameObject name), used for per-part attempt tracking.</summary>
        public string ItemId { get; }

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
        [Tooltip("Part type in the module definition (e.g. battery_module). Identical parts share a type.")]
        [FormerlySerializedAs("_partId")]
        [SerializeField] private string _partTypeId;

        private XRGrabInteractable _grab;
        private Rigidbody _rigidbody;
        private IXRSelectInteractor _lastInteractor;
        private Coroutine _moveRoutine;
        private PartPose _stagingPose;

        public string PartTypeId => _partTypeId;

        /// <summary>Unique name of this physical part (its GameObject name).</summary>
        public string ItemId => name;

        /// <summary>Where the part was laid out at start (its staging position).</summary>
        public PartPose StagingPose => _stagingPose;

        public bool IsHeld { get; private set; }

        /// <summary>True when the XRI grab interactable is enabled (grabbing allowed).</summary>
        public bool IsGrabbable => _grab != null && _grab.isActiveAndEnabled;

        /// <summary>True while a hand or controller is close enough to grab the part (XRI hover).</summary>
        public bool IsHovered => _grab != null && _grab.enabled && _grab.isHovered;

        /// <summary>"Left", "Right" or "" when not held.</summary>
        public string HeldBy { get; private set; } = "";

        /// <summary>True once the part has been locked onto its target (no longer grabbable).</summary>
        public bool IsLocked { get; private set; }

        /// <summary>Current world pose of the part.</summary>
        public PartPose CurrentPose => transform.ToPartPose();

        /// <summary>Raised when the trainee lets go of the part. Not raised for cancelled selections
        /// (e.g. hand tracking lost) so a drop caused by tracking loss never installs a part.</summary>
        public event Action<GrabbablePartAdapter, PartRelease> Released;

        /// <summary>Raised each time a hand or controller takes hold of the part.</summary>
        public event Action<GrabbablePartAdapter> Grabbed;

        private bool _initialised;

        private void Awake() => EnsureInitialised();

        // Other scripts may configure this part from their own Awake, before ours has run.
        private void EnsureInitialised()
        {
            if (_initialised) return;
            _initialised = true;
            _grab = GetComponent<XRGrabInteractable>();
            _rigidbody = GetComponent<Rigidbody>();
            _stagingPose = CurrentPose;
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

        /// <summary>Allows or blocks grabbing (e.g. before a training mode is chosen). Ignored once locked.</summary>
        public void SetGrabbable(bool grabbable)
        {
            EnsureInitialised();
            if (IsLocked) return;
            _grab.enabled = grabbable;
        }

        /// <summary>
        /// Heavy part: it only moves while two interactors (controllers, hands, or one of each) hold it.
        /// One grab leaves it where it is and raises <see cref="SecondHandNeeded"/>; letting go with either
        /// hand while lifting drops it and counts as the release.
        /// </summary>
        public void SetRequiresTwoHands(bool requiresTwoHands)
        {
            EnsureInitialised();
            RequiresTwoHands = requiresTwoHands;
            _grab.selectMode = requiresTwoHands ? InteractableSelectMode.Multiple : InteractableSelectMode.Single;
            SetTracking(!requiresTwoHands);
        }

        public bool RequiresTwoHands { get; private set; }

        /// <summary>True while the trainee is actually carrying the part (held, and by two hands if it needs two).</summary>
        public bool IsCarried => IsHeld && (!RequiresTwoHands || _lifted);

        /// <summary>Raised when a two-hand part is grabbed with only one hand.</summary>
        public event Action<GrabbablePartAdapter> SecondHandNeeded;

        private bool _lifted;
        private readonly TwoHandCarry _carry = new TwoHandCarry();

        private System.Numerics.Vector3 HandPoint(int i) =>
            _grab.interactorsSelecting[i].GetAttachTransform(_grab).position.ToNumerics();

        private void Update()
        {
            if (!RequiresTwoHands || !_lifted || _grab.interactorsSelecting.Count < 2 || IsLocked) return;
            _carry.Pose(HandPoint(0), HandPoint(1), out var position, out float yaw);
            var p = position.ToUnity();
            var r = Quaternion.Euler(0f, yaw, 0f); // level: no pitch or roll from hand noise
            transform.SetPositionAndRotation(p, r);
            if (_rigidbody != null)
            {
                _rigidbody.position = p;
                _rigidbody.rotation = r;
            }
        }

        private void SetTracking(bool track)
        {
            _grab.trackPosition = track;
            _grab.trackRotation = track;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            _lastInteractor = args.interactorObject;
            Refresh();
            Grabbed?.Invoke(this);
            if (!RequiresTwoHands) return;

            if (_grab.interactorsSelecting.Count >= 2)
            {
                // Two-hand carry: our script poses it (midpoint, yaw from the hands, kept level); XRI does not move it.
                _lifted = true;
                SetTracking(false);
                _carry.Begin(transform.position.ToNumerics(), transform.eulerAngles.y, HandPoint(0), HandPoint(1));
            }
            else
            {
                SetTracking(false); // stays put until the second hand grabs
                SecondHandNeeded?.Invoke(this);
            }
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            Refresh();
            var interactor = args.interactorObject;

            if (RequiresTwoHands)
            {
                if (!_lifted) return; // one-hand grab ended: it never moved, nothing to judge
                _lifted = false;
                SetTracking(false);
                // Drop it: the remaining hand lets go too (next frame, outside XRI's event processing).
                StartCoroutine(ReleaseRemainingNextFrame());
            }

            if (args.isCanceled || IsLocked) return;
            _lastInteractor = interactor;
            Released?.Invoke(this, new PartRelease(_partTypeId, ItemId, CurrentPose, SourceOf(interactor), HandOf(interactor)));
        }

        private IEnumerator ReleaseRemainingNextFrame()
        {
            yield return null;
            var manager = _grab.interactionManager;
            if (manager == null) yield break;
            var remaining = new System.Collections.Generic.List<IXRSelectInteractor>(_grab.interactorsSelecting);
            foreach (var interactor in remaining)
                manager.SelectExit(interactor, (IXRSelectInteractable)_grab);
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

        /// <summary>Haptic pulse on the controller that last grabbed or released this part. Hands have no haptics.</summary>
        public void PulseLastController(HapticPulse pulse) => HapticOutput.Send(_lastInteractor, pulse);

        /// <summary>
        /// Moves the part smoothly onto <paramref name="target"/> over <paramref name="duration"/> seconds
        /// and locks it there: kinematic and no longer grabbable.
        /// </summary>
        public void LockAt(PartPose target, float duration)
        {
            if (IsLocked) return;
            IsLocked = true;
            StartMove(target, duration, staysLocked: true);
        }

        /// <summary>
        /// Smoothly carries the part back to where it was laid out (e.g. after a wrong-order release).
        /// It cannot be grabbed while moving and is a normal physics part again afterwards.
        /// </summary>
        public void ReturnToStaging(float duration)
        {
            if (IsLocked) return;
            _lifted = false;
            StartMove(_stagingPose, duration, staysLocked: false);
        }

        /// <summary>True while the part is being carried back to its staging position.</summary>
        public bool IsReturning => _moveRoutine != null && !IsLocked;

        private void StartMove(PartPose target, float duration, bool staysLocked)
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(MoveRoutine(target.Position.ToUnity(), target.Rotation.ToUnity(), duration, staysLocked));
        }

        private IEnumerator MoveRoutine(Vector3 targetPosition, Quaternion targetRotation, float duration, bool staysLocked)
        {
            // Let XRI finish detaching this frame (it restores Rigidbody state and applies throw velocity).
            yield return null;

            _grab.enabled = false;
            bool wasKinematic = _rigidbody != null && _rigidbody.isKinematic;
            var interpolation = _rigidbody != null ? _rigidbody.interpolation : RigidbodyInterpolation.None;
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

            if (!staysLocked)
            {
                if (_rigidbody != null)
                {
                    _rigidbody.position = targetPosition;
                    _rigidbody.rotation = targetRotation;
                    _rigidbody.isKinematic = wasKinematic;
                    _rigidbody.interpolation = interpolation;
                }
                _grab.enabled = true;
            }
            _moveRoutine = null;
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
