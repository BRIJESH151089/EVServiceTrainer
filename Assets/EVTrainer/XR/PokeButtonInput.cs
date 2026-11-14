using System.Collections.Generic;
using EVTrainer.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
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

        private static readonly List<PokeButtonInput> s_Active = new List<PokeButtonInput>();

        private XRSimpleInteractable _interactable;
        private IXRInteractor _lastPresser;

        /// <summary>Pulse on the pressing controller (set by the composition root from the haptic profiles).</summary>
        public HapticPulse ClickPulse { get; set; } = HapticProfiles.Default.ForCue(FeedbackCue.UiPress);

        /// <summary>Distance from <paramref name="point"/> to the nearest enabled button (infinity if none).</summary>
        public static float NearestDistance(Vector3 point)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < s_Active.Count; i++) best = Mathf.Min(best, Vector3.Distance(point, s_Active[i].transform.position));
            return best;
        }

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            _interactable.hoverEntered.AddListener(OnHoverEntered);
        }

        private void OnEnable() => s_Active.Add(this);
        private void OnDisable() => s_Active.Remove(this);

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

        public void NotifyClicked() => HapticOutput.Send(_lastPresser, ClickPulse);
    }
}
