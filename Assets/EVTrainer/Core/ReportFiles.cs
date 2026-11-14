using System;
using System.Collections.Generic;
using System.Globalization;

namespace EVTrainer.Core
{
    /// <summary>
    /// Saved report file names: "report_yyyyMMdd_HHmmss.json" (local end time of the run), so names sort in time order.
    /// Only the newest <see cref="DefaultKeep"/> are kept.
    /// </summary>
    public static class ReportFiles
    {
        public const string Prefix = "report_";
        public const string Extension = ".json";
        public const int DefaultKeep = 20;

        public static string FileName(DateTimeOffset end) =>
            Prefix + end.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + Extension;

        /// <summary>True for names this trainer writes.</summary>
        public static bool IsReportFile(string name) =>
            name != null && name.StartsWith(Prefix, StringComparison.Ordinal) && name.EndsWith(Extension, StringComparison.Ordinal);

        /// <summary>Report file names, newest first (others ignored).</summary>
        public static List<string> NewestFirst(IEnumerable<string> names)
        {
            var list = new List<string>();
            if (names != null) foreach (var n in names) if (IsReportFile(n)) list.Add(n);
            list.Sort((a, b) => string.CompareOrdinal(b, a));
            return list;
        }

        /// <summary>The report files beyond the newest <paramref name="keep"/>, to delete.</summary>
        public static List<string> ToDelete(IEnumerable<string> names, int keep = DefaultKeep)
        {
            var newest = NewestFirst(names);
            if (keep < 0) keep = 0;
            return newest.Count <= keep ? new List<string>() : newest.GetRange(keep, newest.Count - keep);
        }
    }
}
