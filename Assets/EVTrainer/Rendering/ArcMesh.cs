using System.Collections.Generic;
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
        // The torque ring is rebuilt on every torque change: fill shared buffers instead of new arrays (no garbage).
        private static readonly List<Vector3> s_Vertices = new List<Vector3>(256);
        private static readonly List<int> s_Triangles = new List<int>(1024);

        public static void Build(Mesh mesh, float innerRadius, float outerRadius, float fromDegrees, float toDegrees, float degreesPerSegment = 6f)
        {
            mesh.Clear();
            float span = toDegrees - fromDegrees;
            if (span <= 0.01f) return;
            int segments = Mathf.Max(1, Mathf.CeilToInt(span / degreesPerSegment));
            s_Vertices.Clear();
            s_Triangles.Clear();
            for (int i = 0; i <= segments; i++)
            {
                float a = (fromDegrees + span * i / segments) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                s_Vertices.Add(dir * innerRadius);
                s_Vertices.Add(dir * outerRadius);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                // top face (seen from +Y) and bottom face
                AddTriangle(a, b, c);
                AddTriangle(b, d, c);
                AddTriangle(a, c, b);
                AddTriangle(b, c, d);
            }
            mesh.SetVertices(s_Vertices);
            mesh.SetTriangles(s_Triangles, 0);
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
            s_Vertices.Clear();
            s_Triangles.Clear();
            s_Vertices.Add(centre - radial * halfWidth);
            s_Vertices.Add(centre + radial * halfWidth);
            s_Vertices.Add(centre + tangent * length);
            AddTriangle(0, 1, 2);
            AddTriangle(0, 2, 1);
            mesh.SetVertices(s_Vertices);
            mesh.SetTriangles(s_Triangles, 0);
            mesh.RecalculateBounds();
        }

        private static void AddTriangle(int a, int b, int c)
        {
            s_Triangles.Add(a);
            s_Triangles.Add(b);
            s_Triangles.Add(c);
        }
    }
}
