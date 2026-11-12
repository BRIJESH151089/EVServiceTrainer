using System.Collections;
using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Feedback when a part snaps into place: a short self-made click (synthesised at start-up,
    /// no audio asset) and a brief green highlight of the part's renderers.
    /// </summary>
    public sealed class SnapFeedback : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static AudioClip s_Click;

        [SerializeField] private Color _highlightColor = new Color(0.2f, 0.95f, 0.35f, 1f);
        [SerializeField, Min(0.01f)] private float _highlightSeconds = 0.4f;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;

        private Renderer[] _renderers;
        private Color[] _baseColors;
        private MaterialPropertyBlock _block;
        private AudioSource _audio;
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

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.minDistance = 0.5f;
            _audio.clip = s_Click != null ? s_Click : s_Click = CreateClick();
        }

        public void PlayInstalled()
        {
            _audio.PlayOneShot(_audio.clip, _volume);
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

        /// <summary>Two-part metallic click: a bright 2.4 kHz tick over a short 160 Hz thump, ~60 ms.</summary>
        private static AudioClip CreateClick()
        {
            const int sampleRate = 44100;
            int length = sampleRate * 60 / 1000;
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float tick = Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t * 180f);
                float thump = Mathf.Sin(2f * Mathf.PI * 160f * t) * Mathf.Exp(-t * 60f);
                data[i] = 0.55f * tick + 0.45f * thump;
            }
            var clip = AudioClip.Create("SnapClick", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
