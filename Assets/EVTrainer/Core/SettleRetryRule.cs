using System;

namespace EVTrainer.Core
{
    /// <summary>
    /// Decides whether a failed release deserves one more <see cref="AssemblyProgress.TryInstall"/>
    /// after the part has settled under physics: the part is above its target (within the part's
    /// horizontal position tolerance) and at most <see cref="DefaultMaxHeightAboveTargetMetres"/> (6 cm)
    /// above it, so dropping it a few centimetres onto its seat still counts.
    /// World up is +Y (target poses are in world space).
    /// </summary>
    public static class SettleRetryRule
    {
        public const float DefaultMaxHeightAboveTargetMetres = 0.06f;

        public static bool IsEligible(in InstallResult result, PartSpec spec, PartPose releasedPose,
            float maxHeightAboveTargetMetres = DefaultMaxHeightAboveTargetMetres)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (maxHeightAboveTargetMetres < 0f) throw new ArgumentOutOfRangeException(nameof(maxHeightAboveTargetMetres));

            // Only pose failures can be fixed by settling; order problems and success cannot.
            if (result.Outcome != InstallOutcome.OutOfTolerance && result.Outcome != InstallOutcome.WrongOrientation)
                return false;

            var delta = releasedPose.Position - spec.TargetPose.Position;
            float horizontal = (float)Math.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            float height = delta.Y;

            return horizontal <= spec.PositionToleranceMetres
                && height <= maxHeightAboveTargetMetres
                && height >= -spec.PositionToleranceMetres; // slightly low (e.g. pressed into the seat) is fine
        }
    }
}
