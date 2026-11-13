using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// A soft round contact shadow lying on a surface (quad in its local XZ plane), sized and faded by the caller:
    /// stronger when the held part is close to the surface, gone when it is far. Opaque queue with alpha-to-coverage.
    /// </summary>
    public sealed class BlobShadowView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Mesh s_Quad;

        [SerializeField] private Material _material;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;

        private void Awake() => Init();

        private void Init()
        {
            if (_renderer != null) return;
            if (s_Quad == null)
            {
                s_Quad = new Mesh { name = "BlobShadowQuad" };
                s_Quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
                s_Quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
                s_Quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                s_Quad.RecalculateBounds();
            }
            gameObject.AddComponent<MeshFilter>().sharedMesh = s_Quad;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _block = new MaterialPropertyBlock();
            Hide();
        }

        public void SetMaterial(Material material)
        {
            _material = material;
            if (_renderer != null) _renderer.sharedMaterial = material;
        }

        /// <param name="point">Surface point under the part.</param>
        /// <param name="normal">Surface normal.</param>
        /// <param name="size">Blob diameter in metres.</param>
        /// <param name="strength">0 (invisible) .. 1 (darkest).</param>
        public void Show(Vector3 point, Vector3 normal, float size, float strength)
        {
            Init();
            if (strength <= 0.01f) { Hide(); return; }
            if (!_renderer.enabled) _renderer.enabled = true;
            transform.SetPositionAndRotation(point + normal * 0.002f, Quaternion.FromToRotation(Vector3.up, normal));
            transform.localScale = new Vector3(size, 1f, size);
            _block.SetColor(BaseColorId, new Color(0f, 0f, 0f, Mathf.Clamp01(strength) * 0.6f));
            _renderer.SetPropertyBlock(_block);
        }

        public void Hide()
        {
            if (_renderer != null && _renderer.enabled) _renderer.enabled = false;
        }
    }
}
