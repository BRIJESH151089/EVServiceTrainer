using System;

namespace EVTrainer.Core
{
    /// <summary>
    /// Glove hand fit: a finger segment modelled at <c>bindLength</c> is stretched along its bone to the tracked
    /// length (the distance to the next tracked joint), so the glove matches the trainee's real finger lengths.
    /// Limited to a sane range so a tracking glitch never produces a wildly long or squashed finger.
    /// </summary>
    public static class SegmentStretch
    {
        public const float DefaultMin = 0.75f;
        public const float DefaultMax = 1.35f;

        public static float Scale(float trackedLength, float bindLength, float min = DefaultMin, float max = DefaultMax)
        {
            if (min <= 0f || max < min) throw new ArgumentOutOfRangeException(nameof(min));
            if (bindLength <= 1e-6f || float.IsNaN(trackedLength) || float.IsNaN(bindLength) || trackedLength <= 0f) return 1f;
            return Math.Max(min, Math.Min(max, trackedLength / bindLength));
        }
    }
}
