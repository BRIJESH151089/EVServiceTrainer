using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    /// <summary>One line of the procedure: consecutive slots of the same part type (e.g. "Install battery modules", 4 slots).</summary>
    public sealed class AssemblyStep
    {
        internal AssemblyStep(int index, string typeId, string instruction, IReadOnlyList<PartSpec> slots)
        {
            Index = index;
            TypeId = typeId;
            Instruction = instruction;
            Slots = slots;
        }

        public int Index { get; }
        public string TypeId { get; }
        public string Instruction { get; }
        public IReadOnlyList<PartSpec> Slots { get; }
    }

    /// <summary>
    /// Data describing one assembly task (e.g. the battery pack). Slots are listed in procedure order;
    /// consecutive slots of the same type form one <see cref="AssemblyStep"/>.
    /// </summary>
    public sealed class AssemblyDefinition
    {
        private readonly Dictionary<string, PartSpec> _partsById;
        private readonly Dictionary<string, List<PartSpec>> _slotsByType;

        public AssemblyDefinition(string id, string title, IEnumerable<PartSpec> parts)
        {
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
            Parts = list.AsReadOnly();
            Steps = BuildSteps(list);
        }

        public string Id { get; }
        public string Title { get; }

        /// <summary>All slots in procedure order.</summary>
        public IReadOnlyList<PartSpec> Parts { get; }

        /// <summary>Procedure steps: consecutive slots of the same type grouped together.</summary>
        public IReadOnlyList<AssemblyStep> Steps { get; }

        public bool TryGetPart(string partId, out PartSpec part)
        {
            part = null;
            return partId != null && _partsById.TryGetValue(partId, out part);
        }

        /// <summary>All slots that accept parts of <paramref name="typeId"/>, in procedure order (empty if none).</summary>
        public IReadOnlyList<PartSpec> SlotsOfType(string typeId) =>
            typeId != null && _slotsByType.TryGetValue(typeId, out var slots) ? slots : (IReadOnlyList<PartSpec>)Array.Empty<PartSpec>();

        private static IReadOnlyList<AssemblyStep> BuildSteps(List<PartSpec> parts)
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
            return steps.AsReadOnly();
        }
    }
}
