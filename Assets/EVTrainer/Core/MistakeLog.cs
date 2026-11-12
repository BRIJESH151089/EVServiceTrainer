using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    public enum MistakeKind
    {
        /// <summary>Released before its prerequisites were installed.</summary>
        WrongOrder,
        /// <summary>In position but rotated beyond tolerance (e.g. a module turned 180°).</summary>
        WrongOrientation,
        /// <summary>Released too far from its seat.</summary>
        NotSeated,
        /// <summary>A bolt started at too steep an angle and jammed.</summary>
        CrossThread,
        /// <summary>A bolt torqued out of the tightening pattern order.</summary>
        WrongSequence,
        /// <summary>A bolt torqued above spec + tolerance.</summary>
        OverTorque,
        /// <summary>The wrench was used before every bolt was hand-started.</summary>
        TorqueBeforeAllSeated,
    }

    /// <summary>One logged mistake. Time is seconds since the run started (supplied by the caller).</summary>
    public readonly struct MistakeEntry
    {
        public MistakeEntry(double timeSeconds, MistakeKind kind, string typeId, string slotId, string itemId, string reason)
        {
            TimeSeconds = timeSeconds;
            Kind = kind;
            TypeId = typeId;
            SlotId = slotId;
            ItemId = itemId;
            Reason = reason ?? "";
        }

        public double TimeSeconds { get; }
        public MistakeKind Kind { get; }
        public string TypeId { get; }

        /// <summary>The slot the attempt was judged against.</summary>
        public string SlotId { get; }

        /// <summary>The physical part that was released (e.g. "Battery Module 3"), or the type id if not given.</summary>
        public string ItemId { get; }

        public string Reason { get; }
    }

    /// <summary>Chronological record of mistakes in one run. Logged in every mode; the App decides what to show.</summary>
    public sealed class MistakeLog
    {
        private readonly List<MistakeEntry> _entries = new List<MistakeEntry>();

        public IReadOnlyList<MistakeEntry> Entries => _entries;
        public int Count => _entries.Count;

        public void Record(MistakeEntry entry) => _entries.Add(entry);

        public int CountOf(MistakeKind kind)
        {
            int n = 0;
            foreach (var e in _entries) if (e.Kind == kind) n++;
            return n;
        }

        public int CountForType(string typeId)
        {
            int n = 0;
            foreach (var e in _entries) if (e.TypeId == typeId) n++;
            return n;
        }

        /// <summary>Maps a failed install outcome to a mistake kind; false for Installed / AlreadyInstalled.</summary>
        public static bool TryGetKind(InstallOutcome outcome, out MistakeKind kind)
        {
            switch (outcome)
            {
                case InstallOutcome.WrongOrder: kind = MistakeKind.WrongOrder; return true;
                case InstallOutcome.WrongOrientation: kind = MistakeKind.WrongOrientation; return true;
                case InstallOutcome.OutOfTolerance: kind = MistakeKind.NotSeated; return true;
                default: kind = default; return false;
            }
        }
    }

    /// <summary>One successful install: which physical part went where, and whether it fitted first time.</summary>
    public readonly struct InstallRecord
    {
        public InstallRecord(string slotId, string typeId, string itemId, double timeSeconds, int attempts)
        {
            if (attempts < 1) throw new ArgumentOutOfRangeException(nameof(attempts));
            SlotId = slotId;
            TypeId = typeId;
            ItemId = itemId;
            TimeSeconds = timeSeconds;
            Attempts = attempts;
        }

        public string SlotId { get; }
        public string TypeId { get; }
        public string ItemId { get; }
        public double TimeSeconds { get; }

        /// <summary>Recorded attempts with this physical part, including the successful one.</summary>
        public int Attempts { get; }

        public bool IsFirstTimeFit => Attempts == 1;
    }
}
