using System;
using System.Numerics;

namespace EVTrainer.Core
{
    /// <summary>Pose-vs-target distance and angle maths, symmetry-aware.</summary>
    public static class SnapEvaluator
    {
        private const float RadToDeg = (float)(180.0 / Math.PI);
        private static readonly Quaternion HalfTurnAboutUp = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Math.PI);

        public static SnapEvaluation Evaluate(PartSpec spec, PartPose current)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            float positionError = Vector3.Distance(current.Position, spec.TargetPose.Position);
            float angleError = AngleBetweenDegrees(current.Rotation, ClosestEquivalentRotation(spec, current.Rotation));

            return new SnapEvaluation(
                positionError,
                angleError,
                positionError <= spec.PositionToleranceMetres,
                angleError <= spec.AngleToleranceDegrees);
        }

        /// <summary>
        /// The target rotation, or for a symmetric part whichever equivalent target rotation is closest to
        /// <paramref name="current"/> (so a flipped busbar snaps without spinning round).
        /// </summary>
        public static Quaternion ClosestEquivalentRotation(PartSpec spec, Quaternion current)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            var target = spec.TargetPose.Rotation;
            if (spec.Symmetry != PartSymmetry.HalfTurnAboutUp) return target;

            var flipped = target * HalfTurnAboutUp; // 180° about the part's own up axis
            return AngleBetweenDegrees(current, flipped) < AngleBetweenDegrees(current, target) ? flipped : target;
        }

        /// <summary>
        /// Smallest rotation angle (0..180 degrees) that takes <paramref name="a"/> to <paramref name="b"/>.
        /// Uses atan2 on the relative quaternion, which stays accurate for small angles (acos does not).
        /// </summary>
        public static float AngleBetweenDegrees(Quaternion a, Quaternion b)
        {
            a = Quaternion.Normalize(a);
            b = Quaternion.Normalize(b);
            Quaternion relative = Quaternion.Conjugate(a) * b;

            float vectorLength = new Vector3(relative.X, relative.Y, relative.Z).Length();
            float w = Math.Abs(relative.W); // q and -q are the same rotation
            return 2f * (float)Math.Atan2(vectorLength, w) * RadToDeg;
        }
    }
}
