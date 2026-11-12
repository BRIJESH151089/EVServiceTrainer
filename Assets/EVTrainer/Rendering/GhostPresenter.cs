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

        [SerializeField] private Ghost[] _ghosts = Array.Empty<Ghost>();

        public string VisiblePartId { get; private set; }

        private void Awake() => Show(null);

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
            foreach (var g in _ghosts)
            {
                if (g.view != null) g.view.SetActive(g.partId == VisiblePartId);
            }
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
