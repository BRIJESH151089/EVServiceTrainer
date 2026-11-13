using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Colours a part's mesh renderers without new materials (MaterialPropertyBlock on _BaseColor):
    /// a state tint (e.g. bolt yellow / green / red; null = original colour) and a subtle hover highlight
    /// ("you can grab this") that lightens whatever colour is showing.
    /// </summary>
    public sealed class PartTint : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("How far the hover highlight lightens the colour towards the highlight colour (0..1).")]
        [SerializeField, Range(0f, 1f)] private float _highlightAmount = 0.35f;
        [SerializeField] private Color _highlightColor = new Color(1f, 0.95f, 0.75f);

        private MeshRenderer[] _renderers;
        private Color[] _baseColors;
        private MaterialPropertyBlock _block;
        private Color? _tint;
        private bool _highlighted;

        private void Awake() => Init();

        private void Init()
        {
            if (_renderers != null) return;
            // Only lit-material meshes (not text, whose shader has no _BaseColor).
            var all = GetComponentsInChildren<MeshRenderer>(true);
            var list = new System.Collections.Generic.List<MeshRenderer>(all.Length);
            foreach (var r in all)
            {
                var m = r.sharedMaterial;
                if (m != null && m.HasProperty(BaseColorId)) list.Add(r);
            }
            _renderers = list.ToArray();
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].sharedMaterial.GetColor(BaseColorId);
            _block = new MaterialPropertyBlock();
        }

        public bool IsHighlighted => _highlighted;

        /// <summary>State colour; null = the material's own colour.</summary>
        public void SetTint(Color? color)
        {
            Init();
            if (_tint == color) return;
            _tint = color;
            Apply();
        }

        /// <summary>Hover highlight on / off (e.g. a hand or controller is close enough to grab the part).</summary>
        public void SetHighlighted(bool highlighted)
        {
            Init();
            if (_highlighted == highlighted) return;
            _highlighted = highlighted;
            Apply();
        }

        private void Apply()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                if (!_tint.HasValue && !_highlighted)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                var c = _tint ?? _baseColors[i];
                if (_highlighted)
                {
                    float a = c.a;
                    c = Color.Lerp(c, _highlightColor, _highlightAmount);
                    c.a = a;
                }
                _block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
