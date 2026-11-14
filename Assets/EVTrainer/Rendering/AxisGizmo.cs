using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Debug aid: three thin bars along a transform's local axes, X red, Y green, Z blue (Unity's colours), from the
    /// origin outwards. Used in Development builds to show the controller grip pose and the glove wrist pose.
    /// </summary>
    public sealed class AxisGizmo : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Adds a gizmo under <paramref name="target"/> (hidden until <see cref="SetVisible"/>).</summary>
        public static AxisGizmo Create(Transform target, Material material, float length, float thickness = 0.003f)
        {
            var root = new GameObject("Axis Gizmo").AddComponent<AxisGizmo>();
            root.transform.SetParent(target, false);
            var block = new MaterialPropertyBlock();
            Bar(root.transform, material, block, Vector3.right, length, thickness, Color.red);
            Bar(root.transform, material, block, Vector3.up, length, thickness, Color.green);
            Bar(root.transform, material, block, Vector3.forward, length, thickness, Color.blue);
            root.gameObject.SetActive(false);
            return root;
        }

        private static void Bar(Transform parent, Material material, MaterialPropertyBlock block, Vector3 axis, float length,
            float thickness, Color colour)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(bar.GetComponent<Collider>());
            bar.name = colour == Color.red ? "X" : colour == Color.green ? "Y" : "Z";
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = axis * (length * 0.5f);
            bar.transform.localScale = Vector3.one * thickness + axis * (length - thickness);
            var r = bar.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            block.SetColor(BaseColorId, colour);
            r.SetPropertyBlock(block);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
