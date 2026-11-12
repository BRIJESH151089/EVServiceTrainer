using System;
using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Translucent target outlines, one per part. At most one is visible: the part the trainee
    /// should fit next (Learn mode only; the caller decides which).
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

        private MaterialPropertyBlock _block;
        private bool _ready;

        public string VisiblePartId { get; private set; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, _readyColor);
            Show(null);
        }

        /// <summary>
        /// Tints the visible ghost green when <paramref name="ready"/> (the held part is within tolerance),
        /// otherwise back to its normal material colour. Only touches renderers when the state changes.
        /// </summary>
        public void SetReady(bool ready)
        {
            if (_ready == ready) return;
            _ready = ready;
            var ghost = Find(VisiblePartId);
            if (ghost != null) ApplyTint(ghost, ready);
        }

        /// <summary>Puts the ghost of <paramref name="partId"/> on its target pose (world space).</summary>
        public void Place(string partId, Vector3 position, Quaternion rotation)
        {
            var ghost = Find(partId);
            if (ghost != null) ghost.view.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Shows only the ghost of <paramref name="partId"/>; null hides all.</summary>
        public void Show(string partId)
        {
            VisiblePartId = Find(partId) != null ? partId : null;
            _ready = false;
            foreach (var g in _ghosts)
            {
                if (g.view == null) continue;
                ApplyTint(g, false);
                g.view.SetActive(g.partId == VisiblePartId);
            }
        }

        private void ApplyTint(Ghost ghost, bool ready)
        {
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
