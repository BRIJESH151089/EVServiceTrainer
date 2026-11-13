using System;
using System.Numerics;

namespace EVTrainer.Core
{
    /// <summary>Tuning for the One Euro filter (Casiez et al. 2012): low jitter at rest, low lag when moving fast.</summary>
    public readonly struct OneEuroSettings
    {
        public OneEuroSettings(float minCutoffHz, float beta, float derivativeCutoffHz = 1f)
        {
            if (minCutoffHz <= 0f) throw new ArgumentOutOfRangeException(nameof(minCutoffHz));
            if (beta < 0f) throw new ArgumentOutOfRangeException(nameof(beta));
            if (derivativeCutoffHz <= 0f) throw new ArgumentOutOfRangeException(nameof(derivativeCutoffHz));
            MinCutoffHz = minCutoffHz;
            Beta = beta;
            DerivativeCutoffHz = derivativeCutoffHz;
        }

        /// <summary>Cutoff at rest: lower = smoother when still, more lag on slow moves.</summary>
        public float MinCutoffHz { get; }

        /// <summary>How fast the cutoff rises with speed: higher = less lag on fast moves.</summary>
        public float Beta { get; }

        public float DerivativeCutoffHz { get; }

        /// <summary>Smoothing factor for one step of <paramref name="dt"/> seconds at <paramref name="cutoffHz"/>.</summary>
        public static float Alpha(float cutoffHz, float dt)
        {
            float tau = 1f / (2f * (float)Math.PI * cutoffHz);
            return 1f / (1f + tau / dt);
        }
    }

    /// <summary>
    /// One Euro filter on a pose (position + rotation). Position speed is in m/s, rotation speed in rad/s; the
    /// cutoff of each rises with its speed so slow tremor is removed but deliberate moves keep up.
    /// </summary>
    public sealed class OneEuroPoseFilter
    {
        private readonly OneEuroSettings _position;
        private readonly OneEuroSettings _rotation;
        private bool _has;
        private Vector3 _p, _dp;
        private Quaternion _q;
        private float _dq;

        public OneEuroPoseFilter(OneEuroSettings position, OneEuroSettings rotation)
        {
            _position = position;
            _rotation = rotation;
        }

        public Vector3 Position => _p;
        public Quaternion Rotation => _q;

        /// <summary>Forget the history (e.g. tracking lost); the next sample passes straight through.</summary>
        public void Reset() => _has = false;

        public void Update(Vector3 position, Quaternion rotation, float dt)
        {
            rotation = Quaternion.Normalize(rotation);
            if (!_has || dt <= 0f || float.IsNaN(dt))
            {
                _p = position;
                _q = rotation;
                _dp = Vector3.Zero;
                _dq = 0f;
                _has = true;
                return;
            }

            // Position: filtered speed sets the cutoff.
            var rawSpeed = (position - _p) / dt;
            _dp = Vector3.Lerp(_dp, rawSpeed, OneEuroSettings.Alpha(_position.DerivativeCutoffHz, dt));
            float cutoff = _position.MinCutoffHz + _position.Beta * _dp.Length();
            _p = Vector3.Lerp(_p, position, OneEuroSettings.Alpha(cutoff, dt));

            // Rotation: same on the angular speed, then slerp towards the sample.
            if (Quaternion.Dot(_q, rotation) < 0f) rotation = Quaternion.Negate(rotation); // shortest way
            float angle = SnapEvaluator.AngleBetweenDegrees(_q, rotation) * (float)(Math.PI / 180.0);
            float rawAngular = angle / dt;
            _dq += (rawAngular - _dq) * OneEuroSettings.Alpha(_rotation.DerivativeCutoffHz, dt);
            float rotCutoff = _rotation.MinCutoffHz + _rotation.Beta * _dq;
            _q = Quaternion.Normalize(Quaternion.Slerp(_q, rotation, OneEuroSettings.Alpha(rotCutoff, dt)));
        }
    }

    /// <summary>
    /// Hand grab / release debounce: a grab starts at once, but it only ends after the hand has stayed open
    /// (below the release threshold) for <see cref="HoldSeconds"/>, so a flickering pinch does not drop and re-grab.
    /// </summary>
    public sealed class ReleaseDebounce
    {
        private double _openSince = double.NaN;

        public ReleaseDebounce(float holdSeconds = 0.1f)
        {
            if (holdSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(holdSeconds));
            HoldSeconds = holdSeconds;
        }

        public float HoldSeconds { get; }
        public bool IsPressed { get; private set; }

        /// <param name="rawPressed">The thresholded (hysteresis) grab state this frame.</param>
        /// <param name="timeSeconds">Current time.</param>
        /// <returns>The debounced grab state.</returns>
        public bool Update(bool rawPressed, double timeSeconds)
        {
            if (rawPressed)
            {
                IsPressed = true;
                _openSince = double.NaN;
                return true;
            }
            if (!IsPressed) return false;
            if (double.IsNaN(_openSince)) _openSince = timeSeconds;
            if (timeSeconds - _openSince >= HoldSeconds - 1e-6) IsPressed = false;
            return IsPressed;
        }

        /// <summary>Immediate release (e.g. tracking lost).</summary>
        public void Reset()
        {
            IsPressed = false;
            _openSince = double.NaN;
        }
    }

    /// <summary>
    /// Two-hand carry of a large part (e.g. the pack cover): position follows the midpoint of the hands, yaw follows
    /// the line between them, and the part is kept level (no pitch or roll from hand noise).
    /// </summary>
    public sealed class TwoHandCarry
    {
        private Vector3 _localOffset;  // part position relative to the hands' midpoint, in the hands' yaw frame
        private float _yawOffset;      // part yaw minus hands' yaw at the grab

        /// <summary>Starts a carry from the current part pose and hand positions.</summary>
        public void Begin(Vector3 partPosition, float partYawDegrees, Vector3 leftHand, Vector3 rightHand)
        {
            var mid = (leftHand + rightHand) * 0.5f;
            float handsYaw = HandsYaw(leftHand, rightHand);
            _localOffset = Vector3.Transform(partPosition - mid, YawRotation(-handsYaw));
            _yawOffset = AxisAngle.WrapDelta(partYawDegrees - handsYaw);
        }

        /// <summary>Level pose of the part for the current hand positions.</summary>
        public void Pose(Vector3 leftHand, Vector3 rightHand, out Vector3 position, out float yawDegrees)
        {
            var mid = (leftHand + rightHand) * 0.5f;
            float handsYaw = HandsYaw(leftHand, rightHand);
            position = mid + Vector3.Transform(_localOffset, YawRotation(handsYaw));
            yawDegrees = AxisAngle.WrapDelta(handsYaw + _yawOffset);
        }

        /// <summary>Yaw (degrees, clockwise from +Z seen from above) of the horizontal line from left to right hand.</summary>
        public static float HandsYaw(Vector3 leftHand, Vector3 rightHand)
        {
            var d = rightHand - leftHand;
            // The hands' "forward" is perpendicular to the left→right line: rotate it -90° about up.
            return AxisAngle.WrapDelta((float)(Math.Atan2(d.X, d.Z) * 180.0 / Math.PI) - 90f);
        }

        /// <summary>Rotation by <paramref name="yawDegrees"/> about +Y, clockwise seen from above (Unity's convention).</summary>
        public static Quaternion YawRotation(float yawDegrees) =>
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawDegrees * (float)(Math.PI / 180.0));
    }
}
