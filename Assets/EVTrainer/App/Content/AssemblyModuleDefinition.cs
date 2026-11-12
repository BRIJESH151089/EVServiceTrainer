using System;
using System.Collections.Generic;
using EVTrainer.Core;
using EVTrainer.XR;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// Authoring data for one training module (e.g. the battery pack). Target poses are relative
    /// to the module's assembly origin (the fixture), so the same data works wherever the fixture
    /// is placed. Converted into a plain Core <see cref="AssemblyDefinition"/> at start-up.
    /// </summary>
    [CreateAssetMenu(menuName = "EV Trainer/Assembly Module", fileName = "AssemblyModule")]
    public sealed class AssemblyModuleDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class PartEntry
        {
            public string id;
            public string displayName;
            [Tooltip("Step text in the HUD. Empty = \"Fit <display name>\".")]
            public string instruction;
            [Tooltip("Target position relative to the assembly origin (metres).")]
            public Vector3 localPosition;
            [Tooltip("Target rotation relative to the assembly origin (Euler degrees).")]
            public Vector3 localEulerAngles;
            [Min(0f)] public float positionToleranceMetres = 0.015f;
            [Range(0f, 180f)] public float angleToleranceDegrees = 8f;
            public bool requiresTwoHands;
            [Tooltip("Already in place when the module starts (e.g. the tray on the fixture).")]
            public bool preInstalled;
            [Tooltip("Ids of parts that must be installed first.")]
            public string[] prerequisites = Array.Empty<string>();
        }

        [SerializeField] private string _id = "module";
        [SerializeField] private string _title = "Module";
        [SerializeField] private List<PartEntry> _parts = new List<PartEntry>();

        public string Id => _id;
        public string Title => _title;
        public IReadOnlyList<PartEntry> Parts => _parts;

        /// <summary>Builds the Core definition with target poses in world space, placed by <paramref name="origin"/>.</summary>
        public AssemblyDefinition ToCore(Transform origin)
        {
            if (origin == null) throw new ArgumentNullException(nameof(origin));

            var specs = new List<PartSpec>(_parts.Count);
            foreach (var p in _parts)
            {
                var worldPosition = origin.TransformPoint(p.localPosition);
                var worldRotation = origin.rotation * Quaternion.Euler(p.localEulerAngles);
                specs.Add(new PartSpec(
                    p.id, p.displayName,
                    new PartPose(worldPosition.ToNumerics(), worldRotation.ToNumerics()),
                    p.positionToleranceMetres, p.angleToleranceDegrees,
                    p.requiresTwoHands, p.prerequisites, p.preInstalled, p.instruction));
            }
            return new AssemblyDefinition(_id, _title, specs);
        }
    }
}
