using System;
using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// Authoring data for the controller haptic profiles (shared by every module). Converted into Core
    /// <see cref="HapticProfiles"/>, which also maps each training event onto a profile.
    /// </summary>
    [CreateAssetMenu(menuName = "EV Trainer/Haptic Profiles", fileName = "HapticProfiles")]
    public sealed class HapticProfileAsset : ScriptableObject
    {
        [Serializable]
        public struct Pulse
        {
            [Range(0f, 1f)] public float amplitude;
            [Range(0f, 2f)] public float seconds;

            public Pulse(HapticPulse p)
            {
                amplitude = p.Amplitude;
                seconds = p.Seconds;
            }

            public HapticPulse ToCore() => new HapticPulse(amplitude, seconds);
        }

        [Tooltip("Thread ticks, ratchet back-swing, button presses.")]
        [SerializeField] private Pulse _lightTick = new Pulse(HapticProfiles.Default.Get(HapticKind.LightTick));
        [SerializeField] private Pulse _pickUp = new Pulse(HapticProfiles.Default.Get(HapticKind.PickUp));
        [Tooltip("A part snaps in, a bolt seats, the wrench goes on.")]
        [SerializeField] private Pulse _snap = new Pulse(HapticProfiles.Default.Get(HapticKind.Snap));
        [Tooltip("Wrong order, jammed bolt, wrench refused.")]
        [SerializeField] private Pulse _reject = new Pulse(HapticProfiles.Default.Get(HapticKind.Reject));
        [SerializeField] private Pulse _torqueClick = new Pulse(HapticProfiles.Default.Get(HapticKind.TorqueClick));
        [Tooltip("Test mode: the wrench stays on after the click, so the click lasts this long.")]
        [SerializeField, Range(0f, 2f)] private float _torqueClickHoldSeconds = HapticProfiles.Default.TorqueClickHoldSeconds;
        [SerializeField] private Pulse _overTorque = new Pulse(HapticProfiles.Default.Get(HapticKind.OverTorque));
        [Header("Torque ramp (ticks while swinging, growing towards the spec)")]
        [SerializeField, Range(0f, 1f)] private float _rampMinAmplitude = HapticProfiles.Default.RampMinAmplitude;
        [SerializeField, Range(0f, 1f)] private float _rampMaxAmplitude = HapticProfiles.Default.RampMaxAmplitude;
        [SerializeField, Range(0.005f, 0.5f)] private float _rampSeconds = HapticProfiles.Default.RampSeconds;

        public HapticProfiles ToCore() => new HapticProfiles(
            _lightTick.ToCore(), _pickUp.ToCore(), _snap.ToCore(), _reject.ToCore(), _torqueClick.ToCore(), _overTorque.ToCore(),
            _rampMinAmplitude, _rampMaxAmplitude, _rampSeconds, _torqueClickHoldSeconds);
    }
}
