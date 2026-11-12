using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>
    /// XRI side of one hand-held bolt. Measures only: the "virtual" orientation the trainee's hand would give the bolt
    /// (interactor attach rotation x grab offset) so the App can derive twist about a hole axis and tilt from it.
    /// Can pin the bolt to a pose (on the hole axis) while still held, lock it in place, or free it to physics.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class BoltAdapter : MonoBehaviour
    {
        private XRGrabInteractable _grab;
        private Rigidbody _rigidbody;
        private IXRSelectInteractor _interactor;
        private Quaternion _gripOffset = Quaternion.identity;
        private bool _lockedInPlace;
        private bool _initialised;

        public event Action<BoltAdapter> Grabbed;
        public event Action<BoltAdapter> Released;

        public bool IsHeld => _interactor != null;
        public bool IsHovered => _grab != null && _grab.enabled && _grab.isHovered;
        public bool HeldByHand => _interactor != null && _interactor.transform.GetComponentInParent<HandTrackingAdapter>() != null;

        /// <summary>Orientation the bolt would have if it followed the hand freely (valid while held).</summary>
        public Quaternion VirtualRotation =>
            _interactor != null ? _interactor.GetAttachTransform(_grab).rotation * _gripOffset : transform.rotation;

        /// <summary>World position of the hand / controller attach point holding the bolt (valid while held).</summary>
        public Vector3 HandPosition => _interactor != null ? _interactor.GetAttachTransform(_grab).position : transform.position;

        private void Awake() => EnsureInitialised();

        private void EnsureInitialised()
        {
            if (_initialised) return;
            _initialised = true;
            _grab = GetComponent<XRGrabInteractable>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            EnsureInitialised();
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
            _interactor = args.interactorObject;
            RecaptureGrip();
            Grabbed?.Invoke(this);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            _interactor = null;
            SetTracking(true, true);
            // XRI has just restored the Rigidbody; a bolt left in a hole stays put, anything else falls.
            if (_rigidbody != null) _rigidbody.isKinematic = _lockedInPlace;
            Released?.Invoke(this);
        }

        /// <summary>Takes the current hand-to-bolt relation as the reference for <see cref="VirtualRotation"/>.</summary>
        public void RecaptureGrip()
        {
            if (_interactor == null) return;
            _gripOffset = Quaternion.Inverse(_interactor.GetAttachTransform(_grab).rotation) * transform.rotation;
        }

        /// <summary>While held: does XRI move / rotate the bolt with the hand?</summary>
        public void SetTracking(bool position, bool rotation)
        {
            _grab.trackPosition = position;
            _grab.trackRotation = rotation;
        }

        /// <summary>Places the bolt (transform and body) at a pose, e.g. on a hole axis.</summary>
        public void SetPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (_rigidbody != null)
            {
                _rigidbody.position = position;
                _rigidbody.rotation = rotation;
            }
        }

        public void SetPosition(Vector3 position) => SetPose(position, transform.rotation);

        /// <summary>True: the bolt stays exactly where it is when let go (in a hole). False: normal physics.</summary>
        public void SetLockedInPlace(bool locked)
        {
            _lockedInPlace = locked;
            if (_rigidbody == null || IsHeld) return;
            if (!locked && !_rigidbody.isKinematic) return;
            if (locked)
            {
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                _rigidbody.isKinematic = true;
            }
            else _rigidbody.isKinematic = false;
        }

        public void SetGrabbable(bool grabbable)
        {
            EnsureInitialised();
            _grab.enabled = grabbable;
        }

        /// <summary>Makes the holding hand / controller let go now (e.g. once the bolt is seated).</summary>
        public void ForceRelease()
        {
            if (_interactor == null || _grab.interactionManager == null) return;
            _grab.interactionManager.SelectExit(_interactor, (IXRSelectInteractable)_grab);
        }

        /// <summary>Haptic pulse on the holding controller (hands have none).</summary>
        public void Pulse(float amplitude, float duration)
        {
            if (_interactor == null || HeldByHand) return;
            var player = _interactor.transform.GetComponentInParent<HapticImpulsePlayer>();
            if (player != null) player.SendHapticImpulse(amplitude, duration);
        }
    }
}
