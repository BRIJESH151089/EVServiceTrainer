using System;
using System.Collections.Generic;
using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Translucent target outlines, one per slot. The caller decides which slots are shown (Learn mode:
    /// the free slots of the current step), which one is "ready" (the held part would fit there, green) and flashes
    /// a ghost red after a rejected release. Drawn with the GhostRim shader (fresnel rim glow, slow pulse).
    /// </summary>
    public sealed class GhostPresenter : MonoBehaviour
    {
        [Serializable]
        public sealed class Ghost
        {
            public string partId;
            public GameObject view;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Ghost[] _ghosts = Array.Empty<Ghost>();
        [Tooltip("Ghost colour while the held part is within tolerance (\"release now\").")]
        [SerializeField] private Color _readyColor = new Color(0.25f, 1f, 0.4f, 0.5f);
        [Tooltip("Ghost colour for a moment after a rejected release (wrong order or orientation).")]
        [SerializeField] private Color _rejectColor = new Color(1f, 0.22f, 0.15f, 0.55f);
        [SerializeField, Min(0.1f)] private float _rejectSeconds = 0.8f;

        private readonly HashSet<string> _visible = new HashSet<string>(StringComparer.Ordinal);
        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _rejectBlock;
        private string _readyId;
        private string _rejectId;
        private float _rejectUntil;

        /// <summary>Slot ids whose ghosts are currently shown.</summary>
        public IReadOnlyCollection<string> VisiblePartIds => _visible;

        /// <summary>The first shown ghost, or null (convenience for single-ghost checks).</summary>
        public string VisiblePartId
        {
            get
            {
                foreach (var id in _visible) return id;
                return null;
            }
        }

        public string ReadyPartId => _readyId;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, _readyColor);
            _rejectBlock = new MaterialPropertyBlock();
            _rejectBlock.SetColor(BaseColorId, _rejectColor);
            Show((IEnumerable<string>)null);
        }

        /// <summary>Puts the ghost of slot <paramref name="partId"/> on its target pose (world space).</summary>
        public void Place(string partId, Vector3 position, Quaternion rotation)
        {
            var ghost = Find(partId);
            if (ghost != null) ghost.view.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Shows only the ghost of <paramref name="partId"/>; null hides all.</summary>
        public void Show(string partId) => Show(partId == null ? null : new[] { partId });

        /// <summary>Shows exactly the ghosts of <paramref name="partIds"/>; null or empty hides all.</summary>
        public void Show(IEnumerable<string> partIds)
        {
            _visible.Clear();
            if (partIds != null)
            {
                foreach (var id in partIds)
                {
                    if (Find(id) != null) _visible.Add(id);
                }
            }
            _readyId = null;
            foreach (var g in _ghosts)
            {
                if (g.view == null) continue;
                ApplyTint(g, false);
                g.view.SetActive(_visible.Contains(g.partId));
            }
        }

        /// <summary>
        /// Tints the shown ghost of <paramref name="partId"/> green ("release now"); null clears the tint.
        /// Only touches renderers when the ready slot changes.
        /// </summary>
        public void SetReady(string partId)
        {
            if (partId != null && !_visible.Contains(partId)) partId = null;
            if (_readyId == partId) return;
            var previous = Find(_readyId);
            if (previous != null) ApplyTint(previous, false);
            _readyId = partId;
            var next = Find(partId);
            if (next != null) ApplyTint(next, true);
        }

        /// <summary>Flashes the ghost of <paramref name="partId"/> red for a moment (a rejected release). Ignored if hidden.</summary>
        public void FlashReject(string partId)
        {
            var ghost = Find(partId);
            if (ghost == null || !_visible.Contains(partId)) return;
            if (_rejectId != null && _rejectId != partId) ApplyTint(Find(_rejectId), _rejectId == _readyId);
            _rejectId = partId;
            _rejectUntil = Time.time + _rejectSeconds;
            foreach (var r in ghost.view.GetComponentsInChildren<Renderer>(true)) r.SetPropertyBlock(_rejectBlock);
        }

        private void Update()
        {
            if (_rejectId == null || Time.time < _rejectUntil) return;
            var ghost = Find(_rejectId);
            _rejectId = null;
            if (ghost != null) ApplyTint(ghost, ghost.partId == _readyId);
        }

        private void ApplyTint(Ghost ghost, bool ready)
        {
            if (ghost == null) return;
            if (ghost.partId == _rejectId && Time.time < _rejectUntil) return; // the red flash wins until it ends
            foreach (var r in ghost.view.GetComponentsInChildren<Renderer>(true))
                r.SetPropertyBlock(ready ? _block : null);
        }

        private Ghost Find(string partId)
        {
            if (partId == null) return null;
            foreach (var g in _ghosts)
            {
                if (g.partId == partId && g.view != null) return g;
            }
            return null;
        }
    }
}
