using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    /// <summary>One threaded hole and the bolt that goes in it.</summary>
    public sealed class BoltSpec
    {
        public BoltSpec(
            string holeId,
            PartPose holePose,
            float threadTurnsToSeat = 3f,
            float torqueSpecNm = 8f,
            float torqueToleranceNm = 0.4f,
            float stiffnessNmPerDegree = 0.1f,
            float maxStartTiltDegrees = 10f)
        {
            if (string.IsNullOrWhiteSpace(holeId)) throw new ArgumentException("Hole id is required.", nameof(holeId));
            if (threadTurnsToSeat <= 0f) throw new ArgumentOutOfRangeException(nameof(threadTurnsToSeat));
            if (torqueSpecNm <= 0f) throw new ArgumentOutOfRangeException(nameof(torqueSpecNm));
            if (torqueToleranceNm < 0f || torqueToleranceNm >= torqueSpecNm) throw new ArgumentOutOfRangeException(nameof(torqueToleranceNm));
            if (stiffnessNmPerDegree <= 0f) throw new ArgumentOutOfRangeException(nameof(stiffnessNmPerDegree));
            if (maxStartTiltDegrees < 0f || maxStartTiltDegrees > 90f) throw new ArgumentOutOfRangeException(nameof(maxStartTiltDegrees));

            HoleId = holeId;
            HolePose = holePose;
            ThreadTurnsToSeat = threadTurnsToSeat;
            TorqueSpecNm = torqueSpecNm;
            TorqueToleranceNm = torqueToleranceNm;
            StiffnessNmPerDegree = stiffnessNmPerDegree;
            MaxStartTiltDegrees = maxStartTiltDegrees;
        }

        public string HoleId { get; }

        /// <summary>World pose of the hole: position on the surface, rotation whose up (+Y) is the bolt axis (out of the hole).</summary>
        public PartPose HolePose { get; }

        /// <summary>Full turns by hand from first engagement until the head sits on the surface.</summary>
        public float ThreadTurnsToSeat { get; }

        public float TorqueSpecNm { get; }
        public float TorqueToleranceNm { get; }

        /// <summary>Torque gained per degree of wrench rotation once seated.</summary>
        public float StiffnessNmPerDegree { get; }

        /// <summary>Starting the thread with more tilt than this cross-threads the bolt.</summary>
        public float MaxStartTiltDegrees { get; }

        public float MinTorqueNm => TorqueSpecNm - TorqueToleranceNm;
        public float MaxTorqueNm => TorqueSpecNm + TorqueToleranceNm;
    }

    /// <summary>A set of bolts fastening one part (e.g. the 8 pack-cover bolts), with its tightening pattern.</summary>
    public sealed class BoltSetSpec
    {
        public BoltSetSpec(
            string id,
            string prerequisitePartId,
            IEnumerable<BoltSpec> bolts,
            IEnumerable<string> patternOrder,
            string handStartInstruction = "Hand-start bolts",
            string torqueInstruction = "Torque bolts in pattern",
            string groupName = "bolts")
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Bolt set id is required.", nameof(id));
            if (bolts == null) throw new ArgumentNullException(nameof(bolts));
            Id = id;
            PrerequisitePartId = prerequisitePartId;
            var list = new List<BoltSpec>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in bolts)
            {
                if (b == null) throw new ArgumentException("Bolts may not contain null.", nameof(bolts));
                if (!ids.Add(b.HoleId)) throw new ArgumentException($"Duplicate hole id '{b.HoleId}'.", nameof(bolts));
                list.Add(b);
            }
            if (list.Count == 0) throw new ArgumentException("A bolt set needs at least one bolt.", nameof(bolts));
            Bolts = list.AsReadOnly();

            var order = new List<string>(patternOrder ?? Array.Empty<string>());
            if (order.Count != list.Count || new HashSet<string>(order, StringComparer.Ordinal).Count != order.Count)
                throw new ArgumentException("The pattern must list every hole exactly once.", nameof(patternOrder));
            foreach (var h in order)
                if (!ids.Contains(h)) throw new ArgumentException($"Pattern names unknown hole '{h}'.", nameof(patternOrder));
            PatternOrder = order.AsReadOnly();

            HandStartInstruction = handStartInstruction;
            TorqueInstruction = torqueInstruction;
            GroupName = groupName;
        }

        public string Id { get; }

        /// <summary>Part that must be installed before any bolt can go in (e.g. "cover"). Null = none.</summary>
        public string PrerequisitePartId { get; }

        public IReadOnlyList<BoltSpec> Bolts { get; }

        /// <summary>Hole ids in the required tightening order (e.g. a cross pattern).</summary>
        public IReadOnlyList<string> PatternOrder { get; }

        public string HandStartInstruction { get; }
        public string TorqueInstruction { get; }
        public string GroupName { get; }
    }
}
