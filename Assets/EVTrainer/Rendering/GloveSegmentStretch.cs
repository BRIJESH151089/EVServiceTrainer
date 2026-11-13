using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Fits the glove hand to the tracked finger lengths: each finger segment mesh hangs on a leaf "_Seg" bone under
    /// its joint, and is stretched along its bone axis to the distance to the next joint (Core
    /// <see cref="SegmentStretch"/>). Leaf bones only, so stretching never moves other joints. With fixed joints
    /// (controller-posed hands) every scale stays 1.
    /// </summary>
    public sealed class GloveSegmentStretch : MonoBehaviour
    {
        [SerializeField] private Transform[] _segments = new Transform[0];
        [SerializeField] private Transform[] _nextJoints = new Transform[0];
        [SerializeField] private float[] _bindLengths = new float[0];

        public void Configure(Transform[] segments, Transform[] nextJoints, float[] bindLengths)
        {
            _segments = segments;
            _nextJoints = nextJoints;
            _bindLengths = bindLengths;
        }

        private void OnEnable() => Application.onBeforeRender += Apply;
        private void OnDisable() => Application.onBeforeRender -= Apply;
        private void LateUpdate() => Apply();

        private void Apply()
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                var seg = _segments[i];
                var next = _nextJoints[i];
                if (seg == null || next == null) continue;
                float tracked = next.localPosition.magnitude; // next joint relative to this segment's joint
                float s = SegmentStretch.Scale(tracked, _bindLengths[i]);
                seg.localScale = new Vector3(1f, 1f, s);
            }
        }
    }
}
