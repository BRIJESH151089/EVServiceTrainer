using EVTrainer.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>
    /// Wraps an XRGrabInteractable and exposes its grab state and world pose as plain Core data.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class GrabbablePartAdapter : MonoBehaviour
    {
        private XRGrabInteractable _grab;

        public bool IsHeld { get; private set; }

        /// <summary>"Left", "Right" or "" when not held.</summary>
        public string HeldBy { get; private set; } = "";

        /// <summary>Current world pose of the part.</summary>
        public PartPose CurrentPose => transform.ToPartPose();

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            _grab.selectEntered.AddListener(OnSelectChanged);
            _grab.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            _grab.selectEntered.RemoveListener(OnSelectChanged);
            _grab.selectExited.RemoveListener(OnSelectExited);
        }

        private void OnSelectChanged(SelectEnterEventArgs _) => Refresh();
        private void OnSelectExited(SelectExitEventArgs _) => Refresh();

        private void Refresh()
        {
            IsHeld = _grab.isSelected;
            if (!IsHeld)
            {
                HeldBy = "";
                return;
            }

            var interactor = _grab.interactorsSelecting[0];
            HeldBy = interactor.handedness switch
            {
                InteractorHandedness.Left => "Left",
                InteractorHandedness.Right => "Right",
                _ => "Unknown",
            };
        }
    }
}
