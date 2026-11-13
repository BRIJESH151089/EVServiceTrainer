using System;

namespace EVTrainer.Core
{
    /// <summary>Strength of the soft contact shadow under a held part: full when touching, fading out with height.</summary>
    public static class ContactShadow
    {
        /// <param name="heightMetres">Gap between the part's underside and the surface below it.</param>
        /// <param name="fadeHeightMetres">At this gap (or more) the shadow is gone.</param>
        /// <returns>0..1 (smooth: quadratic fade).</returns>
        public static float Strength(float heightMetres, float fadeHeightMetres = 0.45f)
        {
            if (fadeHeightMetres <= 0f) throw new ArgumentOutOfRangeException(nameof(fadeHeightMetres));
            if (float.IsNaN(heightMetres)) return 0f;
            float t = Math.Max(0f, Math.Min(1f, heightMetres / fadeHeightMetres));
            return (1f - t) * (1f - t);
        }
    }
}
