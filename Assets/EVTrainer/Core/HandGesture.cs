using System;
using System.Numerics;

namespace EVTrainer.Core
{
    /// <summary>Distance thresholds (metres) for turning joint positions into 0..1 gesture values.</summary>
    public readonly struct HandGestureSettings
    {
        public HandGestureSettings(float pinchClosedMetres, float pinchOpenMetres, float curlClosedMetres, float curlOpenMetres)
        {
            if (pinchClosedMetres >= pinchOpenMetres) throw new ArgumentException("Pinch closed distance must be smaller than open distance.");
            if (curlClosedMetres >= curlOpenMetres) throw new ArgumentException("Curl closed distance must be smaller than open distance.");
            PinchClosedMetres = pinchClosedMetres;
            PinchOpenMetres = pinchOpenMetres;
            CurlClosedMetres = curlClosedMetres;
            CurlOpenMetres = curlOpenMetres;
        }

        /// <summary>Thumb-tip to index-tip distance at or below which pinch = 1.</summary>
        public float PinchClosedMetres { get; }

        /// <summary>Thumb-tip to index-tip distance at or above which pinch = 0.</summary>
        public float PinchOpenMetres { get; }

        /// <summary>Fingertip to palm distance at or below which a finger counts as fully curled.</summary>
        public float CurlClosedMetres { get; }

        /// <summary>Fingertip to palm distance at or above which a finger counts as straight.</summary>
        public float CurlOpenMetres { get; }

        public static HandGestureSettings Default => new HandGestureSettings(0.015f, 0.06f, 0.045f, 0.095f);
    }

    /// <summary>Plain maths that turns hand joint positions into pinch and grip values.</summary>
    public static class HandGesture
    {
        /// <summary>
        /// Maps a distance to 0..1: 1 at or below <paramref name="fullAt"/>, 0 at or above
        /// <paramref name="zeroAt"/>, linear in between.
        /// </summary>
        public static float DistanceTo01(float distance, float fullAt, float zeroAt)
        {
            if (float.IsNaN(distance)) return 0f;
            if (distance <= fullAt) return 1f;
            if (distance >= zeroAt) return 0f;
            return (zeroAt - distance) / (zeroAt - fullAt);
        }

        /// <summary>Pinch strength from the thumb-tip and index-tip positions.</summary>
        public static float Pinch(Vector3 thumbTip, Vector3 indexTip, in HandGestureSettings settings) =>
            DistanceTo01(Vector3.Distance(thumbTip, indexTip), settings.PinchClosedMetres, settings.PinchOpenMetres);

        /// <summary>
        /// Simple grip estimate: average curl of middle, ring and little fingers, where curl is how
        /// close each fingertip is to the palm centre.
        /// </summary>
        public static float Grip(Vector3 palm, Vector3 middleTip, Vector3 ringTip, Vector3 littleTip, in HandGestureSettings settings)
        {
            float middle = DistanceTo01(Vector3.Distance(palm, middleTip), settings.CurlClosedMetres, settings.CurlOpenMetres);
            float ring = DistanceTo01(Vector3.Distance(palm, ringTip), settings.CurlClosedMetres, settings.CurlOpenMetres);
            float little = DistanceTo01(Vector3.Distance(palm, littleTip), settings.CurlClosedMetres, settings.CurlOpenMetres);
            return (middle + ring + little) / 3f;
        }
    }
}
