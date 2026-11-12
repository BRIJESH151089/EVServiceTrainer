namespace EVTrainer.Core
{
    /// <summary>
    /// Learn: ghost targets and hints, wrong actions blocked and explained.
    /// Test: no ghosts or hints, wrong actions logged and scored.
    /// </summary>
    public enum TrainingMode
    {
        Learn,
        Test,
    }
}
