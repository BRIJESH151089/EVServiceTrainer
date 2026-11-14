using EVTrainer.Core;

namespace EVTrainer.App
{
    /// <summary>
    /// Carries the mode of the next run across a scene reload (Retry Test, Practice), so the new run starts straight in
    /// that mode instead of showing the mode choice. Taken once.
    /// </summary>
    public static class RunLauncher
    {
        private static TrainingMode? s_NextRun;

        public static void StartNextRunIn(TrainingMode mode) => s_NextRun = mode;

        public static bool TryTakeNextRun(out TrainingMode mode)
        {
            mode = s_NextRun ?? TrainingMode.Learn;
            bool has = s_NextRun.HasValue;
            s_NextRun = null;
            return has;
        }
    }
}
