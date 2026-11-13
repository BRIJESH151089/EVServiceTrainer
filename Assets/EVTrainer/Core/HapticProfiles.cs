using System;

namespace EVTrainer.Core
{
    /// <summary>Kinds of controller vibration. Every haptic pulse in the trainer is one of these.</summary>
    public enum HapticKind
    {
        None,
        LightTick,
        PickUp,
        Snap,
        Reject,
        TorqueRamp,
        TorqueClick,
        OverTorque,
    }

    /// <summary>One controller vibration: strength 0..1 and length in seconds.</summary>
    public readonly struct HapticPulse : IEquatable<HapticPulse>
    {
        public HapticPulse(float amplitude, float seconds)
        {
            if (float.IsNaN(amplitude) || amplitude < 0f || amplitude > 1f) throw new ArgumentOutOfRangeException(nameof(amplitude));
            if (float.IsNaN(seconds) || seconds < 0f || seconds > 2f) throw new ArgumentOutOfRangeException(nameof(seconds));
            Amplitude = amplitude;
            Seconds = seconds;
        }

        public float Amplitude { get; }
        public float Seconds { get; }
        public bool IsNone => Amplitude <= 0f || Seconds <= 0f;

        public static HapticPulse None => default;

        public bool Equals(HapticPulse other) => Amplitude.Equals(other.Amplitude) && Seconds.Equals(other.Seconds);
        public override bool Equals(object obj) => obj is HapticPulse p && Equals(p);
        public override int GetHashCode() => Amplitude.GetHashCode() * 397 ^ Seconds.GetHashCode();
        public override string ToString() => $"{Amplitude:0.##} for {Seconds * 1000f:0} ms";
    }

    /// <summary>
    /// The haptic profiles (module-independent data) and the rules that map training events onto them, so the same
    /// event always feels the same on every controller: light ticks for small mechanical steps, a firm pulse when
    /// something locks in, a long strong buzz for "no", and a torque ramp that grows towards the spec before the click.
    /// </summary>
    public sealed class HapticProfiles
    {
        private readonly HapticPulse[] _pulses;

        public HapticProfiles(HapticPulse lightTick, HapticPulse pickUp, HapticPulse snap, HapticPulse reject,
            HapticPulse torqueClick, HapticPulse overTorque,
            float rampMinAmplitude, float rampMaxAmplitude, float rampSeconds, float torqueClickHoldSeconds)
        {
            if (float.IsNaN(rampMinAmplitude) || rampMinAmplitude < 0f || rampMinAmplitude > 1f) throw new ArgumentOutOfRangeException(nameof(rampMinAmplitude));
            if (float.IsNaN(rampMaxAmplitude) || rampMaxAmplitude < rampMinAmplitude || rampMaxAmplitude > 1f) throw new ArgumentOutOfRangeException(nameof(rampMaxAmplitude));
            if (float.IsNaN(rampSeconds) || rampSeconds <= 0f || rampSeconds > 0.5f) throw new ArgumentOutOfRangeException(nameof(rampSeconds));
            if (float.IsNaN(torqueClickHoldSeconds) || torqueClickHoldSeconds < torqueClick.Seconds || torqueClickHoldSeconds > 2f)
                throw new ArgumentOutOfRangeException(nameof(torqueClickHoldSeconds));

            _pulses = new HapticPulse[Enum.GetValues(typeof(HapticKind)).Length];
            _pulses[(int)HapticKind.LightTick] = lightTick;
            _pulses[(int)HapticKind.PickUp] = pickUp;
            _pulses[(int)HapticKind.Snap] = snap;
            _pulses[(int)HapticKind.Reject] = reject;
            _pulses[(int)HapticKind.TorqueClick] = torqueClick;
            _pulses[(int)HapticKind.OverTorque] = overTorque;
            _pulses[(int)HapticKind.TorqueRamp] = new HapticPulse(rampMaxAmplitude, rampSeconds);
            RampMinAmplitude = rampMinAmplitude;
            RampMaxAmplitude = rampMaxAmplitude;
            RampSeconds = rampSeconds;
            TorqueClickHoldSeconds = torqueClickHoldSeconds;
        }

        /// <summary>Defaults tuned on Quest controllers.</summary>
        public static HapticProfiles Default { get; } = new HapticProfiles(
            lightTick: new HapticPulse(0.2f, 0.02f),
            pickUp: new HapticPulse(0.25f, 0.03f),
            snap: new HapticPulse(0.6f, 0.08f),
            reject: new HapticPulse(1f, 0.25f),
            torqueClick: new HapticPulse(1f, 0.12f),
            overTorque: new HapticPulse(1f, 0.4f),
            rampMinAmplitude: 0.05f, rampMaxAmplitude: 0.45f, rampSeconds: 0.02f, torqueClickHoldSeconds: 0.3f);

        public float RampMinAmplitude { get; }
        public float RampMaxAmplitude { get; }
        public float RampSeconds { get; }

        /// <summary>Test mode: the wrench stays on after the click, so the click lasts this long ("stop and lift off").</summary>
        public float TorqueClickHoldSeconds { get; }

        /// <summary>The profile of a kind (<see cref="HapticKind.TorqueRamp"/> gives the ramp at spec).</summary>
        public HapticPulse Get(HapticKind kind) => kind == HapticKind.None ? HapticPulse.None : _pulses[(int)kind];

        /// <summary>The pulse for a training event.</summary>
        public HapticPulse ForCue(FeedbackCue cue) => Get(KindFor(cue));

        /// <summary>Which profile each training event uses (one place, so every event feels the same everywhere).</summary>
        public static HapticKind KindFor(FeedbackCue cue) => cue switch
        {
            FeedbackCue.PickUp => HapticKind.PickUp,
            FeedbackCue.Snap => HapticKind.Snap,
            FeedbackCue.BoltSeated => HapticKind.Snap,
            FeedbackCue.WrenchAttach => HapticKind.Snap,
            FeedbackCue.Reject => HapticKind.Reject,
            FeedbackCue.UiPress => HapticKind.LightTick,
            FeedbackCue.BoltThreadTick => HapticKind.LightTick,
            FeedbackCue.RatchetBack => HapticKind.LightTick,
            FeedbackCue.TorqueClick => HapticKind.TorqueClick,
            FeedbackCue.OverTorque => HapticKind.OverTorque,
            _ => HapticKind.None, // checking fit and the completion chime are heard, not felt
        };

        /// <summary>
        /// Torque ramp tick while swinging the wrench: from the minimum strength at zero torque, growing with the
        /// square of torque/spec to the maximum at the spec (and no further).
        /// </summary>
        public HapticPulse TorqueRamp(float torqueNm, float specNm)
        {
            float k = specNm > 0f && !float.IsNaN(torqueNm) ? Math.Max(0f, Math.Min(1f, torqueNm / specNm)) : 0f;
            return new HapticPulse(RampMinAmplitude + (RampMaxAmplitude - RampMinAmplitude) * k * k, RampSeconds);
        }

        /// <summary>The click at spec; longer when the wrench stays on the bolt (Test) as a "stop and lift off" signal.</summary>
        public HapticPulse TorqueClick(bool wrenchStaysOn)
        {
            var click = Get(HapticKind.TorqueClick);
            return wrenchStaysOn ? new HapticPulse(click.Amplitude, TorqueClickHoldSeconds) : click;
        }
    }
}
