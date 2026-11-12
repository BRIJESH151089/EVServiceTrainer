using System;
using System.Numerics;

namespace EVTrainer.Core
{
    /// <summary>
    /// Rotation about an axis, measured in the plane perpendicular to it with atan2. Positive = clockwise when
    /// looking down the axis from its tip (in Unity's left-handed world: Y-up axis, Z→X is +90°), i.e. tightening.
    /// </summary>
    public static class AxisAngle
    {
        private const float RadToDeg = (float)(180.0 / Math.PI);

        /// <summary>Angle (degrees, -180..180) of <paramref name="direction"/> about <paramref name="axis"/>.
        /// False when the direction is (almost) parallel to the axis and has no usable angle.</summary>
        public static bool TryAngleAbout(Vector3 axis, Vector3 direction, out float angleDegrees)
        {
            angleDegrees = 0f;
            if (axis.LengthSquared() < 1e-12f) return false;
            axis = Vector3.Normalize(axis);

            // Stable basis in the plane: u from world X (or Z if the axis is close to X), v = axis x u.
            var reference = Math.Abs(Vector3.Dot(axis, Vector3.UnitX)) < 0.9f ? Vector3.UnitX : Vector3.UnitZ;
            var u = Vector3.Normalize(reference - axis * Vector3.Dot(reference, axis));
            var v = Vector3.Cross(axis, u);

            var p = direction - axis * Vector3.Dot(direction, axis);
            if (p.LengthSquared() < 1e-10f) return false;
            angleDegrees = (float)Math.Atan2(Vector3.Dot(p, v), Vector3.Dot(p, u)) * RadToDeg;
            return true;
        }

        /// <summary>Wraps an angle difference into (-180, 180].</summary>
        public static float WrapDelta(float deltaDegrees)
        {
            float d = deltaDegrees % 360f;
            if (d > 180f) d -= 360f;
            else if (d <= -180f) d += 360f;
            return d;
        }

        /// <summary>Angle between two directions (0..180 degrees).</summary>
        public static float AngleBetween(Vector3 a, Vector3 b)
        {
            if (a.LengthSquared() < 1e-12f || b.LengthSquared() < 1e-12f) return 0f;
            float cos = Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b));
            return (float)Math.Acos(Math.Max(-1f, Math.Min(1f, cos))) * RadToDeg;
        }
    }

    /// <summary>Per-frame rotation delta about a fixed axis (e.g. a hand twisting a bolt, a wrench handle swinging).</summary>
    public sealed class AxisRotationTracker
    {
        private bool _hasLast;
        private float _last;

        /// <summary>Feeds the current direction; returns the clockwise-positive delta since the last call (0 on the first).</summary>
        public float Update(Vector3 axis, Vector3 direction)
        {
            if (!AxisAngle.TryAngleAbout(axis, direction, out float angle))
            {
                _hasLast = false;
                return 0f;
            }
            float delta = _hasLast ? AxisAngle.WrapDelta(angle - _last) : 0f;
            _last = angle;
            _hasLast = true;
            return delta;
        }

        public void Reset() => _hasLast = false;
    }
}
