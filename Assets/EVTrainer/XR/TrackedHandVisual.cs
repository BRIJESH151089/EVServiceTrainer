using UnityEngine;
using UnityEngine.XR.Hands;

namespace EVTrainer.XR
{
    /// <summary>
    /// Skinned hand mesh that follows the tracked hand joints (XR Hands <see cref="XRHandSkeletonDriver"/> +
    /// <see cref="XRHandTrackingEvents"/> on the same object). Visible only while the hand is tracked. While holding
    /// something, the mesh is shifted by the same smoothing offset the grab uses, so the hand and the held part move
    /// together instead of jittering against each other.
    /// </summary>
    public sealed class TrackedHandVisual : MonoBehaviour
    {
        [SerializeField] private XRHandTrackingEvents _events;
        [SerializeField] private XRHandSkeletonDriver _driver;
        [SerializeField] private Renderer _renderer;
        [Tooltip("This hand's adapter: provides the grab smoothing offset.")]
        [SerializeField] private HandTrackingAdapter _hand;

        private void Update()
        {
            bool tracked = _events != null && _events.handIsTracked;
            if (_renderer != null && _renderer.enabled != tracked) _renderer.enabled = tracked;
            if (_driver == null) return;
            if (_hand != null && _hand.IsGrabbing) _driver.ApplyRootPoseOffset(_hand.GrabSmoothingOffset);
            else _driver.ResetRootPoseOffset();
        }
    }
}
