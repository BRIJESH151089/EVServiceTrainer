using System.Collections.Generic;
using EVTrainer.Core;
using EVTrainer.Rendering;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// Soft contact shadow under every held part, bolt or tool, so depth above the bench is easy to read without
    /// real-time shadows. Casts straight down past the held object, sizes the blob to its footprint and fades it with
    /// height (Core <see cref="ContactShadow"/>).
    /// </summary>
    public sealed class HeldPartShadows : MonoBehaviour
    {
        [SerializeField] private AssemblyTrainingController _training;
        [SerializeField] private BoltingController _bolting;
        [SerializeField] private Material _shadowMaterial;
        [SerializeField, Min(0.05f)] private float _fadeHeightMetres = 0.45f;
        [SerializeField, Range(1, 4)] private int _maxShadows = 3;

        private readonly List<Transform> _held = new List<Transform>();
        private readonly List<BlobShadowView> _pool = new List<BlobShadowView>();
        private readonly Dictionary<Transform, Renderer[]> _renderers = new Dictionary<Transform, Renderer[]>();
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private void LateUpdate()
        {
            _held.Clear();
            if (_training != null) _training.CollectCarried(_held);
            if (_bolting != null) _bolting.CollectHeld(_held);

            int used = 0;
            foreach (var t in _held)
            {
                if (used >= _maxShadows) break;
                if (!TryBounds(t, out var b)) continue;
                int n = Physics.RaycastNonAlloc(new Ray(b.center, Vector3.down), _hits, 3f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                RaycastHit hit = default;
                for (int i = 0; i < n; i++)
                {
                    var h = _hits[i];
                    if (h.collider.transform.IsChildOf(t) || IsHeld(h.collider.transform)) continue;
                    if (h.distance < best) { best = h.distance; hit = h; }
                }
                if (best == float.MaxValue) continue;
                float strength = ContactShadow.Strength(b.min.y - hit.point.y, _fadeHeightMetres);
                float size = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 1.15f, 0.04f, 0.8f);
                Get(used++).Show(hit.point, hit.normal, size, strength);
            }
            for (int i = used; i < _pool.Count; i++) _pool[i].Hide();
        }

        private bool IsHeld(Transform t)
        {
            foreach (var h in _held) if (t.IsChildOf(h)) return true;
            return false;
        }

        private BlobShadowView Get(int i)
        {
            while (_pool.Count <= i)
            {
                var go = new GameObject("Held Part Shadow " + _pool.Count);
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<BlobShadowView>();
                view.SetMaterial(_shadowMaterial);
                _pool.Add(view);
            }
            return _pool[i];
        }

        private bool TryBounds(Transform t, out Bounds bounds)
        {
            bounds = default;
            if (!_renderers.TryGetValue(t, out var rs)) _renderers[t] = rs = t.GetComponentsInChildren<Renderer>();
            bool any = false;
            foreach (var r in rs)
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
