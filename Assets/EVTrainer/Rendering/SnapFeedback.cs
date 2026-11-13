using System.Collections;
using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Visual feedback when a part snaps into place: a brief green highlight of the part's renderers
    /// (the snap sound comes from <see cref="FeedbackAudio"/>).
    /// </summary>
    public sealed class SnapFeedback : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Color _highlightColor = new Color(0.2f, 0.95f, 0.35f, 1f);
        [SerializeField, Min(0.01f)] private float _highlightSeconds = 0.4f;

        private Renderer[] _renderers;
        private Color[] _baseColors;
        private MaterialPropertyBlock _block;
        private Coroutine _highlight;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var mat = _renderers[i].sharedMaterial;
                _baseColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
            }
            _block = new MaterialPropertyBlock();
        }

        public void PlayInstalled()
        {
            if (_highlight != null) StopCoroutine(_highlight);
            _highlight = StartCoroutine(Highlight());
        }

        private IEnumerator Highlight()
        {
            for (float t = 0f; t < _highlightSeconds; t += Time.deltaTime)
            {
                float k = t / _highlightSeconds;
                for (int i = 0; i < _renderers.Length; i++)
                {
                    _block.SetColor(BaseColorId, Color.Lerp(_highlightColor, _baseColors[i], k * k));
                    _renderers[i].SetPropertyBlock(_block);
                }
                yield return null;
            }
            // Clear the blocks so the renderers go back to the SRP Batcher path.
            foreach (var r in _renderers) r.SetPropertyBlock(null);
            _highlight = null;
        }
    }
}
