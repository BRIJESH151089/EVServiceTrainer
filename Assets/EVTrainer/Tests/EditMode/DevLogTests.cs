using System.Collections.Generic;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class DevLogTests
    {
        [Test]
        public void Info_GoesToTheSinkWithThePrefix_InTheEditor()
        {
            var lines = new List<string>();
            var previous = DevLog.Sink;
            DevLog.Sink = lines.Add;
            try
            {
                DevLog.Info("Run started");
            }
            finally
            {
                DevLog.Sink = previous;
            }
            Assert.That(lines, Is.EqualTo(new[] { "[EVTrainer] Run started" }));
        }

        [Test]
        public void Info_WithoutASink_DoesNothing()
        {
            var previous = DevLog.Sink;
            DevLog.Sink = null;
            try
            {
                Assert.DoesNotThrow(() => DevLog.Info("ignored"));
            }
            finally
            {
                DevLog.Sink = previous;
            }
        }

        [Test]
        public void Info_IsCompiledOutOfReleasePlayers()
        {
            var attributes = typeof(DevLog).GetMethod(nameof(DevLog.Info))
                .GetCustomAttributes(typeof(System.Diagnostics.ConditionalAttribute), false);
            var symbols = new List<string>();
            foreach (System.Diagnostics.ConditionalAttribute a in attributes) symbols.Add(a.ConditionString);
            Assert.That(symbols, Is.EquivalentTo(new[] { "DEVELOPMENT_BUILD", "UNITY_EDITOR" }));
        }
    }
}
