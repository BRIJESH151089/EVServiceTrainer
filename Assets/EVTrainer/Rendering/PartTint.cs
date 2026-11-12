using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>Colours a part's renderers (e.g. bolt state: yellow, green, red) without new materials. Null = original colour.</summary>
    public sealed class PartTint : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private Color? _current;

        private void Awake() => Init();

        private void Init()
        {
            if (_renderers != null) return;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        public void SetTint(Color? color)
        {
            Init();
            if (_current == color) return;
            _current = color;
            if (color.HasValue) _block.SetColor(BaseColorId, color.Value);
            foreach (var r in _renderers)
            {
                if (r is MeshRenderer) r.SetPropertyBlock(color.HasValue ? _block : null);
            }
        }
    }
}
