using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Flat ring-sector meshes in the local XZ plane (normal +Y). Angles are degrees clockwise seen from above,
    /// starting at local +Z (Unity: rotating +Z about +Y by a positive angle), matching the Core tightening sense.
    /// Both faces are emitted so the band shows from either side.
    /// </summary>
    public static class ArcMesh
    {
        public static void Build(Mesh mesh, float innerRadius, float outerRadius, float fromDegrees, float toDegrees, float degreesPerSegment = 6f)
        {
            mesh.Clear();
            float span = toDegrees - fromDegrees;
            if (span <= 0.01f) return;
            int segments = Mathf.Max(1, Mathf.CeilToInt(span / degreesPerSegment));
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 12];
            for (int i = 0; i <= segments; i++)
            {
                float a = (fromDegrees + span * i / segments) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                vertices[i * 2] = dir * innerRadius;
                vertices[i * 2 + 1] = dir * outerRadius;
            }
            for (int i = 0, t = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                // top face (seen from +Y) and bottom face
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        /// <summary>Flat triangle pointing clockwise (an arrow head) at <paramref name="atDegrees"/> on the given radius.</summary>
        public static void BuildArrowHead(Mesh mesh, float radius, float atDegrees, float length, float halfWidth)
        {
            mesh.Clear();
            float a = atDegrees * Mathf.Deg2Rad;
            var radial = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var tangent = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)); // clockwise direction of travel
            var centre = radial * radius;
            mesh.vertices = new[] { centre - radial * halfWidth, centre + radial * halfWidth, centre + tangent * length };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 };
            mesh.RecalculateBounds();
        }
    }
}
