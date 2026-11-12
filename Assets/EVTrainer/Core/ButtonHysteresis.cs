using System;

namespace EVTrainer.Core
{
    /// <summary>
    /// Turns an analog 0..1 value into a stable pressed/released state: presses at or above
    /// <see cref="PressThreshold"/>, releases only at or below the lower <see cref="ReleaseThreshold"/>,
    /// so small jitter around one threshold does not toggle the state.
    /// </summary>
    public sealed class ButtonHysteresis
    {
        public ButtonHysteresis(float pressThreshold = 0.75f, float releaseThreshold = 0.5f)
        {
            if (releaseThreshold >= pressThreshold) throw new ArgumentException("Release threshold must be below press threshold.");
            PressThreshold = pressThreshold;
            ReleaseThreshold = releaseThreshold;
        }

        public float PressThreshold { get; }
        public float ReleaseThreshold { get; }

        public bool IsPressed { get; private set; }

        /// <summary>True only on the update where the state went from released to pressed.</summary>
        public bool WasPressedThisUpdate { get; private set; }

        /// <summary>True only on the update where the state went from pressed to released.</summary>
        public bool WasReleasedThisUpdate { get; private set; }

        public bool Update(float value)
        {
            bool wasPressed = IsPressed;
            if (!IsPressed && value >= PressThreshold) IsPressed = true;
            else if (IsPressed && value <= ReleaseThreshold) IsPressed = false;

            WasPressedThisUpdate = !wasPressed && IsPressed;
            WasReleasedThisUpdate = wasPressed && !IsPressed;
            return IsPressed;
        }

        /// <summary>Forces the released state, e.g. when tracking is lost.</summary>
        public void Reset()
        {
            WasReleasedThisUpdate = IsPressed;
            WasPressedThisUpdate = false;
            IsPressed = false;
        }
    }
}
