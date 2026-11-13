using System;

namespace EVTrainer.Core
{
    /// <summary>Maps torque onto a progress ring round the bolt: 0 at the start, the spec window marked as a zone.</summary>
    public static class TorqueDial
    {
        /// <summary>Full ring = this many times the upper torque limit (so the green zone sits well before the end).</summary>
        public const float DefaultFullScaleFactor = 1.25f;

        public static float FullScaleNm(BoltSpec spec, float fullScaleFactor = DefaultFullScaleFactor)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (fullScaleFactor < 1f) throw new ArgumentOutOfRangeException(nameof(fullScaleFactor));
            return spec.MaxTorqueNm * fullScaleFactor;
        }

        /// <summary>Filled part of the ring (0..1) for <paramref name="torqueNm"/>.</summary>
        public static float Fill01(BoltSpec spec, float torqueNm, float fullScaleFactor = DefaultFullScaleFactor)
        {
            if (float.IsNaN(torqueNm)) return 0f;
            return Math.Max(0f, Math.Min(1f, torqueNm / FullScaleNm(spec, fullScaleFactor)));
        }

        /// <summary>Start and end (0..1) of the green target zone (spec ± tolerance).</summary>
        public static (float start, float end) Zone01(BoltSpec spec, float fullScaleFactor = DefaultFullScaleFactor)
        {
            float full = FullScaleNm(spec, fullScaleFactor);
            return (spec.MinTorqueNm / full, spec.MaxTorqueNm / full);
        }
    }
}
