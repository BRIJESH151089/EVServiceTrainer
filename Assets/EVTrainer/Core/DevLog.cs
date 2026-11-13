using System;
using System.Diagnostics;

namespace EVTrainer.Core
{
    /// <summary>
    /// "[EVTrainer]" information logs for development only. Calls are compiled out of release players
    /// (<see cref="ConditionalAttribute"/>: only Editor and Development builds keep them), including the building of
    /// their message strings. Warnings and errors do not go through here; they always use the engine log.
    /// The composition root connects <see cref="Sink"/> to the engine log (Core has no engine).
    /// </summary>
    public static class DevLog
    {
        public const string Prefix = "[EVTrainer] ";

        public static Action<string> Sink { get; set; }

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void Info(string message) => Sink?.Invoke(Prefix + message);
    }
}
