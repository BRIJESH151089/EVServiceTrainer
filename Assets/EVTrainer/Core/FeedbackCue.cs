namespace EVTrainer.Core
{
    /// <summary>Every training event that gives sound and/or haptic feedback. One list for the whole trainer.</summary>
    public enum FeedbackCue
    {
        PickUp,
        Snap,
        Reject,
        CheckingFit,
        UiPress,
        BoltThreadTick,
        BoltSeated,
        WrenchAttach,
        RatchetBack,
        TorqueClick,
        OverTorque,
        ModuleComplete,
    }

    /// <summary>Mixer group a sound plays through.</summary>
    public enum SoundBus
    {
        Sfx,
        Ui,
        Ambience,
    }

    /// <summary>Where each cue's sound goes: workshop events at their source (3D), interface sounds in the head (2D).</summary>
    public static class FeedbackRouting
    {
        public static SoundBus BusFor(FeedbackCue cue) =>
            cue == FeedbackCue.UiPress || cue == FeedbackCue.ModuleComplete ? SoundBus.Ui : SoundBus.Sfx;

        /// <summary>True for sounds positioned at the part, bolt or wrench that made them.</summary>
        public static bool IsSpatial(FeedbackCue cue) => BusFor(cue) == SoundBus.Sfx;
    }
}
