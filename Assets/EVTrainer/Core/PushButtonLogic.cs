using System;

namespace EVTrainer.Core
{
    /// <summary>
    /// What drives a physical push button: how far its cap is pushed (poke only), plus feedback hooks.
    /// Implemented by the XR layer (fingertip / controller tip poke), consumed by the UI layer (cap visual, click).
    /// </summary>
    public interface IPushButtonInput
    {
        /// <summary>How far a fingertip or controller tip pushes the cap: 0 = at rest, 1 = full travel.</summary>
        float PressDepth01 { get; }

        /// <summary>Called when the button fires (e.g. for a haptic pulse on the controller that pressed it).</summary>
        void NotifyClicked();
    }

    /// <summary>
    /// Press depth → click, like a real key: fires once when the cap passes <see cref="ClickAt"/> of its travel,
    /// then must come back to <see cref="RearmAt"/> before it can fire again (hysteresis).
    /// A latching button (e.g. a mode choice) stays pressed in after it fires and ignores pushes until
    /// <see cref="Unlatch"/>; a momentary one springs back.
    /// </summary>
    public sealed class PushButtonLogic
    {
        public PushButtonLogic(float clickAt = 0.7f, float rearmAt = 0.15f, bool latching = false)
        {
            if (!(rearmAt >= 0f && rearmAt < clickAt && clickAt <= 1f))
                throw new ArgumentException("Need 0 <= rearmAt < clickAt <= 1.");
            ClickAt = clickAt;
            RearmAt = rearmAt;
            Latching = latching;
        }

        public float ClickAt { get; }
        public float RearmAt { get; }
        public bool Latching { get; }

        /// <summary>A latching button that has fired: held fully pressed in, inert until <see cref="Unlatch"/>.</summary>
        public bool IsLatched { get; private set; }

        /// <summary>Depth the cap should show: full travel while latched, else the pushed depth.</summary>
        public float DisplayDepth(float pushedDepth01) => IsLatched ? 1f : Clamp(pushedDepth01);

        /// <summary>Holds the button pressed in without a click (e.g. restoring a selection).</summary>
        public void Latch()
        {
            if (!Latching) return;
            IsLatched = true;
            IsArmed = false;
        }

        /// <summary>Releases a latched button; it re-arms once the cap is back out past <see cref="RearmAt"/>.</summary>
        public void Unlatch() => IsLatched = false;

        /// <summary>True when the next push past <see cref="ClickAt"/> will fire.</summary>
        public bool IsArmed { get; private set; } = true;

        /// <summary>Feeds the current depth (0..1). Returns true on the single update the button fires.</summary>
        public bool Update(float depth01)
        {
            if (IsLatched) return false;
            float depth = Clamp(depth01);
            if (IsArmed && depth >= ClickAt)
            {
                IsArmed = false;
                if (Latching) IsLatched = true;
                return true;
            }
            if (!IsArmed && depth <= RearmAt) IsArmed = true;
            return false;
        }

        /// <summary>Back to rest: armed and not latched.</summary>
        public void Reset()
        {
            IsArmed = true;
            IsLatched = false;
        }

        private static float Clamp(float depth01) => float.IsNaN(depth01) ? 0f : Math.Max(0f, Math.Min(1f, depth01));
    }

    /// <summary>Self-made click sound as raw samples (no audio asset): a short bright tick over a soft thump.</summary>
    public static class ProceduralClick
    {
        public static float[] CreateSamples(int sampleRate, float durationSeconds = 0.05f, float tickHz = 2400f, float thumpHz = 160f)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            int length = Math.Max(1, (int)(sampleRate * durationSeconds));
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                double t = (double)i / sampleRate;
                double tick = Math.Sin(2 * Math.PI * tickHz * t) * Math.Exp(-t * 180);
                double thump = Math.Sin(2 * Math.PI * thumpHz * t) * Math.Exp(-t * 60);
                data[i] = (float)(0.55 * tick + 0.45 * thump);
            }
            return data;
        }
    }
}
