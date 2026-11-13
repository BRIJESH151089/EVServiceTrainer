using System;

namespace EVTrainer.Core
{
    /// <summary>
    /// Comfort fade: black overlay opacity over time. Fading out goes 0 → 1 (to black), fading in 1 → 0. Linear in
    /// time (an even dimming reads as calm in a headset); a zero duration is an instant cut.
    /// </summary>
    public static class ScreenFadeCurve
    {
        public const float DefaultSeconds = 0.3f;

        public static float Alpha(float elapsedSeconds, float durationSeconds, bool toBlack)
        {
            float k = durationSeconds <= 0f ? 1f
                : float.IsNaN(elapsedSeconds) ? 0f
                : Math.Max(0f, Math.Min(1f, elapsedSeconds / durationSeconds));
            return toBlack ? k : 1f - k;
        }

        public static bool IsFinished(float elapsedSeconds, float durationSeconds) =>
            durationSeconds <= 0f || elapsedSeconds >= durationSeconds;
    }
}
