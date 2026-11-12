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
        /// <summary>The part was already installed; nothing changed.</summary>
        AlreadyInstalled,
    }

    /// <summary>Result of one <see cref="AssemblyProgress.TryInstall"/> attempt.</summary>
    public readonly struct InstallResult
    {
        private static readonly IReadOnlyList<string> NoIds = Array.Empty<string>();

        private readonly IReadOnlyList<string> _missingPrerequisites;

        public InstallResult(InstallOutcome outcome, string partId, SnapEvaluation evaluation,
            IReadOnlyList<string> missingPrerequisites = null)
        {
            Outcome = outcome;
            PartId = partId;
            Evaluation = evaluation;
            _missingPrerequisites = missingPrerequisites;
        }

        public InstallOutcome Outcome { get; }
        public string PartId { get; }

        /// <summary>Pose error at the attempt. Default (zero) for WrongOrder and AlreadyInstalled.</summary>
        public SnapEvaluation Evaluation { get; }

        /// <summary>Prerequisite part ids that were not installed. Empty unless the outcome is WrongOrder.</summary>
        public IReadOnlyList<string> MissingPrerequisites => _missingPrerequisites ?? NoIds;

        public bool IsInstalled => Outcome == InstallOutcome.Installed;

        public override string ToString() => $"{Outcome} ({PartId})";
    }
}
