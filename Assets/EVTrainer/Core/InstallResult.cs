using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    public enum InstallOutcome
    {
        /// <summary>Within position and angle tolerance, prerequisites met: the part is now installed.</summary>
        Installed,
        /// <summary>Too far from the target position (angle may also be off).</summary>
        OutOfTolerance,
        /// <summary>Position is within tolerance but the part is rotated beyond the angle tolerance.</summary>
        WrongOrientation,
        /// <summary>One or more prerequisite parts are not installed yet.</summary>
        WrongOrder,
        /// <summary>Every slot for this part type is already installed; nothing changed.</summary>
        AlreadyInstalled,
    }

    /// <summary>Result of one install attempt (<see cref="AssemblyProgress.TryInstall"/> or <see cref="AssemblyProgress.Evaluate"/>).</summary>
    public readonly struct InstallResult
    {
        private static readonly IReadOnlyList<string> NoIds = Array.Empty<string>();

        private readonly IReadOnlyList<string> _missingPrerequisites;

        public InstallResult(InstallOutcome outcome, string partId, SnapEvaluation evaluation,
            IReadOnlyList<string> missingPrerequisites = null, string reason = null,
            PartPose snapPose = default, string typeId = null)
        {
            Outcome = outcome;
            PartId = partId;
            Evaluation = evaluation;
            _missingPrerequisites = missingPrerequisites;
            Reason = reason ?? "";
            SnapPose = snapPose;
            TypeId = typeId ?? partId;
        }

        public InstallOutcome Outcome { get; }

        /// <summary>The slot the attempt was judged against (the nearest free slot of the part's type).</summary>
        public string PartId { get; }

        /// <summary>The part type that was released.</summary>
        public string TypeId { get; }

        /// <summary>Pose error against <see cref="PartId"/>. Default (zero) for AlreadyInstalled.</summary>
        public SnapEvaluation Evaluation { get; }

        /// <summary>Prerequisite slot ids that were not installed. Empty unless the outcome is WrongOrder.</summary>
        public IReadOnlyList<string> MissingPrerequisites => _missingPrerequisites ?? NoIds;

        /// <summary>Human explanation for WrongOrder, e.g. "Cooling plate must be fitted before the battery modules."</summary>
        public string Reason { get; }

        /// <summary>Exact pose to snap to when installed (for symmetric parts, the equivalent rotation nearest the release).</summary>
        public PartPose SnapPose { get; }

        public bool IsInstalled => Outcome == InstallOutcome.Installed;

        public override string ToString() => $"{Outcome} ({PartId})";
    }
}
