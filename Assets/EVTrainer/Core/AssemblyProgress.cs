using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    /// <summary>
    /// Run-time state of one assembly: which parts are installed and which step is current.
    /// Parts marked <see cref="PartSpec.PreInstalled"/> start installed.
    /// </summary>
    public sealed class AssemblyProgress
    {
        private readonly HashSet<string> _installed = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _installOrder = new List<string>();

        public AssemblyProgress(AssemblyDefinition definition, TrainingMode mode)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Mode = mode;
            foreach (var part in definition.Parts)
            {
                if (part.PreInstalled) MarkInstalled(part.Id);
            }
        }

        public AssemblyDefinition Definition { get; }
        public TrainingMode Mode { get; }

        /// <summary>Installed part ids in the order they were installed (pre-installed parts first).</summary>
        public IReadOnlyList<string> InstalledPartIds => _installOrder;

        /// <summary>Index into <see cref="AssemblyDefinition.Parts"/> of the first part not yet installed;
        /// equals the part count when complete.</summary>
        public int CurrentStepIndex
        {
            get
            {
                var parts = Definition.Parts;
                for (int i = 0; i < parts.Count; i++)
                {
                    if (!_installed.Contains(parts[i].Id)) return i;
                }
                return parts.Count;
            }
        }

        /// <summary>The first part (in procedure order) not yet installed, or null when complete.</summary>
        public PartSpec CurrentPart
        {
            get
            {
                int index = CurrentStepIndex;
                return index < Definition.Parts.Count ? Definition.Parts[index] : null;
            }
        }

        public bool IsComplete => _installed.Count == Definition.Parts.Count;

        public bool IsInstalled(string partId) => _installed.Contains(partId);

        /// <summary>A part is available when it is not installed and all its prerequisites are.</summary>
        public bool IsAvailable(string partId)
        {
            var part = GetPart(partId);
            return !_installed.Contains(part.Id) && MissingPrerequisites(part).Count == 0;
        }

        /// <summary>All available parts, in procedure order.</summary>
        public IReadOnlyList<PartSpec> AvailableParts
        {
            get
            {
                var result = new List<PartSpec>();
                foreach (var part in Definition.Parts)
                {
                    if (IsAvailable(part.Id)) result.Add(part);
                }
                return result;
            }
        }

        /// <summary>
        /// Attempts to install a part released at <paramref name="pose"/>. Checks, in order:
        /// already installed, prerequisites, position tolerance, angle tolerance.
        /// </summary>
        public InstallResult TryInstall(string partId, PartPose pose)
        {
            var part = GetPart(partId);

            if (_installed.Contains(part.Id))
                return new InstallResult(InstallOutcome.AlreadyInstalled, part.Id, default);

            var missing = MissingPrerequisites(part);
            if (missing.Count > 0)
                return new InstallResult(InstallOutcome.WrongOrder, part.Id, default, missing);

            var evaluation = SnapEvaluator.Evaluate(part, pose);
            if (!evaluation.IsPositionWithinTolerance)
                return new InstallResult(InstallOutcome.OutOfTolerance, part.Id, evaluation);
            if (!evaluation.IsAngleWithinTolerance)
                return new InstallResult(InstallOutcome.WrongOrientation, part.Id, evaluation);

            MarkInstalled(part.Id);
            return new InstallResult(InstallOutcome.Installed, part.Id, evaluation);
        }

        private PartSpec GetPart(string partId)
        {
            if (partId == null || !Definition.TryGetPart(partId, out var part))
                throw new ArgumentException($"Unknown part id '{partId}' for assembly '{Definition.Id}'.", nameof(partId));
            return part;
        }

        private List<string> MissingPrerequisites(PartSpec part)
        {
            var missing = new List<string>();
            foreach (var prerequisite in part.Prerequisites)
            {
                if (!_installed.Contains(prerequisite)) missing.Add(prerequisite);
            }
            return missing;
        }

        private void MarkInstalled(string partId)
        {
            if (_installed.Add(partId)) _installOrder.Add(partId);
        }
    }
}
