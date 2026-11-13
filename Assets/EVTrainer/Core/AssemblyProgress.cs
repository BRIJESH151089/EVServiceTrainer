using System;
using System.Collections.Generic;
using System.Text;

namespace EVTrainer.Core
{
    /// <summary>
    /// Run-time state of one assembly: which slots are installed and which step is current.
    /// Slots marked <see cref="PartSpec.PreInstalled"/> start installed.
    /// </summary>
    public sealed class AssemblyProgress
    {
        private readonly HashSet<string> _installed = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _installOrder = new List<string>();
        private readonly List<InstallRecord> _installRecords = new List<InstallRecord>();
        private readonly Dictionary<string, int> _failedAttempts = new Dictionary<string, int>(StringComparer.Ordinal);

        public AssemblyProgress(AssemblyDefinition definition, TrainingMode mode)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Mode = mode;
            foreach (var part in definition.Parts)
            {
                if (part.PreInstalled) MarkInstalled(part.Id);
            }
            foreach (var set in definition.BoltSets)
            {
                // Learn blocks out-of-pattern torquing; Test allows it and only logs it.
                var bolts = new BoltSet(set, blockOutOfOrder: mode == TrainingMode.Learn, Mistakes);
                _boltSets.Add(bolts);
                _boltSetsById.Add(set.Id, bolts);
            }
        }

        private readonly List<BoltSet> _boltSets = new List<BoltSet>();
        private readonly Dictionary<string, BoltSet> _boltSetsById = new Dictionary<string, BoltSet>(StringComparer.Ordinal);

        /// <summary>Run-time bolt sets, in definition order.</summary>
        public IReadOnlyList<BoltSet> BoltSets => _boltSets;

        public BoltSet GetBoltSet(string id)
        {
            if (id == null || !_boltSetsById.TryGetValue(id, out var set))
                throw new ArgumentException($"Unknown bolt set '{id}'.", nameof(id));
            return set;
        }

        /// <summary>Bolts can go in once the part they fasten is installed.</summary>
        public bool AreBoltsAvailable(BoltSet set) =>
            set.Spec.PrerequisitePartId == null || _installed.Contains(set.Spec.PrerequisitePartId);

        /// <summary>Done count of a step: installed slots, seated bolts (hand-start) or finished bolts (torque).</summary>
        public int CompletedCount(AssemblyStep step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            switch (step.Kind)
            {
                case StepKind.HandStartBolts: return GetBoltSet(step.BoltSet.Id).SeatedCount;
                case StepKind.TorqueBolts: return GetBoltSet(step.BoltSet.Id).FinishedCount;
                default: return InstalledCount(step);
            }
        }

        public bool IsStepComplete(AssemblyStep step) => CompletedCount(step) >= step.Size;

        public AssemblyDefinition Definition { get; }
        public TrainingMode Mode { get; }

        /// <summary>Snap-assist capture zone of this run's mode (Learn is wider than Test).</summary>
        public CaptureZone CaptureZone => Definition.SnapAssist.For(Mode);

        /// <summary>Installed slot ids in the order they were installed (pre-installed slots first).</summary>
        public IReadOnlyList<string> InstalledPartIds => _installOrder;

        /// <summary>Index into <see cref="AssemblyDefinition.Parts"/> of the first slot not yet installed;
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

        /// <summary>The first slot (in procedure order) not yet installed, or null when complete.</summary>
        public PartSpec CurrentPart
        {
            get
            {
                int index = CurrentStepIndex;
                return index < Definition.Parts.Count ? Definition.Parts[index] : null;
            }
        }

        /// <summary>The first procedure step that is not complete, or null when everything is done.</summary>
        public AssemblyStep CurrentStep
        {
            get
            {
                foreach (var step in Definition.Steps)
                {
                    if (!IsStepComplete(step)) return step;
                }
                return null;
            }
        }

        /// <summary>All parts installed and every bolt set finished (Torqued, or OverTorqued which is logged).</summary>
        public bool IsComplete
        {
            get
            {
                if (_installed.Count != Definition.Parts.Count) return false;
                foreach (var set in _boltSets) if (!set.IsComplete) return false;
                return true;
            }
        }

        public bool IsInstalled(string partId) => _installed.Contains(partId);

        /// <summary>Number of installed slots in <paramref name="step"/>.</summary>
        public int InstalledCount(AssemblyStep step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            int n = 0;
            foreach (var slot in step.Slots)
            {
                if (_installed.Contains(slot.Id)) n++;
            }
            return n;
        }

        /// <summary>A slot is available when it is not installed and all its prerequisites are.</summary>
        public bool IsAvailable(string partId)
        {
            var part = GetPart(partId);
            return !_installed.Contains(part.Id) && MissingPrerequisites(part).Count == 0;
        }

        /// <summary>All available slots, in procedure order.</summary>
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
        /// Judges a part of <paramref name="typeId"/> released at <paramref name="pose"/> without changing anything.
        /// The part is judged against the nearest free slot of its type (a slot within tolerance wins), then checks,
        /// in order: prerequisites of that slot, position tolerance, angle tolerance (symmetry-aware). Tolerances are
        /// widened to the mode's <see cref="CaptureZone"/>; an install then snaps to the exact <see cref="InstallResult.SnapPose"/>.
        /// </summary>
        public InstallResult Evaluate(string typeId, PartPose pose)
        {
            var slots = Definition.SlotsOfType(typeId);
            if (slots.Count == 0)
                throw new ArgumentException($"Unknown part type '{typeId}' for assembly '{Definition.Id}'.", nameof(typeId));

            var slot = PickSlot(slots, pose, out var evaluation);
            if (slot == null)
                return new InstallResult(InstallOutcome.AlreadyInstalled, slots[slots.Count - 1].Id, default, typeId: typeId);

            var missing = MissingPrerequisites(slot);
            if (missing.Count > 0)
                return new InstallResult(InstallOutcome.WrongOrder, slot.Id, evaluation, missing, OrderReason(slot, missing), typeId: typeId);

            var snapPose = new PartPose(slot.TargetPose.Position, SnapEvaluator.ClosestEquivalentRotation(slot, pose.Rotation));
            if (!evaluation.IsPositionWithinTolerance)
                return new InstallResult(InstallOutcome.OutOfTolerance, slot.Id, evaluation, snapPose: snapPose, typeId: typeId);
            if (!evaluation.IsAngleWithinTolerance)
                return new InstallResult(InstallOutcome.WrongOrientation, slot.Id, evaluation, snapPose: snapPose, typeId: typeId);

            return new InstallResult(InstallOutcome.Installed, slot.Id, evaluation, snapPose: snapPose, typeId: typeId);
        }

        /// <summary>
        /// Attempts to install a part of <paramref name="typeId"/> released at <paramref name="pose"/>
        /// (see <see cref="Evaluate"/>). On success the chosen slot becomes installed. Every attempt is
        /// tracked: failures go to <see cref="Mistakes"/>, successes to <see cref="InstallRecords"/>.
        /// </summary>
        /// <param name="timeSeconds">Run time of the attempt, for the logs.</param>
        /// <param name="itemId">The physical part released (identical parts are told apart by this);
        /// attempts are counted per item. Defaults to the type id.</param>
        public InstallResult TryInstall(string typeId, PartPose pose, double timeSeconds = 0, string itemId = null)
        {
            var result = Evaluate(typeId, pose);
            string item = string.IsNullOrEmpty(itemId) ? typeId : itemId;

            if (result.IsInstalled)
            {
                MarkInstalled(result.PartId);
                _failedAttempts.TryGetValue(item, out int failed);
                _installRecords.Add(new InstallRecord(result.PartId, typeId, item, timeSeconds, failed + 1));
            }
            else if (MistakeLog.TryGetKind(result.Outcome, out var kind))
            {
                _failedAttempts.TryGetValue(item, out int failed);
                _failedAttempts[item] = failed + 1;
                Mistakes.Record(new MistakeEntry(timeSeconds, kind, typeId, result.PartId, item, result.Reason));
            }
            return result;
        }

        /// <summary>Mistakes in this run, in order. Recorded in every mode.</summary>
        public MistakeLog Mistakes { get; } = new MistakeLog();

        /// <summary>Successful installs in order (pre-installed slots are not listed).</summary>
        public IReadOnlyList<InstallRecord> InstallRecords => _installRecords;

        /// <summary>Installs that succeeded on the part's first attempt.</summary>
        public int FirstTimeFitCount
        {
            get
            {
                int n = 0;
                foreach (var r in _installRecords) if (r.IsFirstTimeFit) n++;
                return n;
            }
        }

        /// <summary>
        /// Nearest free slot to <paramref name="pose"/>. A slot the pose is fully within tolerance of wins over a
        /// merely nearer one, so tightly packed slots never steal a correct fit. Null when every slot is installed.
        /// </summary>
        private PartSpec PickSlot(IReadOnlyList<PartSpec> slots, PartPose pose, out SnapEvaluation evaluation)
        {
            PartSpec best = null;
            evaluation = default;
            bool bestFits = false;
            foreach (var slot in slots)
            {
                if (_installed.Contains(slot.Id)) continue;
                var e = SnapEvaluator.Evaluate(slot, pose, CaptureZone);
                bool fits = e.IsWithinTolerance;
                if (best == null || (fits && !bestFits) || (fits == bestFits && e.PositionErrorMetres < evaluation.PositionErrorMetres))
                {
                    best = slot;
                    evaluation = e;
                    bestFits = fits;
                }
            }
            return best;
        }

        /// <summary>"Cooling plate must be fitted before the battery modules."</summary>
        private string OrderReason(PartSpec slot, List<string> missing)
        {
            var groups = new List<string>();
            foreach (var id in missing)
            {
                Definition.TryGetPart(id, out var part);
                if (!groups.Contains(part.GroupName)) groups.Add(part.GroupName);
            }

            var sb = new StringBuilder();
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) sb.Append(i == groups.Count - 1 ? " and " : ", ");
                sb.Append(groups[i]);
            }
            if (sb.Length > 0) sb[0] = char.ToUpperInvariant(sb[0]);
            sb.Append(" must be fitted before the ").Append(slot.GroupName).Append('.');
            return sb.ToString();
        }

        private PartSpec GetPart(string partId)
        {
            if (!Definition.TryGetPart(partId, out var part))
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
