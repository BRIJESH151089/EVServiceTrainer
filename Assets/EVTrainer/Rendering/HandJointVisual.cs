using System.Collections.Generic;
using EVTrainer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Self-made debug hand: a small sphere on every joint and a thin line along each finger.
    /// Driven with plain joint positions; hidden while the hand is not tracked.
    /// </summary>
    public sealed class HandJointVisual : MonoBehaviour
    {
        [SerializeField] private Mesh _sphereMesh;
        [SerializeField] private Material _material;
        [SerializeField] private float _jointDiameter = 0.012f;
        [SerializeField] private float _lineWidth = 0.004f;

        private readonly List<Transform> _jointTransforms = new List<Transform>(HandSkeleton.JointCount);
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private LineRenderer[] _lines;
        private bool _visible = true;

        private void Awake()
        {
            for (int i = 0; i < HandSkeleton.JointCount; i++)
            {
                var go = new GameObject(((HandJoint)i).ToString());
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * _jointDiameter;
                go.AddComponent<MeshFilter>().sharedMesh = _sphereMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _material;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _jointTransforms.Add(go.transform);
                _renderers.Add(mr);
            }

            _lines = new LineRenderer[HandSkeleton.FingerChains.Count];
            for (int c = 0; c < _lines.Length; c++)
            {
                var go = new GameObject("Finger " + c);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = _material;
                lr.useWorldSpace = true;
                lr.widthMultiplier = _lineWidth;
                lr.positionCount = HandSkeleton.FingerChains[c].Count;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                _lines[c] = lr;
                _renderers.Add(lr);
            }

            SetVisible(false);
        }

        /// <summary>Updates the joints from world-space positions indexed by <see cref="HandJoint"/>.</summary>
        public void Render(IReadOnlyList<System.Numerics.Vector3> worldJoints, bool tracked)
        {
            SetVisible(tracked);
            if (!tracked || worldJoints == null) return;

            for (int i = 0; i < _jointTransforms.Count; i++)
                _jointTransforms[i].position = ToUnity(worldJoints[i]);

            for (int c = 0; c < _lines.Length; c++)
            {
                var chain = HandSkeleton.FingerChains[c];
                for (int k = 0; k < chain.Count; k++)
                    _lines[c].SetPosition(k, ToUnity(worldJoints[(int)chain[k]]));
            }
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            foreach (var r in _renderers) r.enabled = visible;
        }

        private static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
