using System;

namespace EVTrainer.Core
{
    public enum BoltState
    {
        /// <summary>Not in the hole, or in it but not yet turned.</summary>
        Loose,
        /// <summary>Thread engaged, being turned down by hand.</summary>
        Started,
        /// <summary>Head on the surface, finger-tight. Ready for the torque wrench.</summary>
        Seated,
        /// <summary>Torque inside the spec window.</summary>
        Torqued,
        /// <summary>Torque went above spec + tolerance. Stays so (the bolt would be replaced in real life).</summary>
        OverTorqued,
        /// <summary>Started at an angle: jammed until removed and reinserted.</summary>
        CrossThreaded,
    }

    /// <summary>
    /// One bolt being fastened. Rotations are clockwise-positive degrees (tightening); anticlockwise deltas are
    /// ignored (a ratchet: swinging back does nothing). Hand turns thread the bolt down until it is seated;
    /// wrench rotation then raises the torque by the stiffness per degree.
    /// </summary>
    public sealed class BoltTightening
    {
        /// <summary>Within this many turns (0.4°) of seating counts as seated.</summary>
        public const float SeatToleranceTurns = 0.001f;

        public BoltTightening(BoltSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public BoltSpec Spec { get; }
        public BoltState State { get; private set; } = BoltState.Loose;

        /// <summary>Thread turns done (0..ThreadTurnsToSeat).</summary>
        public float Turns { get; private set; }

        public float TorqueNm { get; private set; }

        /// <summary>0 = just engaged, 1 = seated (for the sinking visual).</summary>
        public float SeatProgress01 => Math.Min(1f, Turns / Spec.ThreadTurnsToSeat);

        /// <summary>At least seated (Seated, Torqued or OverTorqued).</summary>
        public bool IsSeated => State == BoltState.Seated || State == BoltState.Torqued || State == BoltState.OverTorqued;

        /// <summary>Done with the wrench (Torqued, or OverTorqued which is logged as a mistake).</summary>
        public bool IsFinished => State == BoltState.Torqued || State == BoltState.OverTorqued;

        /// <summary>Cross-thread check: the thread jams if the first twist starts with more tilt than allowed.</summary>
        public static bool IsCrossThreaded(float tiltDegrees, float maxStartTiltDegrees) => tiltDegrees > maxStartTiltDegrees;

        /// <summary>
        /// Turning by hand. The first clockwise twist checks the tilt (cross-thread check); afterwards each degree
        /// threads the bolt down until it is seated. A hand cannot torque: turns past seating are ignored.
        /// </summary>
        public BoltState HandTurn(float deltaDegrees, float tiltDegrees)
        {
            if (!IsTightening(deltaDegrees)) return State;
            if (State == BoltState.Loose)
            {
                if (IsCrossThreaded(tiltDegrees, Spec.MaxStartTiltDegrees))
                {
                    State = BoltState.CrossThreaded;
                    return State;
                }
                State = BoltState.Started;
            }
            if (State == BoltState.Started) Thread(deltaDegrees);
            return State;
        }

        /// <summary>
        /// Turning with a tool (clockwise only). Threads a started bolt down until seated, then torque rises by
        /// stiffness x angle: Torqued inside the window, OverTorqued above it. Jammed and over-torqued bolts do not move.
        /// </summary>
        public BoltState ApplyRotation(float deltaDegrees)
        {
            if (!IsTightening(deltaDegrees)) return State;
            if (State == BoltState.CrossThreaded || State == BoltState.OverTorqued) return State;
            if (State == BoltState.Loose) State = BoltState.Started;
            if (State == BoltState.Started)
            {
                deltaDegrees = Thread(deltaDegrees); // leftover after seating goes into torque
                if (deltaDegrees <= 0f) return State;
            }

            TorqueNm += Spec.StiffnessNmPerDegree * deltaDegrees;
            if (TorqueNm > Spec.MaxTorqueNm) State = BoltState.OverTorqued;
            else if (TorqueNm >= Spec.MinTorqueNm) State = BoltState.Torqued;
            return State;
        }

        /// <summary>Takes the bolt out of the hole: back to Loose, no turns, no torque (clears a cross-thread).</summary>
        public void Remove()
        {
            State = BoltState.Loose;
            Turns = 0f;
            TorqueNm = 0f;
        }

        private static bool IsTightening(float deltaDegrees) => !float.IsNaN(deltaDegrees) && deltaDegrees > 0f;

        /// <summary>Threads down by <paramref name="deltaDegrees"/>; returns the degrees left over after seating.</summary>
        private float Thread(float deltaDegrees)
        {
            float remainingTurns = Spec.ThreadTurnsToSeat - Turns;
            float turns = deltaDegrees / 360f;
            // Tolerance so float accumulation of many small twists never leaves a bolt a hair short of seated.
            if (turns < remainingTurns - SeatToleranceTurns)
            {
                Turns += turns;
                return 0f;
            }
            Turns = Spec.ThreadTurnsToSeat;
            State = BoltState.Seated;
            return Math.Max(0f, turns - remainingTurns) * 360f;
        }
    }
}
