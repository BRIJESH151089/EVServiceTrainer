using System;

namespace EVTrainer.Core
{
    /// <summary>Which physical input produced an <see cref="InputFrame"/>.</summary>
    public enum InputSource
    {
        None,
        Controller,
        Hand,
    }

    /// <summary>
    /// One frame of source-agnostic hand input. Produced by the XR adapter from controllers or
    /// (later) hand tracking; Core and UI never need to know which source is active.
    /// </summary>
    public readonly struct InputFrame
    {
        public InputFrame(InputSource source, float grip, float trigger, float pinch, PartPose handPose, bool isTracked)
        {
            Source = source;
            Grip = Clamp01(grip);
            Trigger = Clamp01(trigger);
            Pinch = Clamp01(pinch);
            HandPose = handPose;
            IsTracked = isTracked;
        }

        public InputSource Source { get; }

        /// <summary>Grip (squeeze) amount, 0..1.</summary>
        public float Grip { get; }

        /// <summary>Index trigger amount, 0..1.</summary>
        public float Trigger { get; }

        /// <summary>Thumb-index pinch strength, 0..1. Always 0 for controllers.</summary>
        public float Pinch { get; }

        /// <summary>World pose of the hand / controller grip.</summary>
        public PartPose HandPose { get; }

        public bool IsTracked { get; }

        public static InputFrame None => new InputFrame(InputSource.None, 0f, 0f, 0f, PartPose.Identity, false);

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0f;
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
