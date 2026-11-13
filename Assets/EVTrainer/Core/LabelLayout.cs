using System;
using System.Collections.Generic;
using System.Numerics;

namespace EVTrainer.Core
{
    /// <summary>
    /// Learn-mode name labels over staged parts: one label per part group ("Battery module ×4"), placed so no two
    /// overlap as seen from the trainee: labels whose directions from the viewer are too close are lifted, nearest first.
    /// </summary>
    public static class LabelLayout
    {
        /// <summary>"Battery module" for one, "Battery module ×4" for several; empty when none are left.</summary>
        public static string GroupText(string name, int count)
        {
            if (count <= 0 || string.IsNullOrEmpty(name)) return "";
            return count == 1 ? name : $"{name} ×{count}";
        }

        /// <summary>Capitalises the first letter (e.g. "cover bolts" → "Cover bolts").</summary>
        public static string Capitalise(string text) =>
            string.IsNullOrEmpty(text) ? text ?? "" : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>
        /// Final label positions for <paramref name="anchors"/> (each just above its group). A label that would appear
        /// within <paramref name="minBearingDegrees"/> sideways and <paramref name="minElevationDegrees"/> up/down of a
        /// nearer label (from <paramref name="viewer"/>) is raised by <paramref name="stepMetres"/> until clear.
        /// </summary>
        public static Vector3[] Place(IReadOnlyList<Vector3> anchors, Vector3 viewer, float minBearingDegrees = 9f,
            float minElevationDegrees = 4f, float stepMetres = 0.04f, int maxSteps = 8)
        {
            var placed = new LabelPlacer().Place(anchors, viewer, minBearingDegrees, minElevationDegrees, stepMetres, maxSteps);
            var result = new Vector3[placed.Count];
            for (int i = 0; i < result.Length; i++) result[i] = placed[i];
            return result;
        }

        internal static bool Overlaps(Vector3 p, List<Vector3> placed, Vector3 viewer, float bearing, float elevation)
        {
            Direction(p - viewer, out float b, out float e);
            for (int i = 0; i < placed.Count; i++)
            {
                Direction(placed[i] - viewer, out float qb, out float qe);
                if (Math.Abs(AxisAngle.WrapDelta(b - qb)) < bearing && Math.Abs(e - qe) < elevation) return true;
            }
            return false;
        }

        /// <summary>Bearing (degrees about +Y) and elevation (degrees above horizontal) of a direction.</summary>
        public static void Direction(Vector3 d, out float bearingDegrees, out float elevationDegrees)
        {
            const double toDeg = 180.0 / Math.PI;
            bearingDegrees = (float)(Math.Atan2(d.X, d.Z) * toDeg);
            elevationDegrees = (float)(Math.Atan2(d.Y, Math.Sqrt(d.X * d.X + d.Z * d.Z)) * toDeg);
        }
    }

    /// <summary>
    /// <see cref="LabelLayout.Place"/> for labels refreshed every few frames: same result, but it reuses its buffers
    /// (no garbage per refresh). The returned list is owned by the placer and valid until the next call.
    /// </summary>
    public sealed class LabelPlacer
    {
        private readonly List<int> _order = new List<int>();
        private readonly List<float> _distance = new List<float>();
        private readonly List<Vector3> _placed = new List<Vector3>();
        private readonly List<Vector3> _result = new List<Vector3>();

        public IReadOnlyList<Vector3> Place(IReadOnlyList<Vector3> anchors, Vector3 viewer, float minBearingDegrees = 9f,
            float minElevationDegrees = 4f, float stepMetres = 0.04f, int maxSteps = 8)
        {
            if (anchors == null) throw new ArgumentNullException(nameof(anchors));
            _order.Clear();
            _distance.Clear();
            _placed.Clear();
            _result.Clear();
            for (int i = 0; i < anchors.Count; i++)
            {
                _distance.Add(Vector3.DistanceSquared(anchors[i], viewer));
                _result.Add(anchors[i]);
                // Stable insertion by distance (a handful of labels): nearest first.
                int at = _order.Count;
                while (at > 0 && _distance[_order[at - 1]] > _distance[i]) at--;
                _order.Insert(at, i);
            }
            for (int k = 0; k < _order.Count; k++)
            {
                int i = _order[k];
                var p = anchors[i];
                for (int step = 0; step < maxSteps && LabelLayout.Overlaps(p, _placed, viewer, minBearingDegrees, minElevationDegrees); step++)
                    p += new Vector3(0f, stepMetres, 0f);
                _result[i] = p;
                _placed.Add(p);
            }
            return _result;
        }
    }

    /// <summary>Remembers "Battery module ×4"-style texts so a label refresh does not build a new string each time.</summary>
    public sealed class GroupTextCache
    {
        private readonly Dictionary<string, Dictionary<int, string>> _byName = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);

        public string Get(string name, int count)
        {
            if (count <= 0 || string.IsNullOrEmpty(name)) return "";
            if (!_byName.TryGetValue(name, out var byCount)) _byName.Add(name, byCount = new Dictionary<int, string>());
            if (!byCount.TryGetValue(count, out var text)) byCount.Add(count, text = LabelLayout.GroupText(name, count));
            return text;
        }
    }
}
