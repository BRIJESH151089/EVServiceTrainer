using System;
using EVTrainer.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>
    /// XRI side of the torque wrench. Measures only: where the holding hand is and where the socket mouth is.
    /// Once attached to a bolt, XRI stops moving it (no position/rotation tracking, no Rigidbody interpolation)
    /// and the pose set by the App is the only thing that places it, re-applied every frame up to rendering, also
    /// while it is parked on the bolt (let go). The trainee can hold the handle anywhere (dynamic attach).
    /// It never falls: kinematic without gravity whenever it is not held.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class WrenchAdapter : MonoBehaviour
    {
        [Tooltip("Open end of the socket (sits on the bolt head when attached). Defaults to this transform.")]
        [SerializeField] private Transform _socketMouth;

        private XRGrabInteractable _grab;
        private Rigidbody _rigidbody;
        private IXRSelectInteractor _interactor;
        private bool _attached;
        private Vector3 _drivenPosition;
        private Quaternion _drivenRotation;
        private RigidbodyInterpolation _interpolation;
        private ToolHome _home;

        public event Action<WrenchAdapter> Grabbed;

        /// <summary>Let go. The App decides what happens (park on the bolt, float, or return to the stand); it never falls.</summary>
        public event Action<WrenchAdapter> Released;

        public bool IsHeld => _interactor != null;
        public bool IsHovered => _grab != null && _grab.isHovered;
        public bool IsAttached => _attached;
        public bool HeldByHand => _interactor != null && _interactor.transform.GetComponentInParent<HandTrackingAdapter>() != null;

        /// <summary>World position of the hand / controller attach point holding the wrench (valid while held).</summary>
        public Vector3 HandPosition => _interactor != null ? _interactor.GetAttachTransform(_grab).position : transform.position;

        /// <summary>Open end of the socket.</summary>
        public Vector3 SocketMouth => _socketMouth != null ? _socketMouth.position : transform.position;

        /// <summary>Socket axis (the wrench's local up).</summary>
        public Vector3 SocketAxis => transform.up;

        /// <summary>Distance from the socket mouth to the wrench origin, along the socket axis.</summary>
        public float MouthToOrigin => _socketMouth != null ? Vector3.Dot(transform.position - _socketMouth.position, transform.up) : 0f;

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _rigidbody = GetComponent<Rigidbody>();
            _home = new ToolHome(this, _rigidbody);
            _home.HoldStill(); // rests on its stand without physics
        }

        /// <summary>Stays exactly where it is (kinematic, no gravity).</summary>
        public void HoldStill() => _home.HoldStill();

        /// <summary>Glides back to its place on the tool stand.</summary>
        public void ReturnHome(float seconds)
        {
            SetAttached(false);
            _home.Return(seconds);
        }

        public bool IsReturningHome => _home != null && _home.IsReturning;

        private void OnEnable()
        {
            _grab.selectEntered.AddListener(OnSelectEntered);
            _grab.selectExited.AddListener(OnSelectExited);
            Application.onBeforeRender += ApplyDrivenPose;
        }

        private void OnDisable()
        {
            _grab.selectEntered.RemoveListener(OnSelectEntered);
            _grab.selectExited.RemoveListener(OnSelectExited);
            Application.onBeforeRender -= ApplyDrivenPose;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            _interactor = args.interactorObject;
            _home.Cancel();
            Grabbed?.Invoke(this);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            _interactor = null;
            // XRI has restored the Rigidbody; hold still first so it never drops, then let the App decide.
            _home.HoldStill();
            Released?.Invoke(this);
        }

        public void SetGrabbable(bool grabbable)
        {
            if (_grab == null) _grab = GetComponent<XRGrabInteractable>();
            _grab.enabled = grabbable;
        }

        /// <summary>
        /// Attached to a bolt: XRI no longer moves the wrench and the Rigidbody does not interpolate, so only
        /// <see cref="Drive"/> places it. Detached: it follows the hand again from where the hand holds it.
        /// </summary>
        public void SetAttached(bool attached)
        {
            if (_attached == attached) return;
            _attached = attached;
            _grab.trackPosition = !attached;
            _grab.trackRotation = !attached;
            if (_rigidbody == null) return;
            if (attached)
            {
                _interpolation = _rigidbody.interpolation;
                _rigidbody.interpolation = RigidbodyInterpolation.None;
                _drivenPosition = transform.position;
                _drivenRotation = transform.rotation;
            }
            else _rigidbody.interpolation = _interpolation;
        }

        /// <summary>Attached: the pose to hold this frame (re-applied late in the frame and before rendering).</summary>
        public void Drive(Vector3 position, Quaternion rotation)
        {
            _drivenPosition = position;
            _drivenRotation = rotation;
            ApplyDrivenPose();
        }

        private void LateUpdate() => ApplyDrivenPose();

        private void ApplyDrivenPose()
        {
            if (!_attached) return;
            transform.SetPositionAndRotation(_drivenPosition, _drivenRotation);
            if (_rigidbody != null)
            {
                _rigidbody.position = _drivenPosition;
                _rigidbody.rotation = _drivenRotation;
            }
        }

        /// <summary>Haptic pulse on the holding controller (hands have none).</summary>
        public void Pulse(HapticPulse pulse) => HapticOutput.Send(_interactor, pulse);
    }
}
