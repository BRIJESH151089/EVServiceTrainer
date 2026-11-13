using System;

namespace EVTrainer.Core
{
    /// <summary>
    /// Smooth yaw-only facing for labels: ignores small head movements (dead zone) and eases towards the target with
    /// an exponential time constant, so a label never jitters with tracking noise.
    /// </summary>
    public sealed class YawFollower
    {
        public YawFollower(float deadZoneDegrees = 4f, float timeConstantSeconds = 0.18f)
        {
            if (deadZoneDegrees < 0f) throw new ArgumentOutOfRangeException(nameof(deadZoneDegrees));
            if (timeConstantSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(timeConstantSeconds));
            DeadZoneDegrees = deadZoneDegrees;
            TimeConstantSeconds = timeConstantSeconds;
        }

        public float DeadZoneDegrees { get; }
        public float TimeConstantSeconds { get; }

        /// <summary>Current yaw (degrees, -180..180).</summary>
        public float Yaw { get; private set; }

        public bool HasYaw { get; private set; }

        private bool _moving;

        /// <summary>Jumps straight to <paramref name="yaw"/> (e.g. when a label appears).</summary>
        public void Snap(float yaw)
        {
            Yaw = AxisAngle.WrapDelta(yaw);
            HasYaw = true;
            _moving = false;
        }

        /// <summary>
        /// Moves towards <paramref name="targetYaw"/>. Starts turning only once the error exceeds the dead zone, then
        /// eases all the way in (so it does not stop just inside the zone and creep).
        /// </summary>
        public float Update(float targetYaw, float deltaSeconds)
        {
            if (!HasYaw)
            {
                Snap(targetYaw);
                return Yaw;
            }
            float error = AxisAngle.WrapDelta(targetYaw - Yaw);
            if (!_moving && Math.Abs(error) <= DeadZoneDegrees) return Yaw;
            _moving = true;
            float k = 1f - (float)Math.Exp(-Math.Max(0f, deltaSeconds) / TimeConstantSeconds);
            Yaw = AxisAngle.WrapDelta(Yaw + error * k);
            if (Math.Abs(AxisAngle.WrapDelta(targetYaw - Yaw)) < 0.25f) _moving = false;
            return Yaw;
        }
    }
}
