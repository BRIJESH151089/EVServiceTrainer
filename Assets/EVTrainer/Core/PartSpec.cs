using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    /// <summary>Rotational symmetry of a part type: which rotations of the part count as the same fit.</summary>
    public enum PartSymmetry
    {
        /// <summary>The part has a front; only the target rotation fits.</summary>
        None,
        /// <summary>Turning the part 180° about its own vertical (local up) axis fits equally well (e.g. a busbar).</summary>
        HalfTurnAboutUp,
    }

    /// <summary>
    /// One SLOT in an assembly: where a part goes, how precisely it must be placed, and what must be
    /// installed first. Identical parts share a <see cref="TypeId"/> and can go into any free slot of
    /// that type (e.g. four battery-module slots of type "battery_module").
    /// </summary>
    public sealed class PartSpec
    {
        public PartSpec(
            string id,
            string displayName,
            PartPose targetPose,
            float positionToleranceMetres,
            float angleToleranceDegrees,
            bool requiresTwoHands = false,
            IEnumerable<string> prerequisites = null,
            bool preInstalled = false,
            string instruction = null,
            string typeId = null,
            string groupName = null,
            PartSymmetry symmetry = PartSymmetry.None)
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
            PreInstalled = preInstalled;
            Instruction = string.IsNullOrWhiteSpace(instruction) ? "Fit " + DisplayName : instruction;
            TypeId = string.IsNullOrWhiteSpace(typeId) ? id : typeId;
            GroupName = string.IsNullOrWhiteSpace(groupName) ? DisplayName : groupName;
            Symmetry = symmetry;
        }

        /// <summary>Slot id, unique in the assembly (e.g. "module_2").</summary>
        public string Id { get; }

        /// <summary>Name of this slot's part (e.g. "Battery module 2").</summary>
        public string DisplayName { get; }

        public PartPose TargetPose { get; }
        public float PositionToleranceMetres { get; }
        public float AngleToleranceDegrees { get; }
        public bool RequiresTwoHands { get; }

        /// <summary>Ids of slots that must be installed before this one.</summary>
        public IReadOnlyList<string> Prerequisites { get; }

        /// <summary>True for parts already in place when training starts (e.g. the pack tray on the fixture).</summary>
        public bool PreInstalled { get; }

        /// <summary>Step text shown to the trainee, e.g. "Fit cooling plate". Shared by all slots of a step.</summary>
        public string Instruction { get; }

        /// <summary>Part type. Defaults to the slot id (a unique part). Identical parts share a type.</summary>
        public string TypeId { get; }

        /// <summary>Name of the part type as used in sentences, e.g. "battery modules" or "cooling plate".</summary>
        public string GroupName { get; }

        public PartSymmetry Symmetry { get; }
    }
}
