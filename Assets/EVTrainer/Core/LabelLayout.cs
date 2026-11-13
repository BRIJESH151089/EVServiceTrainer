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
            if (anchors == null) throw new ArgumentNullException(nameof(anchors));
            var result = new Vector3[anchors.Count];
            var order = new List<int>(anchors.Count);
            for (int i = 0; i < anchors.Count; i++) order.Add(i);
            order.Sort((a, b) => Vector3.DistanceSquared(anchors[a], viewer).CompareTo(Vector3.DistanceSquared(anchors[b], viewer)));

            var placed = new List<Vector3>(anchors.Count);
            foreach (int i in order)
            {
                var p = anchors[i];
                for (int step = 0; step < maxSteps && Overlaps(p, placed, viewer, minBearingDegrees, minElevationDegrees); step++)
                    p += new Vector3(0f, stepMetres, 0f);
                result[i] = p;
                placed.Add(p);
            }
            return result;
        }

        private static bool Overlaps(Vector3 p, List<Vector3> placed, Vector3 viewer, float bearing, float elevation)
        {
            Direction(p - viewer, out float b, out float e);
            foreach (var q in placed)
            {
                Direction(q - viewer, out float qb, out float qe);
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
}
