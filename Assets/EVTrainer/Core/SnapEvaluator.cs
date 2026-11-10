using System;
using System.Numerics;

namespace EVTrainer.Core
{
    /// <summary>Pose-vs-target distance and angle maths.</summary>
    public static class SnapEvaluator
    {
        private const float RadToDeg = (float)(180.0 / Math.PI);

        public static SnapEvaluation Evaluate(PartSpec spec, PartPose current)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            float positionError = Vector3.Distance(current.Position, spec.TargetPose.Position);
            float angleError = AngleBetweenDegrees(current.Rotation, spec.TargetPose.Rotation);

            return new SnapEvaluation(
                positionError,
                angleError,
                positionError <= spec.PositionToleranceMetres,
                angleError <= spec.AngleToleranceDegrees);
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
