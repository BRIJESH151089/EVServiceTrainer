using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    /// <summary>Static description of one part: where it goes and how precisely it must be placed.</summary>
    public sealed class PartSpec
    {
        public PartSpec(
            string id,
            string displayName,
            PartPose targetPose,
            float positionToleranceMetres,
            float angleToleranceDegrees,
            bool requiresTwoHands = false,
            IEnumerable<string> prerequisites = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Part id is required.", nameof(id));
            if (positionToleranceMetres < 0f) throw new ArgumentOutOfRangeException(nameof(positionToleranceMetres));
            if (angleToleranceDegrees < 0f || angleToleranceDegrees > 180f) throw new ArgumentOutOfRangeException(nameof(angleToleranceDegrees));

            Id = id;
            DisplayName = displayName ?? id;
            TargetPose = targetPose;
            PositionToleranceMetres = positionToleranceMetres;
            AngleToleranceDegrees = angleToleranceDegrees;
            RequiresTwoHands = requiresTwoHands;
            Prerequisites = new List<string>(prerequisites ?? Array.Empty<string>()).AsReadOnly();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public PartPose TargetPose { get; }
        public float PositionToleranceMetres { get; }
        public float AngleToleranceDegrees { get; }
        public bool RequiresTwoHands { get; }

        /// <summary>Ids of parts that must be installed before this one.</summary>
        public IReadOnlyList<string> Prerequisites { get; }
    }
}
