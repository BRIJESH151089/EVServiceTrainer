using TMPro;
using UnityEngine;

namespace EVTrainer.UI
{
    /// <summary>
    /// Small world-space label (part name + one-line hint) that floats above the held part
    /// and turns to face the viewer (yaw only, so it stays upright).
    /// </summary>
    public sealed class PartLabelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [Tooltip("Camera the label faces. Defaults to the main camera.")]
        [SerializeField] private Transform _viewer;

        private string _shown;

        private void Awake()
        {
            if (_viewer == null && Camera.main != null) _viewer = Camera.main.transform;
            Hide();
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
            FaceViewer();
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void FaceViewer()
        {
            if (_viewer == null) return;
            var away = transform.position - _viewer.position; // UI front faces -forward, so look away from the viewer
            away.y = 0f;
            if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
