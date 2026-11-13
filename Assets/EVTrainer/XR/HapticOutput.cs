using EVTrainer.Core;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>The one place that sends a Core <see cref="HapticPulse"/> to a controller (tracked hands have no haptics).</summary>
    public static class HapticOutput
    {
        public static void Send(IXRInteractor interactor, HapticPulse pulse)
        {
            if (pulse.IsNone || interactor == null || interactor.transform == null) return;
            if (interactor.transform.GetComponentInParent<HandTrackingAdapter>() != null) return;
            var player = interactor.transform.GetComponentInParent<HapticImpulsePlayer>();
            if (player != null) player.SendHapticImpulse(pulse.Amplitude, pulse.Seconds);
        }
    }
}
