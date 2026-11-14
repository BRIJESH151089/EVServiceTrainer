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

        [Header("Fit to text (part labels)")]
        [Tooltip("On: the text stays on one line (no word wrap) at the font size below, and the background is sized to " +
                 "the text plus the padding, so the text never spills outside it. Off: the label keeps its layout.")]
        [SerializeField] private bool _fitToText;
        [SerializeField, Min(1f)] private float _fontSize = 50f;
        [Tooltip("Space between the text and the edge of the background (label units).")]
        [SerializeField] private Vector2 _padding = new Vector2(18f, 8f);

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
                if (text != _shown)
                {
                    _text.text = text;
                    if (_fitToText) FitTo(text);
                }
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
        /// One line at <see cref="_fontSize"/>, and the label (its background stretches with it) sized to the text plus
        /// the padding. The text rect keeps its insets inside the label, so the padding is added on top of them.
        /// </summary>
        private void FitTo(string text)
        {
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.enableAutoSizing = false;
            _text.fontSize = _fontSize;
            var label = transform as RectTransform;
            var textRect = _text.rectTransform;
            if (label == null) return;
            Vector2 preferred = _text.GetPreferredValues(text);
            Vector2 insets = -textRect.sizeDelta; // the text rect is stretched with these insets (e.g. 24 x 12)
            label.sizeDelta = new Vector2(preferred.x + insets.x + 2f * _padding.x, preferred.y + insets.y + 2f * _padding.y);
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
