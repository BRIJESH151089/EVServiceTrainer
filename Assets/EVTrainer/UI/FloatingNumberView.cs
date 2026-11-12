using TMPro;
using UnityEngine;

namespace EVTrainer.UI
{
    /// <summary>Learn mode: a pattern number floating above a bolt (1–8), turned toward the viewer; the next one highlighted.</summary>
    public sealed class FloatingNumberView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Color _normal = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color _next = new Color(1f, 0.83f, 0.3f, 1f);
        [SerializeField] private Color _done = new Color(0.45f, 0.9f, 0.5f, 0.8f);
        [SerializeField] private Color _fault = new Color(1f, 0.35f, 0.3f, 1f);

        public enum Look { Normal, Next, Done, Fault }

        private Transform _viewer;
        private Look _look = (Look)(-1);

        public void Show(int number, Vector3 position, Look look)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.position = position;
            if (_text != null && look != _look)
            {
                _look = look;
                _text.text = look == Look.Next ? $"<b>{number}</b>" : number.ToString();
                _text.color = look == Look.Next ? _next : look == Look.Done ? _done : look == Look.Fault ? _fault : _normal;
                transform.localScale = Vector3.one * (look == Look.Next ? 1.35f : 1f);
            }
            if (_viewer == null && Camera.main != null) _viewer = Camera.main.transform;
            if (_viewer != null)
            {
                var away = transform.position - _viewer.position; // text front faces -Z: look away from the viewer
                away.y = 0f;
                if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
