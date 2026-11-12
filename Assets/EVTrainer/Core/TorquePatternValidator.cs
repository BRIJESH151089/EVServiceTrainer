using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    public enum PatternCheck
    {
        Ok,
        /// <summary>A bolt was torqued before one that comes earlier in the pattern.</summary>
        WrongSequence,
        /// <summary>One or more bolts in the pattern were never torqued.</summary>
        Skipped,
    }

    /// <summary>Checks tightening against the expected order (e.g. a cross pattern 1,5,3,7,2,6,4,8 mapped to hole ids).</summary>
    public sealed class TorquePatternValidator
    {
        private readonly List<string> _order;

        public TorquePatternValidator(IEnumerable<string> expectedOrder)
        {
            if (expectedOrder == null) throw new ArgumentNullException(nameof(expectedOrder));
            _order = new List<string>(expectedOrder);
            if (_order.Count == 0) throw new ArgumentException("The pattern is empty.", nameof(expectedOrder));
            if (new HashSet<string>(_order, StringComparer.Ordinal).Count != _order.Count)
                throw new ArgumentException("The pattern lists a hole twice.", nameof(expectedOrder));
        }

        public IReadOnlyList<string> ExpectedOrder => _order;

        /// <summary>1-based position of the hole in the pattern (what Learn mode shows above the bolt); 0 if not in it.</summary>
        public int SequenceNumber(string holeId) => _order.IndexOf(holeId) + 1;

        /// <summary>The first hole in the pattern that is not done yet, or null when all are.</summary>
        public string NextExpected(Func<string, bool> isDone)
        {
            foreach (var h in _order) if (!isDone(h)) return h;
            return null;
        }

        /// <summary>Live check before torquing <paramref name="holeId"/>: OK if it is next (or already done), else WrongSequence.</summary>
        public PatternCheck Check(string holeId, Func<string, bool> isDone)
        {
            if (isDone(holeId)) return PatternCheck.Ok;
            return NextExpected(isDone) == holeId ? PatternCheck.Ok : PatternCheck.WrongSequence;
        }

        /// <summary>Whole-run check of the order in which holes were torqued.</summary>
        public PatternCheck Evaluate(IReadOnlyList<string> torqueOrder)
        {
            if (torqueOrder == null) throw new ArgumentNullException(nameof(torqueOrder));
            var seen = new HashSet<string>(torqueOrder, StringComparer.Ordinal);
            foreach (var h in _order) if (!seen.Contains(h)) return PatternCheck.Skipped;
            for (int i = 0; i < _order.Count; i++)
                if (i >= torqueOrder.Count || torqueOrder[i] != _order[i]) return PatternCheck.WrongSequence;
            return PatternCheck.Ok;
        }
    }
}
