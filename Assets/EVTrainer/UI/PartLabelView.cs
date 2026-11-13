using TMPro;
using UnityEngine;

namespace EVTrainer.UI
{
    /// <summary>
    /// Small world-space label (part name, optional one-line hint) floating above a part. It turns smoothly toward
    /// the viewer (yaw only, <see cref="YawBillboard"/>, late in the frame) so it stays upright and never jitters.
    /// </summary>
    public sealed class PartLabelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [Tooltip("Labels sit below eye level: tilt them back so they face up toward the viewer.")]
        [SerializeField, Range(0f, 80f)] private float _tiltBackDegrees = 35f;

        private string _shown;
        private string _shownTitle;
        private string _shownHint;

        private void Awake()
        {
            var billboard = GetComponent<YawBillboard>();
            if (billboard == null) billboard = gameObject.AddComponent<YawBillboard>();
            billboard.TiltBackDegrees = _tiltBackDegrees;
            if (_shown == null) Hide();
        }

        public void Show(string title, string hint, Vector3 worldPosition)
        {
            // Labels are re-placed every few frames: only build the rich text when the words change.
            if (_text != null && (_shown == null || !ReferenceEquals(title, _shownTitle) || !ReferenceEquals(hint, _shownHint)))
            {
                string text = string.IsNullOrEmpty(hint) ? $"<b>{title}</b>" : $"<b>{title}</b>\n<size=80%>{hint}</size>";
                if (text != _shown) _text.text = text;
                _shown = text;
                _shownTitle = title;
                _shownHint = hint;
            }
            transform.position = worldPosition;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>
        /// Lays out <paramref name="title"/> once now (e.g. at scene start) so the text buffers are sized before the
        /// label is first shown mid-animation (no allocation spike then). Leaves the label hidden.
        /// </summary>
        public void Prewarm(string title, string hint = null)
        {
            Show(title, hint, transform.position);
            if (_text != null) _text.ForceMeshUpdate(true);
            Hide();
        }

        public bool IsShown => gameObject.activeSelf;
    }
}
