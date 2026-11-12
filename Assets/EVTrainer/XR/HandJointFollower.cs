using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.XR
{
    /// <summary>
    /// Keeps this transform on one tracked hand joint (e.g. the index fingertip for a poke interactor),
    /// pointing along the finger (from the previous joint to this one).
    /// </summary>
    [DefaultExecutionOrder(-29975)] // after HandTrackingAdapter (-29980), before XRI's interaction manager
    public sealed class HandJointFollower : MonoBehaviour
    {
        [SerializeField] private HandTrackingAdapter _hand;
        [SerializeField] private HandJoint _joint = HandJoint.IndexTip;
        [Tooltip("Joint before it along the finger; sets the forward (pointing) direction.")]
        [SerializeField] private HandJoint _previousJoint = HandJoint.IndexDistal;

        private void Update()
        {
            if (_hand == null || !_hand.isActiveAndEnabled || !_hand.Current.IsTracked) return;
            var tip = _hand.JointPositions[(int)_joint].ToUnity();
            var prev = _hand.JointPositions[(int)_previousJoint].ToUnity();
            var dir = tip - prev;
            if (dir.sqrMagnitude > 1e-8f) transform.SetPositionAndRotation(tip, Quaternion.LookRotation(dir.normalized, Vector3.up));
            else transform.position = tip;
        }
    }
}
