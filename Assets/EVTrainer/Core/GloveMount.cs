using System;
using System.Numerics;

namespace EVTrainer.Core
{
    /// <summary>
    /// The controller's OpenXR grip pose, as Unity reports it (Unity left-handed axes).
    ///
    /// OpenXR spec, "Standard pose identifiers", grip (KhronosGroup/OpenXR-Docs, semantic_paths.adoc):
    /// position: a fixed point within the controller that lines up with the palm centroid;
    /// +X: normal to the palm when the hand is flat (away from the palm in the LEFT hand, into the palm in the RIGHT),
    ///     i.e. the way the left palm faces: towards the user's RIGHT for both hands (confirmed on a Quest);
    /// -Z: through the tube formed by the non-thumb fingers when holding the controller, from little finger to thumb;
    /// +Y: completes the right-handed frame.
    ///
    /// Unity conversion (com.unity.xr.openxr 1.19.0, XrVector3f / XrQuaternionf): position (x, y, -z), rotation
    /// (-x, -y, z, w), a mirror in Z. So in UNITY grip space:
    /// +X = the way the left palm faces / into the palm of the right hand (both: towards the user's RIGHT when held);
    /// +Z = along the handle from the little finger to the thumb (towards the thumbstick);
    /// +Y = from the knuckles towards the wrist (back towards the user when held).
    /// Held straight ahead: +Z points up (tilted forward with the handle), +Y back, +X to the user's right, for both
    /// hands. The identity rotation is NOT "held straight ahead": at identity the hand would point its fingers down.
    /// </summary>
    public static class GripPose
    {
        /// <summary>
        /// The grip pose of a controller held straight ahead at <paramref name="position"/> (fist forward, thumb on top),
        /// with the handle tilted forward from vertical by <paramref name="handleTiltDegrees"/>. Same rotation for both hands.
        /// </summary>
        public static PartPose HeldStraightAhead(Vector3 position, float handleTiltDegrees = 30f)
        {
            float h = handleTiltDegrees * (float)Math.PI / 180f;
            var x = new Vector3(1f, 0f, 0f);                                      // user's right
            var z = new Vector3(0f, (float)Math.Cos(h), (float)Math.Sin(h));     // up the handle, tilted forward
            var y = Vector3.Cross(z, x);                                          // back (slightly up)
            return new PartPose(position, Basis(x, y, z));
        }

        /// <summary>Rotation whose local X, Y, Z map onto the given orthonormal axes.</summary>
        public static Quaternion Basis(Vector3 x, Vector3 y, Vector3 z)
        {
            var m = new Matrix4x4(x.X, x.Y, x.Z, 0f, y.X, y.Y, y.Z, 0f, z.X, z.Y, z.Z, 0f, 0f, 0f, 0f, 1f);
            return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
        }
    }

    /// <summary>
    /// Where the glove's wrist sits on a controller: a pose in the controller's grip space (per side, data).
    /// <see cref="Fit"/> builds it from the hand geometry: the glove's forward runs along grip -Y (wrist → knuckles),
    /// turned towards grip +Z (the thumb end) by the hand tilt, because the handle crosses the palm obliquely; the back
    /// of the hand faces grip -X on the left and +X on the right; the glove point that holds the handle sits on the
    /// grip origin.
    /// </summary>
    public readonly struct GloveMount
    {
        public const float DefaultHandTiltDegrees = 35f;

        /// <summary>Glove-space point (left hand) that sits on the grip origin: palm centroid, a little inside the handle.</summary>
        public static readonly Vector3 DefaultGripPointInGlove = new Vector3(0f, -0.025f, 0.06f);

        public GloveMount(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        /// <summary>Wrist position in grip space.</summary>
        public Vector3 Position { get; }

        /// <summary>Wrist rotation in grip space (glove axes, see <see cref="GloveHandLayout"/>).</summary>
        public Quaternion Rotation { get; }

        public static GloveMount Fit(HandSide side, Vector3 gripPointInLeftGlove, float handTiltDegrees = DefaultHandTiltDegrees)
        {
            float t = handTiltDegrees * (float)Math.PI / 180f;
            var forward = new Vector3(0f, -(float)Math.Cos(t), (float)Math.Sin(t));          // glove +Z in grip space
            var back = side == HandSide.Left ? -Vector3.UnitX : Vector3.UnitX;                // glove +Y in grip space
            var right = Vector3.Cross(back, forward);                                         // glove +X (Unity: up × forward)
            var rotation = GripPose.Basis(right, back, forward);
            var gripPoint = side == HandSide.Left ? gripPointInLeftGlove
                : new Vector3(-gripPointInLeftGlove.X, gripPointInLeftGlove.Y, gripPointInLeftGlove.Z);
            return new GloveMount(-Vector3.Transform(gripPoint, rotation), rotation);
        }

        /// <summary>The glove wrist pose for a grip pose.</summary>
        public PartPose WristPose(PartPose grip) =>
            new PartPose(grip.Position + Vector3.Transform(Position, grip.Rotation), Quaternion.Concatenate(Rotation, grip.Rotation));

        /// <summary>A glove-space point (e.g. a joint) in the grip pose's space (world if the grip pose is in world).</summary>
        public Vector3 GlovePointToWorld(PartPose grip, Vector3 glovePoint)
        {
            var wrist = WristPose(grip);
            return wrist.Position + Vector3.Transform(glovePoint, wrist.Rotation);
        }

        /// <summary>A glove-space direction in the grip pose's space.</summary>
        public Vector3 GloveDirectionToWorld(PartPose grip, Vector3 gloveDirection) =>
            Vector3.Transform(gloveDirection, WristPose(grip).Rotation);
    }
}
