namespace EVTrainer.Core
{
    /// <summary>Result of comparing a current pose against a part's target pose.</summary>
    public readonly struct SnapEvaluation
    {
        public SnapEvaluation(float positionErrorMetres, float angleErrorDegrees, bool isPositionWithinTolerance, bool isAngleWithinTolerance)
        {
            PositionErrorMetres = positionErrorMetres;
            AngleErrorDegrees = angleErrorDegrees;
            IsPositionWithinTolerance = isPositionWithinTolerance;
            IsAngleWithinTolerance = isAngleWithinTolerance;
        }

        public float PositionErrorMetres { get; }
        public float AngleErrorDegrees { get; }
        public bool IsPositionWithinTolerance { get; }
        public bool IsAngleWithinTolerance { get; }
        public bool IsWithinTolerance => IsPositionWithinTolerance && IsAngleWithinTolerance;
    }
}
