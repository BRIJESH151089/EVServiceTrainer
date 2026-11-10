using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    /// <summary>Data describing one assembly task (e.g. the battery pack). Phase 1: parts only.</summary>
    public sealed class AssemblyDefinition
    {
        private readonly Dictionary<string, PartSpec> _partsById;

        public AssemblyDefinition(string id, string title, IEnumerable<PartSpec> parts)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Assembly id is required.", nameof(id));
            if (parts == null) throw new ArgumentNullException(nameof(parts));

            Id = id;
            Title = title ?? id;

            var list = new List<PartSpec>();
            _partsById = new Dictionary<string, PartSpec>(StringComparer.Ordinal);
            foreach (var part in parts)
            {
                if (part == null) throw new ArgumentException("Parts may not contain null.", nameof(parts));
                if (_partsById.ContainsKey(part.Id)) throw new ArgumentException($"Duplicate part id '{part.Id}'.", nameof(parts));
                _partsById.Add(part.Id, part);
                list.Add(part);
            }
            Parts = list.AsReadOnly();
        }

        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<PartSpec> Parts { get; }

        public bool TryGetPart(string partId, out PartSpec part) => _partsById.TryGetValue(partId, out part);
    }
}
