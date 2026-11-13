using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    public enum StepKind
    {
        /// <summary>Fit parts into slots.</summary>
        Parts,
        /// <summary>Hand-start every bolt of a bolt set until seated.</summary>
        HandStartBolts,
        /// <summary>Torque every bolt of a bolt set in its pattern.</summary>
        TorqueBolts,
    }

    /// <summary>
    /// One line of the procedure: consecutive slots of the same part type (e.g. "Install battery modules", 4 slots),
    /// or one phase of a bolt set ("Hand-start cover bolts", "Torque bolts in cross pattern").
    /// </summary>
    public sealed class AssemblyStep
    {
        private static readonly IReadOnlyList<PartSpec> NoSlots = Array.Empty<PartSpec>();

        internal AssemblyStep(int index, string typeId, string instruction, IReadOnlyList<PartSpec> slots,
            StepKind kind = StepKind.Parts, BoltSetSpec boltSet = null)
        {
            Index = index;
            TypeId = typeId;
            Instruction = instruction;
            Slots = slots ?? NoSlots;
            Kind = kind;
            BoltSet = boltSet;
        }

        public int Index { get; }
        public string TypeId { get; }
        public string Instruction { get; }

        /// <summary>Part slots of a Parts step; empty for bolt steps.</summary>
        public IReadOnlyList<PartSpec> Slots { get; }

        public StepKind Kind { get; }

        /// <summary>The bolt set of a bolt step; null for Parts steps.</summary>
        public BoltSetSpec BoltSet { get; }

        /// <summary>How many things the step contains (slots or bolts).</summary>
        public int Size => Kind == StepKind.Parts ? Slots.Count : BoltSet.Bolts.Count;

        /// <summary>True for a parts step whose slots are all pre-installed (nothing for the trainee to do).</summary>
        public bool IsPreInstalled
        {
            get
            {
                if (Kind != StepKind.Parts || Slots.Count == 0) return false;
                foreach (var s in Slots) if (!s.PreInstalled) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// Data describing one assembly task (e.g. the battery pack). Slots are listed in procedure order;
    /// consecutive slots of the same type form one <see cref="AssemblyStep"/>. Bolt sets add two steps each
    /// (hand-start, then torque) after the parts.
    /// </summary>
    public sealed class AssemblyDefinition
    {
        private readonly Dictionary<string, PartSpec> _partsById;
        private readonly Dictionary<string, List<PartSpec>> _slotsByType;

        public AssemblyDefinition(string id, string title, IEnumerable<PartSpec> parts, IEnumerable<BoltSetSpec> boltSets = null,
            SnapAssist snapAssist = null)
        {
            SnapAssist = snapAssist ?? SnapAssist.None;
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Assembly id is required.", nameof(id));
            if (parts == null) throw new ArgumentNullException(nameof(parts));

            Id = id;
            Title = title ?? id;

            var list = new List<PartSpec>();
            _partsById = new Dictionary<string, PartSpec>(StringComparer.Ordinal);
            _slotsByType = new Dictionary<string, List<PartSpec>>(StringComparer.Ordinal);
            foreach (var part in parts)
            {
                if (part == null) throw new ArgumentException("Parts may not contain null.", nameof(parts));
                if (_partsById.ContainsKey(part.Id)) throw new ArgumentException($"Duplicate part id '{part.Id}'.", nameof(parts));
                _partsById.Add(part.Id, part);
                list.Add(part);
                if (!_slotsByType.TryGetValue(part.TypeId, out var slots)) _slotsByType.Add(part.TypeId, slots = new List<PartSpec>());
                slots.Add(part);
            }

            foreach (var part in list)
            {
                foreach (var prerequisite in part.Prerequisites)
                {
                    if (prerequisite == part.Id)
                        throw new ArgumentException($"Part '{part.Id}' lists itself as a prerequisite.", nameof(parts));
                    if (!_partsById.ContainsKey(prerequisite))
                        throw new ArgumentException($"Part '{part.Id}' has unknown prerequisite '{prerequisite}'.", nameof(parts));
                }
            }

            var sets = new List<BoltSetSpec>();
            var setIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var set in boltSets ?? Array.Empty<BoltSetSpec>())
            {
                if (set == null) throw new ArgumentException("Bolt sets may not contain null.", nameof(boltSets));
                if (!setIds.Add(set.Id)) throw new ArgumentException($"Duplicate bolt set id '{set.Id}'.", nameof(boltSets));
                if (set.PrerequisitePartId != null && !_partsById.ContainsKey(set.PrerequisitePartId))
                    throw new ArgumentException($"Bolt set '{set.Id}' needs unknown part '{set.PrerequisitePartId}'.", nameof(boltSets));
                sets.Add(set);
            }

            Parts = list.AsReadOnly();
            BoltSets = sets.AsReadOnly();
            Steps = BuildSteps(list, sets);
        }

        public string Id { get; }
        public string Title { get; }

        /// <summary>Capture zones per mode for parts and bolts (magnetic snap assist).</summary>
        public SnapAssist SnapAssist { get; }

        /// <summary>All slots in procedure order.</summary>
        public IReadOnlyList<PartSpec> Parts { get; }

        /// <summary>Bolt sets (e.g. the cover bolts), in procedure order after the parts.</summary>
        public IReadOnlyList<BoltSetSpec> BoltSets { get; }

        /// <summary>Procedure steps: consecutive slots of the same type grouped together, then two steps per bolt set.</summary>
        public IReadOnlyList<AssemblyStep> Steps { get; }

        public bool TryGetPart(string partId, out PartSpec part)
        {
            part = null;
            return partId != null && _partsById.TryGetValue(partId, out part);
        }

        /// <summary>All slots that accept parts of <paramref name="typeId"/>, in procedure order (empty if none).</summary>
        public IReadOnlyList<PartSpec> SlotsOfType(string typeId) =>
            typeId != null && _slotsByType.TryGetValue(typeId, out var slots) ? slots : (IReadOnlyList<PartSpec>)Array.Empty<PartSpec>();

        private static IReadOnlyList<AssemblyStep> BuildSteps(List<PartSpec> parts, List<BoltSetSpec> boltSets)
        {
            var steps = new List<AssemblyStep>();
            int i = 0;
            while (i < parts.Count)
            {
                var group = new List<PartSpec> { parts[i] };
                int j = i + 1;
                while (j < parts.Count && parts[j].TypeId == parts[i].TypeId) group.Add(parts[j++]);
                steps.Add(new AssemblyStep(steps.Count, parts[i].TypeId, parts[i].Instruction, group.AsReadOnly()));
                i = j;
            }
            foreach (var set in boltSets)
            {
                steps.Add(new AssemblyStep(steps.Count, set.Id + ":hand-start", set.HandStartInstruction, null, StepKind.HandStartBolts, set));
                steps.Add(new AssemblyStep(steps.Count, set.Id + ":torque", set.TorqueInstruction, null, StepKind.TorqueBolts, set));
            }
            return steps.AsReadOnly();
        }
    }
}
