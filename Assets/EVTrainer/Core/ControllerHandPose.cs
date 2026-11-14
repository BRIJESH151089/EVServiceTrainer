using System;

namespace EVTrainer.Core
{
    public enum Finger { Thumb, Index, Middle, Ring, Little }

    /// <summary>How far each finger is curled: 0 = straight (open hand), 1 = fully curled (fist).</summary>
    public readonly struct FingerCurls
    {
        public FingerCurls(float thumb, float index, float middle, float ring, float little)
        {
            Thumb = Clamp01(thumb);
            Index = Clamp01(index);
            Middle = Clamp01(middle);
            Ring = Clamp01(ring);
            Little = Clamp01(little);
        }

        public float Thumb { get; }
        public float Index { get; }
        public float Middle { get; }
        public float Ring { get; }
        public float Little { get; }

        public float this[Finger f]
        {
            get
            {
                switch (f)
                {
                    case Finger.Thumb: return Thumb;
                    case Finger.Index: return Index;
                    case Finger.Middle: return Middle;
                    case Finger.Ring: return Ring;
                    default: return Little;
                }
            }
        }

        internal static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(1f, v));
    }

    /// <summary>
    /// Hand pose shown on a controller: grip closes the middle, ring and little fingers (and the thumb), the trigger
    /// curls the index finger. A relaxed hand stays loosely curled round the handle; curls ease towards their targets
    /// so the hand never snaps between poses.
    /// </summary>
    public static class ControllerHandPose
    {
        /// <summary>Curl with nothing pressed: the hand still loosely holds the controller handle.</summary>
        public const float RestCurl = 0.35f;

        /// <summary>Pointing to poke a button: index straight (at the poke point), the others closed, thumb resting.</summary>
        public static FingerCurls Pointing => new FingerCurls(thumb: 0.6f, index: 0f, middle: 1f, ring: 1f, little: 1f);

        /// <summary>Target curls for the current grip / trigger values (0..1).</summary>
        public static FingerCurls FromInput(float grip, float trigger)
        {
            grip = FingerCurls.Clamp01(grip);
            trigger = FingerCurls.Clamp01(trigger);
            float Lerp(float a, float b, float t) => a + (b - a) * t;
            float fist = Lerp(RestCurl, 1f, grip);
            float index = Lerp(RestCurl, 1f, Math.Max(trigger, grip * 0.35f));
            float thumb = Lerp(RestCurl, 0.75f, Math.Max(grip, trigger * 0.5f));
            return new FingerCurls(thumb, index, fist, fist, fist);
        }

        /// <summary>
        /// Eases <paramref name="current"/> towards <paramref name="target"/> (exponential, time constant
        /// <paramref name="timeConstantSeconds"/>), per finger.
        /// </summary>
        public static FingerCurls Ease(FingerCurls current, FingerCurls target, float deltaSeconds, float timeConstantSeconds = 0.06f)
        {
            if (timeConstantSeconds <= 0f) return target;
            float k = 1f - (float)Math.Exp(-Math.Max(0f, deltaSeconds) / timeConstantSeconds);
            float E(float a, float b) => a + (b - a) * k;
            return new FingerCurls(E(current.Thumb, target.Thumb), E(current.Index, target.Index), E(current.Middle, target.Middle),
                E(current.Ring, target.Ring), E(current.Little, target.Little));
        }

        /// <summary>Bend of one finger joint for a curl: proximal 0..70°, intermediate 0..95°, distal 0..60° (thumb less).</summary>
        public static float JointAngleDegrees(Finger finger, int jointIndex, float curl)
        {
            if (jointIndex < 0 || jointIndex > 2) throw new ArgumentOutOfRangeException(nameof(jointIndex));
            var max = finger == Finger.Thumb ? ThumbMaxDegrees : FingerMaxDegrees;
            return max[jointIndex] * FingerCurls.Clamp01(curl);
        }

        // Called for every joint every frame: no per-call arrays.
        private static readonly float[] ThumbMaxDegrees = { 25f, 45f, 50f };
        private static readonly float[] FingerMaxDegrees = { 70f, 95f, 60f };
    }
}
