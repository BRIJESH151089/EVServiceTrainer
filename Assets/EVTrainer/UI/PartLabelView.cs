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

        private void Awake()
        {
            var billboard = GetComponent<YawBillboard>();
            if (billboard == null) billboard = gameObject.AddComponent<YawBillboard>();
            billboard.TiltBackDegrees = _tiltBackDegrees;
            if (_shown == null) Hide();
        }

        public void Show(string title, string hint, Vector3 worldPosition)
        {
            string text = string.IsNullOrEmpty(hint) ? $"<b>{title}</b>" : $"<b>{title}</b>\n<size=80%>{hint}</size>";
            if (text != _shown && _text != null)
            {
                _text.text = text;
                _shown = text;
            }
            transform.position = worldPosition;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public bool IsShown => gameObject.activeSelf;
    }
}
