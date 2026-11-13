using System;
using System.Numerics;

namespace EVTrainer.Core
{
    /// <summary>How the torque wrench attaches to and comes off a bolt head (module data).</summary>
    public sealed class WrenchSpec
    {
        public WrenchSpec(
            float attachRadiusMetres = 0.03f,
            float attachMaxAboveMetres = 0.04f,
            float attachMaxBelowMetres = 0.03f,
            float attachMaxTiltDegrees = 45f,
            float detachOffPlaneMetres = 0.15f,
            float ratchetClickDegrees = 6f,
            float minHandRadiusMetres = 0.04f)
        {
            if (attachRadiusMetres <= 0f) throw new ArgumentOutOfRangeException(nameof(attachRadiusMetres));
            if (attachMaxAboveMetres < 0f) throw new ArgumentOutOfRangeException(nameof(attachMaxAboveMetres));
            if (attachMaxBelowMetres < 0f) throw new ArgumentOutOfRangeException(nameof(attachMaxBelowMetres));
            if (attachMaxTiltDegrees < 0f || attachMaxTiltDegrees > 90f) throw new ArgumentOutOfRangeException(nameof(attachMaxTiltDegrees));
            if (detachOffPlaneMetres <= 0f) throw new ArgumentOutOfRangeException(nameof(detachOffPlaneMetres));
            if (ratchetClickDegrees <= 0f) throw new ArgumentOutOfRangeException(nameof(ratchetClickDegrees));
            if (minHandRadiusMetres < 0f) throw new ArgumentOutOfRangeException(nameof(minHandRadiusMetres));

            AttachRadiusMetres = attachRadiusMetres;
            AttachMaxAboveMetres = attachMaxAboveMetres;
            AttachMaxBelowMetres = attachMaxBelowMetres;
            AttachMaxTiltDegrees = attachMaxTiltDegrees;
            DetachOffPlaneMetres = detachOffPlaneMetres;
            RatchetClickDegrees = ratchetClickDegrees;
            MinHandRadiusMetres = minHandRadiusMetres;
        }

        public static WrenchSpec Default { get; } = new WrenchSpec();

        /// <summary>The socket mouth attaches within this distance of the bolt axis...</summary>
        public float AttachRadiusMetres { get; }

        /// <summary>...from this far above the top of the head...</summary>
        public float AttachMaxAboveMetres { get; }

        /// <summary>...to this far below it (the socket pushed over the head)...</summary>
        public float AttachMaxBelowMetres { get; }

        /// <summary>...with the socket axis within this angle of the bolt axis.</summary>
        public float AttachMaxTiltDegrees { get; }

        /// <summary>Attached: the holding hand moving this far off the swing plane takes the wrench off.</summary>
        public float DetachOffPlaneMetres { get; }

        /// <summary>A soft ratchet click every this many degrees of back-swing.</summary>
        public float RatchetClickDegrees { get; }

        /// <summary>Closer to the bolt axis than this, the hand gives no usable handle angle (ignored).</summary>
        public float MinHandRadiusMetres { get; }

        /// <summary>
        /// Is the socket on the bolt head? <paramref name="socketMouth"/> is the open end of the socket,
        /// <paramref name="headTop"/> the top centre of the bolt head; axes point out of the hole / up the socket.
        /// </summary>
        public bool IsOnHead(Vector3 socketMouth, Vector3 socketAxis, Vector3 headTop, Vector3 boltAxis)
        {
            if (boltAxis.LengthSquared() < 1e-12f) return false;
            boltAxis = Vector3.Normalize(boltAxis);
            var rel = socketMouth - headTop;
            float height = Vector3.Dot(rel, boltAxis);
            if (height > AttachMaxAboveMetres || height < -AttachMaxBelowMetres) return false;
            float radial = (rel - boltAxis * height).Length();
            if (radial > AttachRadiusMetres) return false;
            return AxisAngle.AngleBetween(socketAxis, boltAxis) <= AttachMaxTiltDegrees;
        }

        /// <summary>Distance of the hand from the swing plane through the head (for the detach rule).</summary>
        public static float OffPlane(Vector3 hand, Vector3 headTop, Vector3 boltAxis)
        {
            if (boltAxis.LengthSquared() < 1e-12f) return 0f;
            return Math.Abs(Vector3.Dot(hand - headTop, Vector3.Normalize(boltAxis)));
        }

        public bool ShouldDetach(Vector3 hand, Vector3 headTop, Vector3 boltAxis) =>
            OffPlane(hand, headTop, boltAxis) > DetachOffPlaneMetres;
    }

    /// <summary>Result of one frame of handle swing.</summary>
    public readonly struct RatchetStep
    {
        public RatchetStep(float handleAngleDegrees, float driveDegrees, int backClicks, bool hasAngle)
        {
            HandleAngleDegrees = handleAngleDegrees;
            DriveDegrees = driveDegrees;
            BackClicks = backClicks;
            HasAngle = hasAngle;
        }

        /// <summary>Handle angle about the bolt axis (degrees, -180..180, clockwise-positive), pointing at the hand.</summary>
        public float HandleAngleDegrees { get; }

        /// <summary>Clockwise degrees to turn the bolt this frame (0 on a back-swing).</summary>
        public float DriveDegrees { get; }

        /// <summary>Soft ratchet clicks to play this frame (back-swing).</summary>
        public int BackClicks { get; }

        /// <summary>False when the hand is too close to the axis to give an angle (the handle keeps its last angle).</summary>
        public bool HasAngle { get; }
    }

    /// <summary>
    /// A ratchet handle swung about a bolt axis. The handle angle is the hand's position projected onto the plane
    /// perpendicular to the axis (atan2); a clockwise swing drives the bolt, a back-swing only moves the handle back
    /// (with a soft click every <see cref="WrenchSpec.RatchetClickDegrees"/>).
    /// </summary>
    public sealed class RatchetSwing
    {
        private readonly WrenchSpec _spec;
        private readonly AxisRotationTracker _tracker = new AxisRotationTracker();
        private float _backAccumulator;
        private float _handleAngle;

        public RatchetSwing(WrenchSpec spec = null) => _spec = spec ?? WrenchSpec.Default;

        public float HandleAngleDegrees => _handleAngle;

        /// <summary>Starts a new swing (on attach): the first update only sets the handle angle.</summary>
        public void Reset()
        {
            _tracker.Reset();
            _backAccumulator = 0f;
        }

        /// <param name="axis">Bolt axis (out of the hole).</param>
        /// <param name="handOffset">Hand position minus the bolt head.</param>
        public RatchetStep Update(Vector3 axis, Vector3 handOffset)
        {
            if (axis.LengthSquared() < 1e-12f) return new RatchetStep(_handleAngle, 0f, 0, false);
            var n = Vector3.Normalize(axis);
            var inPlane = handOffset - n * Vector3.Dot(handOffset, n);
            if (inPlane.Length() < _spec.MinHandRadiusMetres || !AxisAngle.TryAngleAbout(n, inPlane, out float angle))
            {
                _tracker.Reset(); // resume cleanly once the hand is usable again
                return new RatchetStep(_handleAngle, 0f, 0, false);
            }

            float delta = _tracker.Update(n, inPlane);
            _handleAngle = angle;
            if (delta >= 0f)
            {
                _backAccumulator = 0f;
                return new RatchetStep(angle, delta, 0, true);
            }

            _backAccumulator += -delta;
            int clicks = (int)(_backAccumulator / _spec.RatchetClickDegrees);
            _backAccumulator -= clicks * _spec.RatchetClickDegrees;
            return new RatchetStep(angle, 0f, clicks, true);
        }
    }
}
