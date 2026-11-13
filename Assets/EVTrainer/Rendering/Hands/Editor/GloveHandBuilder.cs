using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EVTrainer.Rendering.Hands.Editor
{
    /// <summary>
    /// Builds the self-made low-poly glove hands (left and right): a rounded palm block, tapered capsule finger
    /// segments with rounded tips, and a wrist cuff (accent material). Skinned to a bone hierarchy that matches the
    /// XR Hands joint layout (L_/R_ Wrist, Palm, and per finger Metacarpal/Proximal/Intermediate/Distal/Tip; the thumb
    /// has Metacarpal/Proximal/Distal/Tip), so the XR Hands skeleton driver and the controller finger poser drive it.
    /// Bind pose: palm down, fingers along +Z, back of the hand +Y (OpenXR joint convention); left thumb on +X.
    /// Each finger segment hangs on a leaf "_Seg" bone so it can be stretched to tracked lengths.
    /// Output: Assets/EVTrainer/Rendering/Hands/GloveHand{Left,Right}.asset (mesh) and .prefab.
    /// </summary>
    public static class GloveHandBuilder
    {
        private const string Folder = "Assets/EVTrainer/Rendering/Hands";
        private const int Sides = 8;

        // Average adult hand, left side, metres, relative to the wrist joint.
        private static readonly Dictionary<string, Vector3> Joints = new Dictionary<string, Vector3>
        {
            ["Palm"] = new Vector3(0.000f, 0.002f, 0.050f),
            ["ThumbMetacarpal"] = new Vector3(0.024f, -0.010f, 0.020f), ["ThumbProximal"] = new Vector3(0.046f, -0.016f, 0.052f),
            ["ThumbDistal"] = new Vector3(0.060f, -0.018f, 0.082f), ["ThumbTip"] = new Vector3(0.070f, -0.018f, 0.104f),
            ["IndexMetacarpal"] = new Vector3(0.020f, 0.002f, 0.012f), ["IndexProximal"] = new Vector3(0.024f, 0.004f, 0.090f),
            ["IndexIntermediate"] = new Vector3(0.026f, 0.004f, 0.130f), ["IndexDistal"] = new Vector3(0.027f, 0.003f, 0.154f),
            ["IndexTip"] = new Vector3(0.028f, 0.002f, 0.176f),
            ["MiddleMetacarpal"] = new Vector3(0.005f, 0.003f, 0.012f), ["MiddleProximal"] = new Vector3(0.004f, 0.005f, 0.093f),
            ["MiddleIntermediate"] = new Vector3(0.004f, 0.005f, 0.138f), ["MiddleDistal"] = new Vector3(0.004f, 0.004f, 0.166f),
            ["MiddleTip"] = new Vector3(0.004f, 0.003f, 0.190f),
            ["RingMetacarpal"] = new Vector3(-0.010f, 0.002f, 0.012f), ["RingProximal"] = new Vector3(-0.014f, 0.004f, 0.087f),
            ["RingIntermediate"] = new Vector3(-0.017f, 0.004f, 0.128f), ["RingDistal"] = new Vector3(-0.019f, 0.003f, 0.154f),
            ["RingTip"] = new Vector3(-0.020f, 0.002f, 0.176f),
            ["LittleMetacarpal"] = new Vector3(-0.022f, 0.000f, 0.012f), ["LittleProximal"] = new Vector3(-0.031f, 0.000f, 0.077f),
            ["LittleIntermediate"] = new Vector3(-0.037f, 0.000f, 0.108f), ["LittleDistal"] = new Vector3(-0.041f, -0.001f, 0.128f),
            ["LittleTip"] = new Vector3(-0.044f, -0.002f, 0.148f),
        };

        // Chains (joint order) and per-segment radii (start, end).
        private static readonly (string[] chain, Vector2[] radii)[] Fingers =
        {
            (new[] { "ThumbMetacarpal", "ThumbProximal", "ThumbDistal", "ThumbTip" }, new[] { new Vector2(0.0140f, 0.0125f), new Vector2(0.0120f, 0.0108f), new Vector2(0.0108f, 0.0094f) }),
            (new[] { "IndexMetacarpal", "IndexProximal", "IndexIntermediate", "IndexDistal", "IndexTip" }, new[] { Vector2.zero, new Vector2(0.0098f, 0.0090f), new Vector2(0.0090f, 0.0082f), new Vector2(0.0082f, 0.0074f) }),
            (new[] { "MiddleMetacarpal", "MiddleProximal", "MiddleIntermediate", "MiddleDistal", "MiddleTip" }, new[] { Vector2.zero, new Vector2(0.0102f, 0.0094f), new Vector2(0.0094f, 0.0086f), new Vector2(0.0086f, 0.0077f) }),
            (new[] { "RingMetacarpal", "RingProximal", "RingIntermediate", "RingDistal", "RingTip" }, new[] { Vector2.zero, new Vector2(0.0095f, 0.0087f), new Vector2(0.0087f, 0.0080f), new Vector2(0.0080f, 0.0072f) }),
            (new[] { "LittleMetacarpal", "LittleProximal", "LittleIntermediate", "LittleDistal", "LittleTip" }, new[] { Vector2.zero, new Vector2(0.0083f, 0.0076f), new Vector2(0.0076f, 0.0069f), new Vector2(0.0069f, 0.0062f) }),
        };

        [MenuItem("EV Trainer/Build Glove Hands")]
        public static void BuildAll()
        {
            var glove = LoadOrCreateMaterial("HandGlove", new Color(0.24f, 0.29f, 0.37f));
            var accent = LoadOrCreateMaterial("GloveAccent", new Color(0.05f, 0.62f, 0.62f));
            Build(true, glove, accent);
            Build(false, glove, accent);
            AssetDatabase.SaveAssets();
        }

        private static Material LoadOrCreateMaterial(string name, Color colour)
        {
            var path = $"Assets/EVTrainer/Rendering/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Smoothness", 0.3f);
            m.SetColor("_SpecColor", new Color(0.12f, 0.12f, 0.12f));
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        private sealed class MeshData
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<BoneWeight> Weights = new List<BoneWeight>();
            public readonly List<int>[] Triangles = { new List<int>(), new List<int>() };
        }

        private static void Build(bool left, Material glove, Material accent)
        {
            string side = left ? "Left" : "Right";
            char p = left ? 'L' : 'R';
            float mirror = left ? 1f : -1f;
            Vector3 Pos(string joint) => Vector3.Scale(Joints[joint], new Vector3(mirror, 1f, 1f));

            // Bone hierarchy (world = root space at bind).
            var root = new GameObject("GloveHand" + side);
            var wrist = NewBone($"{p}_Wrist", root.transform, Vector3.zero, Quaternion.identity);
            var palm = NewBone($"{p}_Palm", wrist, Pos("Palm"), Quaternion.identity);
            var bones = new List<Transform> { wrist, palm };
            var segs = new List<Transform>(); var nexts = new List<Transform>(); var lengths = new List<float>();
            var data = new MeshData();

            foreach (var (chain, radii) in Fingers)
            {
                Transform parent = wrist;
                var created = new Transform[chain.Length];
                for (int i = 0; i < chain.Length; i++)
                {
                    var pos = Pos(chain[i]);
                    var dir = i + 1 < chain.Length ? Pos(chain[i + 1]) - pos : pos - Pos(chain[i - 1]);
                    var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    created[i] = NewBone($"{p}_{chain[i]}", parent, pos, rot);
                    parent = created[i];
                }
                for (int i = 0; i < chain.Length - 1; i++)
                {
                    var r = radii[i];
                    if (r == Vector2.zero) continue; // finger metacarpals sit inside the palm block
                    var joint = created[i];
                    var seg = NewBone(joint.name + "_Seg", joint, joint.position, joint.rotation);
                    float length = Vector3.Distance(joint.position, created[i + 1].position);
                    bool isTip = i == chain.Length - 2;
                    bones.Add(seg); segs.Add(seg); nexts.Add(created[i + 1]); lengths.Add(length);
                    AddCapsule(data, seg, bones.Count - 1, length, r.x, r.y, isTip);
                }
            }

            // Rounded palm block (palm bone) and wrist cuff (wrist bone, accent).
            float xMin = left ? -0.046f : -0.036f, xMax = left ? 0.036f : 0.046f;
            AddRoundedBox(data, 1, new Vector3((xMin + xMax) * 0.5f, -0.0005f, 0.043f), new Vector3(xMax - xMin, 0.027f, 0.094f), 0.012f, 0);
            AddCuff(data, 0, new Vector3(-0.005f * mirror, -0.001f, -0.018f), 0.043f, 0.027f, 0.040f);

            // Mesh with bind poses.
            var mesh = new Mesh { name = "GloveHand" + side };
            mesh.SetVertices(data.Vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(data.Triangles[0], 0);
            mesh.SetTriangles(data.Triangles[1], 1);
            mesh.boneWeights = data.Weights.ToArray();
            var bindposes = new Matrix4x4[bones.Count];
            for (int i = 0; i < bones.Count; i++) bindposes[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
            mesh.bindposes = bindposes;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var meshPath = $"{Folder}/GloveHand{side}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else
            {
                existing.Clear(); // rewrite in place: keeps the asset id and re-uploads to the GPU
                existing.SetVertices(data.Vertices);
                existing.subMeshCount = 2;
                existing.SetTriangles(data.Triangles[0], 0);
                existing.SetTriangles(data.Triangles[1], 1);
                existing.boneWeights = data.Weights.ToArray();
                existing.bindposes = bindposes;
                existing.RecalculateNormals();
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }

            var skin = new GameObject("Glove");
            skin.transform.SetParent(root.transform, false);
            var smr = skin.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones.ToArray();
            smr.rootBone = wrist;
            smr.sharedMaterials = new[] { glove, accent };
            smr.localBounds = new Bounds(new Vector3(0f, 0f, 0.07f), new Vector3(0.24f, 0.16f, 0.32f));
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.receiveShadows = false;
            smr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            smr.skinnedMotionVectors = false;
            smr.quality = SkinQuality.Bone1;

            var stretch = root.AddComponent<GloveSegmentStretch>();
            stretch.Configure(segs.ToArray(), nexts.ToArray(), lengths.ToArray());

            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/GloveHand{side}.prefab");
            Object.DestroyImmediate(root);
            Debug.Log($"[EVTrainer] Glove hand {side}: {mesh.vertexCount} verts, {(data.Triangles[0].Count + data.Triangles[1].Count) / 3} tris, {bones.Count} skin bones");
        }

        private static Transform NewBone(string name, Transform parent, Vector3 worldPos, Quaternion worldRot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(worldPos, worldRot);
            return t;
        }

        private static void AddTriangle(MeshData d, int sub, int a, int b, int c, Vector3 centroid)
        {
            var va = d.Vertices[a]; var vb = d.Vertices[b]; var vc = d.Vertices[c];
            var outward = (va + vb + vc) / 3f - centroid;
            // Unity front faces: clockwise seen from outside = the cross product points outwards.
            if (Vector3.Dot(Vector3.Cross(vb - va, vc - va), outward) >= 0f) d.Triangles[sub].AddRange(new[] { a, b, c });
            else d.Triangles[sub].AddRange(new[] { a, c, b });
        }

        private static int AddVertex(MeshData d, Vector3 v, int bone)
        {
            d.Vertices.Add(v);
            d.Weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
            return d.Vertices.Count - 1;
        }

        /// <summary>Tapered capsule along the bone's +Z: rounded start (overlaps the previous segment at the joint),
        /// rounded end (ends exactly at the next joint for fingertips). Slightly flattened like a gloved finger.</summary>
        private static void AddCapsule(MeshData d, Transform seg, int bone, float length, float r0, float r1, bool tip)
        {
            float end = tip ? Mathf.Max(0.001f, length - r1) : length;
            var rings = new List<(float z, float r)>
            {
                (-r0 * Mathf.Sin(60f * Mathf.Deg2Rad), r0 * Mathf.Cos(60f * Mathf.Deg2Rad)),
                (-r0 * Mathf.Sin(30f * Mathf.Deg2Rad), r0 * Mathf.Cos(30f * Mathf.Deg2Rad)),
                (0f, r0), (end * 0.5f, (r0 + r1) * 0.5f), (end, r1),
                (end + r1 * Mathf.Sin(30f * Mathf.Deg2Rad), r1 * Mathf.Cos(30f * Mathf.Deg2Rad)),
                (end + r1 * Mathf.Sin(60f * Mathf.Deg2Rad), r1 * Mathf.Cos(60f * Mathf.Deg2Rad)),
            };
            var toRoot = seg.localToWorldMatrix;
            Vector3 L(float x, float y, float z) => toRoot.MultiplyPoint3x4(new Vector3(x, y * 0.88f, z));
            int start = d.Vertices.Count;
            foreach (var (z, r) in rings)
                for (int s = 0; s < Sides; s++)
                {
                    float a = s * Mathf.PI * 2f / Sides;
                    AddVertex(d, L(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z), bone);
                }
            int poleA = AddVertex(d, L(0, 0, -r0), bone);
            int poleB = AddVertex(d, L(0, 0, end + r1), bone);
            var centroid = L(0, 0, end * 0.5f);
            for (int ring = 0; ring < rings.Count - 1; ring++)
                for (int s = 0; s < Sides; s++)
                {
                    int a = start + ring * Sides + s, b = start + ring * Sides + (s + 1) % Sides;
                    int c = a + Sides, e = b + Sides;
                    AddTriangle(d, 0, a, b, e, centroid);
                    AddTriangle(d, 0, a, e, c, centroid);
                }
            int last = start + (rings.Count - 1) * Sides;
            for (int s = 0; s < Sides; s++)
            {
                AddTriangle(d, 0, poleA, start + s, start + (s + 1) % Sides, centroid);
                AddTriangle(d, 0, poleB, last + s, last + (s + 1) % Sides, centroid);
            }
        }

        /// <summary>Rounded box: a 4x4-per-face subdivided cube pushed out to rounded edges, welded for smooth normals.</summary>
        private static void AddRoundedBox(MeshData d, int bone, Vector3 centre, Vector3 size, float radius, int sub)
        {
            const int n = 4;
            var half = size * 0.5f;
            var inner = half - Vector3.one * radius;
            var index = new Dictionary<Vector3Int, int>();
            int Weld(Vector3 v)
            {
                var key = Vector3Int.RoundToInt(v * 100000f);
                if (index.TryGetValue(key, out int i)) return i;
                return index[key] = AddVertex(d, v, bone);
            }
            Vector3 Shape(Vector3 unit)
            {
                var pnt = Vector3.Scale(unit, half);
                var clamped = new Vector3(Mathf.Clamp(pnt.x, -inner.x, inner.x), Mathf.Clamp(pnt.y, -inner.y, inner.y), Mathf.Clamp(pnt.z, -inner.z, inner.z));
                var off = pnt - clamped;
                return centre + clamped + (off.sqrMagnitude > 1e-12f ? off.normalized * radius : Vector3.zero);
            }
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var nrm in normals)
            {
                var u = Mathf.Abs(nrm.y) > 0.5f ? Vector3.right : Vector3.up;
                var v = Vector3.Cross(nrm, u);
                var grid = new int[n + 1, n + 1];
                for (int i = 0; i <= n; i++)
                    for (int j = 0; j <= n; j++)
                        grid[i, j] = Weld(Shape(nrm + u * (i * 2f / n - 1f) + v * (j * 2f / n - 1f)));
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        AddTriangle(d, sub, grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], centre);
                        AddTriangle(d, sub, grid[i, j], grid[i + 1, j + 1], grid[i, j + 1], centre);
                    }
            }
        }

        /// <summary>Wrist cuff: a short elliptical band along Z with softened rims and closed ends (accent material).</summary>
        private static void AddCuff(MeshData d, int bone, Vector3 centre, float rx, float ry, float length)
        {
            const int sides = 16;
            float z0 = centre.z - length * 0.5f, z1 = centre.z + length * 0.5f;
            var rings = new (float z, float k)[] { (z0, 0.86f), (z0 + 0.006f, 1f), (z1 - 0.006f, 1f), (z1, 0.86f) };
            int start = d.Vertices.Count;
            foreach (var (z, k) in rings)
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    AddVertex(d, new Vector3(centre.x + Mathf.Cos(a) * rx * k, centre.y + Mathf.Sin(a) * ry * k, z), bone);
                }
            int capA = AddVertex(d, new Vector3(centre.x, centre.y, z0), bone);
            int capB = AddVertex(d, new Vector3(centre.x, centre.y, z1), bone);
            for (int ring = 0; ring < rings.Length - 1; ring++)
                for (int s = 0; s < sides; s++)
                {
                    int a = start + ring * sides + s, b = start + ring * sides + (s + 1) % sides;
                    AddTriangle(d, 1, a, b, b + sides, centre);
                    AddTriangle(d, 1, a, b + sides, a + sides, centre);
                }
            int last = start + (rings.Length - 1) * sides;
            for (int s = 0; s < sides; s++)
            {
                AddTriangle(d, 1, capA, start + s, start + (s + 1) % sides, centre);
                AddTriangle(d, 1, capB, last + s, last + (s + 1) % sides, centre);
            }
        }
    }
}
