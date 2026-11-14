using System;
using System.Collections.Generic;
using System.Numerics;

namespace EVTrainer.Core
{
    public enum HandSide { Left, Right }

    /// <summary>
    /// The glove hand's bind pose (average adult hand, metres, relative to the wrist joint), shared by the mesh builder,
    /// the controller mount and the tests.
    ///
    /// Glove local axes (both hands, the same convention as the XR Hands / OpenXR hand joints):
    /// <list type="bullet">
    /// <item>+Z = forward: from the wrist towards the middle finger (all fingers point along +Z when open).</item>
    /// <item>+Y = out of the BACK of the hand; the palm normal (the way the palm faces) is -Y.</item>
    /// <item>Thumb side: +X on the LEFT hand, -X on the RIGHT hand.</item>
    /// </list>
    /// The right hand is a true mirror of the left: every joint has its X negated (no negative scales anywhere).
    /// </summary>
    public static class GloveHandLayout
    {
        public static readonly Vector3 Forward = Vector3.UnitZ;
        public static readonly Vector3 BackOfHand = Vector3.UnitY;
        public static readonly Vector3 PalmNormal = -Vector3.UnitY;

        /// <summary>Thumb-side direction of a hand in its own glove space.</summary>
        public static Vector3 ThumbSide(HandSide side) => side == HandSide.Left ? Vector3.UnitX : -Vector3.UnitX;

        /// <summary>Left hand, metres from the wrist joint.</summary>
        private static readonly Dictionary<string, Vector3> LeftJoints = new Dictionary<string, Vector3>(StringComparer.Ordinal)
        {
            ["Wrist"] = Vector3.Zero,
            ["Palm"] = new Vector3(0.000f, 0.002f, 0.050f),
            ["ThumbMetacarpal"] = new Vector3(0.024f, -0.010f, 0.020f), ["ThumbProximal"] = new Vector3(0.046f, -0.016f, 0.052f),
            ["ThumbDistal"] = new Vector3(0.060f, -0.018f, 0.082f), ["ThumbTip"] = new Vector3(0.070f, -0.018f, 0.104f),
            ["IndexMetacarpal"] = new Vector3(0.020f, 0.002f, 0.012f), ["IndexProximal"] = new Vector3(0.024f, 0.004f, 0.090f),
            ["IndexIntermediate"] = new Vector3(0.026f, 0.004f, 0.130f), ["IndexDistal"] = new Vector3(0.027f, 0.003f, 0.154f),
            ["IndexTip"] = new Vector3(0.028f, 0.002f, 0.176f),
            ["MiddleMetacarpal"] = new Vector3(0.005f, 0.003f, 0.012f), ["MiddleProximal"] = new Vector3(0.004f, 0.005f, 0.093f),
            ["MiddleIntermediate"] = new Vector3(0.004f, 0.005f, 0.138f), ["MiddleDistal"] = new Vector3(0.004f, 0.004f, 0.166f),
            ["MiddleTip"] = new Vector3(0.004f, 0.003f, 0.190f),
            ["RingMetacarpal"] = new Vector3(-0.010f, 0.002f, 0.012f), ["RingProximal"] = new Vector3(-0.014f, 0.004f, 0.087f),
            ["RingIntermediate"] = new Vector3(-0.017f, 0.004f, 0.128f), ["RingDistal"] = new Vector3(-0.019f, 0.003f, 0.154f),
            ["RingTip"] = new Vector3(-0.020f, 0.002f, 0.176f),
            ["LittleMetacarpal"] = new Vector3(-0.022f, 0.000f, 0.012f), ["LittleProximal"] = new Vector3(-0.031f, 0.000f, 0.077f),
            ["LittleIntermediate"] = new Vector3(-0.037f, 0.000f, 0.108f), ["LittleDistal"] = new Vector3(-0.041f, -0.001f, 0.128f),
            ["LittleTip"] = new Vector3(-0.044f, -0.002f, 0.148f),
        };

        /// <summary>Joint names (XR Hands names without the L_/R_ prefix).</summary>
        public static IEnumerable<string> JointNames => LeftJoints.Keys;

        /// <summary>Bind position of a joint in its hand's glove space (right = left mirrored in X).</summary>
        public static Vector3 Joint(string name, HandSide side)
        {
            if (!LeftJoints.TryGetValue(name, out var p)) throw new ArgumentException($"Unknown glove joint '{name}'.", nameof(name));
            return side == HandSide.Left ? p : new Vector3(-p.X, p.Y, p.Z);
        }

        /// <summary>The bending joints of a finger, then its tip (the thumb has three bending joints like the others).</summary>
        public static IReadOnlyList<string> CurlChain(Finger finger) => finger switch
        {
            Finger.Thumb => new[] { "ThumbMetacarpal", "ThumbProximal", "ThumbDistal", "ThumbTip" },
            _ => new[] { finger + "Proximal", finger + "Intermediate", finger + "Distal", finger + "Tip" },
        };

        /// <summary>
        /// Axis a finger bone bends about so that a POSITIVE rotation (quaternion from axis and angle, as Unity's
        /// AngleAxis) swings the bone towards the palm: axis = boneDirection × palmNormal.
        /// </summary>
        public static Vector3 CurlAxis(Vector3 boneDirection)
        {
            var axis = Vector3.Cross(boneDirection, PalmNormal);
            float length = axis.Length();
            return length > 1e-6f ? axis / length : Vector3.UnitX;
        }

        /// <summary>
        /// Fingertip position in glove space for a curl (0 = open, 1 = fully curled), bending each joint by
        /// <see cref="ControllerHandPose.JointAngleDegrees"/> about its <see cref="CurlAxis"/> (the controller hand's rule).
        /// </summary>
        public static Vector3 CurledTip(Finger finger, float curl, HandSide side)
        {
            var chain = CurlChain(finger);
            var points = new Vector3[chain.Count];
            for (int i = 0; i < points.Length; i++) points[i] = Joint(chain[i], side);
            var turned = Quaternion.Identity;
            for (int j = 0; j < points.Length - 1; j++)
            {
                var bindDirection = Vector3.Normalize(Joint(chain[j + 1], side) - Joint(chain[j], side));
                var axis = Vector3.Transform(CurlAxis(bindDirection), turned);
                float radians = ControllerHandPose.JointAngleDegrees(finger, j, curl) * (float)Math.PI / 180f;
                var bend = Quaternion.CreateFromAxisAngle(axis, radians);
                for (int k = j + 1; k < points.Length; k++) points[k] = points[j] + Vector3.Transform(points[k] - points[j], bend);
                turned = Quaternion.Concatenate(turned, bend);
            }
            return points[points.Length - 1];
        }
    }
}
