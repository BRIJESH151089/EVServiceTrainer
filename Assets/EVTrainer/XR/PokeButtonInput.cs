using EVTrainer.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>
    /// XRI side of a physical push button, poke only: an XRPokeInteractor (fingertip / controller tip) hovering this
    /// XRSimpleInteractable (gated by an XRPokeFilter so only front-on pokes count) pushes the cap; the press depth is
    /// how far the tip has gone past the cap's rest face along this transform's forward (push direction).
    /// Haptic pulse on the controller that pressed it.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class PokeButtonInput : MonoBehaviour, IPushButtonInput
    {
        [Tooltip("Cap travel in metres (same as the view's).")]
        [SerializeField, Min(0.001f)] private float _travel = 0.009f;
        [SerializeField, Range(0f, 1f)] private float _hapticAmplitude = 0.35f;
        [SerializeField, Min(0f)] private float _hapticSeconds = 0.04f;

        private XRSimpleInteractable _interactable;
        private IXRInteractor _lastPresser;

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            _interactable.hoverEntered.AddListener(OnHoverEntered);
        }

        private void OnDestroy()
        {
            if (_interactable != null) _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (args.interactorObject is XRPokeInteractor) _lastPresser = args.interactorObject;
        }

        public float PressDepth01
        {
            get
            {
                if (_interactable == null || !_interactable.isHovered) return 0f;
                float deepest = 0f;
                var hovering = _interactable.interactorsHovering;
                for (int i = 0; i < hovering.Count; i++)
                {
                    if (!(hovering[i] is XRPokeInteractor poke)) continue;
                    var tip = poke.GetAttachTransform(_interactable).position;
                    float pushedIn = Vector3.Dot(tip - transform.position, transform.forward); // > 0 once past the rest face
                    deepest = Mathf.Max(deepest, Mathf.Clamp01(pushedIn / _travel));
                }
                return deepest;
            }
        }

        public void NotifyClicked()
        {
            if (_lastPresser == null || _lastPresser.transform == null) return;
            if (_lastPresser.transform.GetComponentInParent<HandTrackingAdapter>() != null) return; // hands: no haptics
            var player = _lastPresser.transform.GetComponentInParent<HapticImpulsePlayer>();
            if (player != null) player.SendHapticImpulse(_hapticAmplitude, _hapticSeconds);
        }
    }
}
