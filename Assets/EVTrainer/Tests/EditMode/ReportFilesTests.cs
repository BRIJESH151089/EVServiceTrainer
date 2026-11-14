using System;
using System.Linq;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class ReportFilesTests
    {
        [Test]
        public void FileName_UsesTheLocalEndTime_AndSortsInTimeOrder()
        {
            var end = new DateTimeOffset(2026, 11, 15, 9, 4, 7, TimeSpan.FromHours(5.5));
            Assert.That(ReportFiles.FileName(end), Is.EqualTo("report_20261115_090407.json"));
            Assert.That(string.CompareOrdinal(ReportFiles.FileName(end.AddSeconds(1)), ReportFiles.FileName(end)), Is.GreaterThan(0));
        }

        [Test]
        public void NewestFirst_IgnoresOtherFiles()
        {
            var names = new[] { "report_20261115_090407.json", "notes.txt", "report_20261116_080000.json", "report_x.tmp" };
            Assert.That(ReportFiles.NewestFirst(names), Is.EqualTo(new[] { "report_20261116_080000.json", "report_20261115_090407.json" }));
            Assert.That(ReportFiles.NewestFirst(null), Is.Empty);
        }

        [Test]
        public void ToDelete_KeepsTheNewestTwenty()
        {
            var names = Enumerable.Range(1, 23).Select(i => $"report_202611{i:00}_120000.json").ToList();
            var delete = ReportFiles.ToDelete(names);

            Assert.That(delete, Is.EqualTo(new[] { "report_20261103_120000.json", "report_20261102_120000.json", "report_20261101_120000.json" }));
            Assert.That(ReportFiles.ToDelete(names.Take(20)), Is.Empty);
            Assert.That(ReportFiles.ToDelete(names, keep: 0).Count, Is.EqualTo(23));
        }
    }
}
