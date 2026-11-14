using System.Numerics;

namespace EVTrainer.Core
{
    /// <summary>
    /// Where a controller pokes buttons: a FIXED point in the controller's grip space, at the pad of the index fingertip
    /// of the glove in its pointing pose (index straight, the bind pose), placed with the glove's own
    /// <see cref="GloveMount"/>. It does not follow the curled finger, so grip and trigger never move it; the glove
    /// shows the pointing pose near buttons so the visual matches (<see cref="ControllerHandPose.Pointing"/>).
    /// </summary>
    public static class ControllerPokePoint
    {
        /// <summary>From the tip joint along the finger to the front of the fingertip pad.</summary>
        public const float DefaultPadMetres = 0.008f;

        public static Vector3 PositionInGrip(HandSide side, GloveMount mount, float padMetres = DefaultPadMetres)
        {
            var tip = GloveHandLayout.Joint("IndexTip", side);
            return mount.GlovePointToWorld(Identity, tip + PointingDirectionInGlove(side) * padMetres);
        }

        /// <summary>Poke rotation in grip space: +Z along the pointing index finger, +Y out of the back of the hand.</summary>
        public static Quaternion RotationInGrip(HandSide side, GloveMount mount)
        {
            var forward = mount.GloveDirectionToWorld(Identity, PointingDirectionInGlove(side));
            var back = mount.GloveDirectionToWorld(Identity, GloveHandLayout.BackOfHand);
            back = Vector3.Normalize(back - forward * Vector3.Dot(back, forward));
            return GripPose.Basis(Vector3.Cross(back, forward), back, forward);
        }

        /// <summary>Direction of the straight index finger (distal joint → tip) in glove space.</summary>
        public static Vector3 PointingDirectionInGlove(HandSide side) =>
            Vector3.Normalize(GloveHandLayout.Joint("IndexTip", side) - GloveHandLayout.Joint("IndexDistal", side));

        private static PartPose Identity => new PartPose(Vector3.Zero, Quaternion.Identity);
    }
}
