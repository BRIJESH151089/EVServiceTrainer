using System;

namespace EVTrainer.Core
{
    /// <summary>
    /// Magnetic snap assist: a released part (or bolt) within this distance and angle of its target is pulled into
    /// its exact pose. Symmetry still applies, so a part with a front turned 180° is never inside the zone.
    /// </summary>
    public readonly struct CaptureZone
    {
        public CaptureZone(float positionMetres, float angleDegrees)
        {
            if (positionMetres < 0f) throw new ArgumentOutOfRangeException(nameof(positionMetres));
            if (angleDegrees < 0f || angleDegrees > 180f) throw new ArgumentOutOfRangeException(nameof(angleDegrees));
            PositionMetres = positionMetres;
            AngleDegrees = angleDegrees;
        }

        public static CaptureZone None => default;

        public float PositionMetres { get; }
        public float AngleDegrees { get; }
    }

    /// <summary>Capture zones per training mode (module data). Learn is more forgiving than Test.</summary>
    public sealed class SnapAssist
    {
        public SnapAssist(CaptureZone learn, CaptureZone test)
        {
            Learn = learn;
            Test = test;
        }

        /// <summary>No assist: only each part's own tolerance counts.</summary>
        public static SnapAssist None { get; } = new SnapAssist(CaptureZone.None, CaptureZone.None);

        public CaptureZone Learn { get; }
        public CaptureZone Test { get; }

        public CaptureZone For(TrainingMode mode) => mode == TrainingMode.Learn ? Learn : Test;
    }
}
