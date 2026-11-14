using System.Collections.Generic;
using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.XR
{
    /// <summary>
    /// Shows a hand on a controller instead of a controller model: the hand mesh is attached at the controller grip
    /// pose and its fingers are posed procedurally from grip and trigger (Core <see cref="ControllerHandPose"/>).
    /// Bones follow the XR Hands rig names (L_/R_ + Thumb/Index/Middle/Ring/Little + Proximal/Intermediate/Distal;
    /// thumb: Metacarpal/Proximal/Distal). The bend direction of each finger is found once at start by testing which
    /// rotation moves its tip towards the palm, so left and right rigs work without hand-tuned signs.
    /// </summary>
    public sealed class ControllerHandPoser : MonoBehaviour
    {
        [SerializeField] private ControllerInputAdapter _input;
        [Tooltip("Root of the hand rig (e.g. the LeftHand model instance).")]
        [SerializeField] private Transform _handRoot;
        [SerializeField, Range(0.01f, 0.5f)] private float _easeSeconds = 0.06f;

        [Header("Mount on the controller's OpenXR grip pose (per side)")]
        [Tooltip("Grip pose axes: +Z through the finger tunnel towards the thumb/thumbstick end, +Y from the wrist " +
                 "towards the knuckles, +X out of the back of the LEFT hand (into the palm of the right). Hand root " +
                 "(wrist) position in grip space.")]
        [SerializeField] private Vector3 _mountPosition = new Vector3(0.03f, -0.07f, 0f);
        [Tooltip("Hand root rotation in grip space (Euler). The hand's +Z (wrist → fingers) goes along grip +Y, its " +
                 "+Y (back of the hand) out of the back of the hand.")]
        [SerializeField] private Vector3 _mountEuler = new Vector3(270f, 270f, 0f); // left; right = (270, 90, 0)
        [Tooltip("Apply the mount above at start (off: keep the transform as placed in the scene).")]
        [SerializeField] private bool _applyMount = true;

        private sealed class Joint
        {
            public Transform Bone;
            public Quaternion Bind;
            public Vector3 Axis;
            public Finger Finger;
            public int Index;
        }

        private readonly List<Joint> _joints = new List<Joint>();
        private FingerCurls _curls = ControllerHandPose.FromInput(0f, 0f);

        private void Awake()
        {
            if (_handRoot == null) _handRoot = transform;
            if (_applyMount) _handRoot.SetLocalPositionAndRotation(_mountPosition, Quaternion.Euler(_mountEuler));
            Transform palm = Find("Palm");
            var palmDown = palm != null ? -palm.up : -_handRoot.up;
            foreach (Finger finger in System.Enum.GetValues(typeof(Finger)))
            {
                string[] names = finger == Finger.Thumb
                    ? new[] { "ThumbMetacarpal", "ThumbProximal", "ThumbDistal" }
                    : new[] { finger + "Proximal", finger + "Intermediate", finger + "Distal" };
                var tip = Find(finger + "Tip");
                for (int i = 0; i < names.Length; i++)
                {
                    var bone = Find(names[i]);
                    if (bone == null) continue;
                    _joints.Add(new Joint { Bone = bone, Bind = bone.localRotation, Axis = BendAxis(bone, tip, palmDown), Finger = finger, Index = i });
                }
            }
        }

        /// <summary>Local X, signed so a positive rotation moves the fingertip towards the palm.</summary>
        private static Vector3 BendAxis(Transform bone, Transform tip, Vector3 palmDown)
        {
            if (tip == null) return Vector3.right;
            var bind = bone.localRotation;
            var before = tip.position;
            bone.localRotation = bind * Quaternion.AngleAxis(20f, Vector3.right);
            var moved = tip.position - before;
            bone.localRotation = bind;
            return Vector3.Dot(moved, palmDown) >= 0f ? Vector3.right : Vector3.left;
        }

        private Transform Find(string suffix)
        {
            foreach (var t in _handRoot.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(suffix, System.StringComparison.Ordinal) && (t.name.StartsWith("L_") || t.name.StartsWith("R_"))) return t;
            return null;
        }

        private void LateUpdate()
        {
            var frame = _input != null ? _input.Current : InputFrame.None;
            var target = ControllerHandPose.FromInput(frame.Grip, frame.Trigger);
            _curls = ControllerHandPose.Ease(_curls, target, Time.deltaTime, _easeSeconds);
            foreach (var j in _joints)
                j.Bone.localRotation = j.Bind * Quaternion.AngleAxis(ControllerHandPose.JointAngleDegrees(j.Finger, j.Index, _curls[j.Finger]), j.Axis);
        }
    }
}
