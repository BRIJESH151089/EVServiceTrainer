using System;
using System.Numerics;

namespace EVTrainer.Core
{
    /// <summary>Pose-vs-target distance and angle maths, symmetry-aware.</summary>
    public static class SnapEvaluator
    {
        private const float RadToDeg = (float)(180.0 / Math.PI);
        private static readonly Quaternion HalfTurnAboutUp = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Math.PI);

        public static SnapEvaluation Evaluate(PartSpec spec, PartPose current) => Evaluate(spec, current, CaptureZone.None);

        /// <summary>
        /// As <see cref="Evaluate(PartSpec, PartPose)"/>, but the accepted error is the larger of the part's own
        /// tolerance and the snap-assist <paramref name="zone"/> (the part is then pulled into its exact pose).
        /// </summary>
        public static SnapEvaluation Evaluate(PartSpec spec, PartPose current, CaptureZone zone)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            float positionError = Vector3.Distance(current.Position, spec.TargetPose.Position);
            float angleError = AngleBetweenDegrees(current.Rotation, ClosestEquivalentRotation(spec, current.Rotation));

            return new SnapEvaluation(
                positionError,
                angleError,
                positionError <= PositionTolerance(spec, zone),
                angleError <= AngleTolerance(spec, zone));
        }

        /// <summary>Effective position tolerance: the part's own, widened to the capture zone.</summary>
        public static float PositionTolerance(PartSpec spec, CaptureZone zone) => Math.Max(spec.PositionToleranceMetres, zone.PositionMetres);

        /// <summary>Effective angle tolerance: the part's own, widened to the capture zone.</summary>
        public static float AngleTolerance(PartSpec spec, CaptureZone zone) => Math.Max(spec.AngleToleranceDegrees, zone.AngleDegrees);

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
