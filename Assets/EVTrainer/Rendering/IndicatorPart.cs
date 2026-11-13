using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>One coloured procedural mesh (runtime child), coloured with a property block on a shared unlit material.</summary>
    internal sealed class IndicatorPart
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly MeshRenderer _renderer;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Color _color = new Color(-1f, -1f, -1f);

        public IndicatorPart(Transform parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Mesh = new Mesh { name = name };
            Mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = Mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            Transform = go.transform;
        }

        public Mesh Mesh { get; }
        public Transform Transform { get; }

        public void SetColor(Color color)
        {
            if (color == _color) return;
            _color = color;
            _block.SetColor(BaseColorId, color);
            _renderer.SetPropertyBlock(_block);
        }

        public void SetVisible(bool visible)
        {
            if (_renderer.enabled != visible) _renderer.enabled = visible;
        }
    }
}
