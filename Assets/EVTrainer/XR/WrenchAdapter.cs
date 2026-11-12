using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>
    /// XRI side of the torque wrench. Measures only: where the holding hand is (to swing the handle about a bolt axis)
    /// and where the socket is. While fitted on a bolt it stops following the hand and is posed by the App
    /// (socket on the bolt head, handle pointing at the hand).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class WrenchAdapter : MonoBehaviour
    {
        private XRGrabInteractable _grab;
        private Rigidbody _rigidbody;
        private IXRSelectInteractor _interactor;

        public event Action<WrenchAdapter> Released;

        public bool IsHeld => _interactor != null;
        public bool IsHovered => _grab != null && _grab.isHovered;
        public bool HeldByHand => _interactor != null && _interactor.transform.GetComponentInParent<HandTrackingAdapter>() != null;

        /// <summary>World position of the hand / controller attach point holding the wrench (valid while held).</summary>
        public Vector3 HandPosition => _interactor != null ? _interactor.GetAttachTransform(_grab).position : transform.position;

        /// <summary>The socket's working point (the wrench pivot): sits on top of the bolt head when fitted.</summary>
        public Vector3 SocketPosition => transform.position;

        /// <summary>Socket axis (the wrench's local up).</summary>
        public Vector3 SocketAxis => transform.up;

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

        private void OnSelectEntered(SelectEnterEventArgs args) => _interactor = args.interactorObject;

        private void OnSelectExited(SelectExitEventArgs args)
        {
            _interactor = null;
            SetFitted(false);
            Released?.Invoke(this);
        }

        public void SetGrabbable(bool grabbable)
        {
            if (_grab == null) _grab = GetComponent<XRGrabInteractable>();
            _grab.enabled = grabbable;
        }

        /// <summary>Fitted on a bolt: no longer follows the hand; the App poses it each frame.</summary>
        public void SetFitted(bool fitted)
        {
            _grab.trackPosition = !fitted;
            _grab.trackRotation = !fitted;
        }

        public void SetPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (_rigidbody != null)
            {
                _rigidbody.position = position;
                _rigidbody.rotation = rotation;
            }
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
