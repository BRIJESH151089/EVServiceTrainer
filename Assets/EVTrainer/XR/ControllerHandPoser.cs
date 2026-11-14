using System.Collections.Generic;
using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.XR
{
    /// <summary>
    /// Shows a glove hand on a controller instead of a controller model. This component sits on the glove root, a
    /// child of the controller whose pose is the OpenXR GRIP pose (devicePosition / deviceRotation; axes documented
    /// on Core <see cref="GripPose"/>). The wrist is placed in grip space by Core <see cref="GloveMount"/> from per-side
    /// data, and the fingers are curled from grip and trigger (Core <see cref="ControllerHandPose"/>), each joint about
    /// Core <see cref="GloveHandLayout.CurlAxis"/> so a curl always moves the fingertip towards the palm.
    /// Bones follow the XR Hands rig names (L_/R_ + Thumb/Index/Middle/Ring/Little + Proximal/Intermediate/Distal;
    /// thumb: Metacarpal/Proximal/Distal).
    /// </summary>
    public sealed class ControllerHandPoser : MonoBehaviour
    {
        [SerializeField] private ControllerInputAdapter _input;
        [Tooltip("Root of the hand rig (the glove's wrist).")]
        [SerializeField] private Transform _handRoot;
        [SerializeField, Range(0.01f, 0.5f)] private float _easeSeconds = 0.06f;

        [Header("Mount on the controller grip pose (per side, data)")]
        [SerializeField] private HandSide _side = HandSide.Left;
        [Tooltip("Point of the LEFT glove (glove space: +Z wrist → fingers, +Y back of the hand, thumb +X) that sits on " +
                 "the grip origin: the palm centroid, slightly inside the handle. Mirrored for the right hand.")]
        [SerializeField] private Vector3 _gripPointInGlove = new Vector3(0f, -0.025f, 0.06f);
        [Tooltip("The glove's forward turns from grip +Y (wrist → knuckles) towards grip +Z (thumb end) by this much, " +
                 "because the handle crosses the palm obliquely.")]
        [SerializeField, Range(-60f, 60f)] private float _handTiltDegrees = GloveMount.DefaultHandTiltDegrees;
        [Tooltip("Place the wrist from the data above at start (off: keep the transform as placed in the scene).")]
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

        public HandSide Side => _side;

        /// <summary>The glove's wrist (hand root).</summary>
        public Transform Wrist => _handRoot != null ? _handRoot : transform;

        /// <summary>The wrist pose in grip space from the per-side data.</summary>
        public GloveMount Mount => GloveMount.Fit(_side, _gripPointInGlove.ToNumerics(), _handTiltDegrees);

        private void Awake()
        {
            if (_handRoot == null) _handRoot = transform;
            if (_applyMount)
            {
                var mount = Mount;
                _handRoot.SetLocalPositionAndRotation(mount.Position.ToUnity(), mount.Rotation.ToUnity());
            }
            foreach (Finger finger in System.Enum.GetValues(typeof(Finger)))
            {
                var chain = GloveHandLayout.CurlChain(finger);
                for (int i = 0; i < chain.Count - 1; i++)
                {
                    var bone = Find(chain[i]);
                    var next = Find(chain[i + 1]);
                    if (bone == null || next == null) continue;
                    // Bone direction in glove space → Core curl axis (towards the palm) → the bone's local space.
                    var direction = _handRoot.InverseTransformDirection(next.position - bone.position).normalized;
                    var axisWorld = _handRoot.TransformDirection(GloveHandLayout.CurlAxis(direction.ToNumerics()).ToUnity());
                    _joints.Add(new Joint
                    {
                        Bone = bone,
                        Bind = bone.localRotation,
                        Axis = Quaternion.Inverse(bone.rotation) * axisWorld,
                        Finger = finger,
                        Index = i,
                    });
                }
            }
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
