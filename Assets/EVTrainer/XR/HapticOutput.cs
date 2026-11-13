using System.Collections.Generic;
using EVTrainer.Core;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>The one place that sends a Core <see cref="HapticPulse"/> to a controller (tracked hands have no haptics).</summary>
    public static class HapticOutput
    {
        // Torque ticks pulse many times a second: look each interactor's player up once (null = a hand, or none).
        private static readonly Dictionary<IXRInteractor, HapticImpulsePlayer> s_Players = new Dictionary<IXRInteractor, HapticImpulsePlayer>();

        // A restart reloads the scene with new interactors: forget the old ones.
        static HapticOutput() => UnityEngine.SceneManagement.SceneManager.sceneUnloaded += _ => s_Players.Clear();

        public static void Send(IXRInteractor interactor, HapticPulse pulse)
        {
            if (pulse.IsNone || interactor == null || interactor.transform == null) return;
            if (!s_Players.TryGetValue(interactor, out var player))
            {
                bool hand = interactor.transform.GetComponentInParent<HandTrackingAdapter>() != null;
                player = hand ? null : interactor.transform.GetComponentInParent<HapticImpulsePlayer>();
                s_Players[interactor] = player;
            }
            if (player != null) player.SendHapticImpulse(pulse.Amplitude, pulse.Seconds);
        }
    }
}
