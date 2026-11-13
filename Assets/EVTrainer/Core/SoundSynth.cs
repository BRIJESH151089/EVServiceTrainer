using System;

namespace EVTrainer.Core
{
    /// <summary>
    /// The trainer's self-made sound set as raw mono samples (no audio assets): short tones, ticks and noise bursts
    /// built from a few envelopes, one recipe per <see cref="FeedbackCue"/>. Every sound is normalised to its own
    /// peak (always below <see cref="MaxPeak"/>, so it never clips) and fades in and out to exactly zero (no pops).
    /// Deterministic: the same cue always gives the same samples.
    /// </summary>
    public static class SoundSynth
    {
        public const int DefaultSampleRate = 44100;

        /// <summary>No sound is louder than this (headroom before the mixer).</summary>
        public const float MaxPeak = 0.9f;

        /// <summary>Peak of each cue: the loudest are the "stop" signals (torque click, over-torque).</summary>
        public static float TargetPeak(FeedbackCue cue) => cue switch
        {
            FeedbackCue.TorqueClick => 0.85f,
            FeedbackCue.OverTorque => 0.7f,
            FeedbackCue.Reject => 0.65f,
            FeedbackCue.Snap => 0.7f,
            FeedbackCue.ModuleComplete => 0.6f,
            FeedbackCue.WrenchAttach => 0.55f,
            FeedbackCue.BoltSeated => 0.55f,
            FeedbackCue.UiPress => 0.5f,
            FeedbackCue.PickUp => 0.45f,
            FeedbackCue.RatchetBack => 0.35f,
            FeedbackCue.CheckingFit => 0.3f,
            FeedbackCue.BoltThreadTick => 0.25f,
            _ => 0.5f,
        };

        public static float[] Generate(FeedbackCue cue, int sampleRate = DefaultSampleRate)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            var noise = new Random(17 + (int)cue);
            float[] s;
            switch (cue)
            {
                case FeedbackCue.PickUp: // soft rubber grip: low thump with a little scuff
                    s = Buffer(sampleRate, 0.09f);
                    AddTone(s, sampleRate, 0f, 180f, 0.6f, 45f);
                    AddTone(s, sampleRate, 0f, 360f, 0.25f, 70f);
                    AddNoise(s, sampleRate, noise, 0f, 0.05f, 0.35f, 70f, 0.15f);
                    break;
                case FeedbackCue.Snap: // metallic click-clack of a part seating
                    s = Buffer(sampleRate, 0.12f);
                    AddTone(s, sampleRate, 0f, 2400f, 0.55f, 180f);
                    AddTone(s, sampleRate, 0f, 160f, 0.45f, 60f);
                    AddTone(s, sampleRate, 0.035f, 3100f, 0.3f, 220f);
                    break;
                case FeedbackCue.Reject: // two low, beating buzzes
                    s = Buffer(sampleRate, 0.34f);
                    AddBuzz(s, sampleRate, 0f, 0.13f, 110f, 0.5f);
                    AddBuzz(s, sampleRate, 0f, 0.13f, 117f, 0.5f);
                    AddBuzz(s, sampleRate, 0.18f, 0.13f, 110f, 0.5f);
                    AddBuzz(s, sampleRate, 0.18f, 0.13f, 117f, 0.5f);
                    break;
                case FeedbackCue.CheckingFit: // two soft, neutral blips
                    s = Buffer(sampleRate, 0.2f);
                    AddTone(s, sampleRate, 0f, 1250f, 0.5f, 60f);
                    AddTone(s, sampleRate, 0.1f, 1250f, 0.5f, 60f);
                    break;
                case FeedbackCue.UiPress: // short plastic button click
                    s = Buffer(sampleRate, 0.045f);
                    AddTone(s, sampleRate, 0f, 3200f, 0.55f, 180f);
                    AddTone(s, sampleRate, 0f, 220f, 0.45f, 60f);
                    break;
                case FeedbackCue.BoltThreadTick: // tiny thread tick
                    s = Buffer(sampleRate, 0.025f);
                    AddTone(s, sampleRate, 0f, 5200f, 0.6f, 350f);
                    AddNoise(s, sampleRate, noise, 0f, 0.01f, 0.3f, 400f, 0.6f);
                    break;
                case FeedbackCue.BoltSeated: // snug "tock"
                    s = Buffer(sampleRate, 0.1f);
                    AddTone(s, sampleRate, 0f, 900f, 0.5f, 60f);
                    AddTone(s, sampleRate, 0f, 300f, 0.5f, 45f);
                    AddNoise(s, sampleRate, noise, 0f, 0.015f, 0.2f, 200f, 0.4f);
                    break;
                case FeedbackCue.WrenchAttach: // socket clunk over the bolt head
                    s = Buffer(sampleRate, 0.12f);
                    AddTone(s, sampleRate, 0f, 620f, 0.4f, 50f);
                    AddTone(s, sampleRate, 0f, 1850f, 0.3f, 90f);
                    AddNoise(s, sampleRate, noise, 0f, 0.04f, 0.4f, 80f, 0.3f);
                    break;
                case FeedbackCue.RatchetBack: // light pawl click
                    s = Buffer(sampleRate, 0.02f);
                    AddTone(s, sampleRate, 0f, 4200f, 0.55f, 180f);
                    AddTone(s, sampleRate, 0f, 260f, 0.45f, 60f);
                    break;
                case FeedbackCue.TorqueClick: // the wrench "breaks" at spec: sharp, solid click
                    s = Buffer(sampleRate, 0.07f);
                    AddTone(s, sampleRate, 0f, 1800f, 0.55f, 180f);
                    AddTone(s, sampleRate, 0f, 120f, 0.45f, 60f);
                    AddNoise(s, sampleRate, noise, 0f, 0.008f, 0.4f, 600f, 0.7f);
                    break;
                case FeedbackCue.OverTorque: // falling two-tone warning
                    s = Buffer(sampleRate, 0.42f);
                    AddBeep(s, sampleRate, 0f, 0.16f, 880f, 0.5f);
                    AddBeep(s, sampleRate, 0.22f, 0.18f, 660f, 0.5f);
                    break;
                case FeedbackCue.ModuleComplete: // rising four-note chime (C E G C)
                    s = Buffer(sampleRate, 1.3f);
                    AddBell(s, sampleRate, 0f, 523.25f, 0.35f);
                    AddBell(s, sampleRate, 0.11f, 659.25f, 0.35f);
                    AddBell(s, sampleRate, 0.22f, 783.99f, 0.35f);
                    AddBell(s, sampleRate, 0.33f, 1046.5f, 0.35f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cue));
            }
            Normalise(s, TargetPeak(cue));
            FadeEdges(s, sampleRate, 0.002f, 0.006f);
            return s;
        }

        /// <summary>
        /// Very quiet workshop room tone that loops seamlessly: low rumble, soft air and a faint 100 Hz hum.
        /// The end is cross-faded into the start, so the loop point is inaudible.
        /// </summary>
        public static float[] AmbienceLoop(int sampleRate = 22050, float seconds = 8f, float peak = 0.25f)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (seconds < 1f) throw new ArgumentOutOfRangeException(nameof(seconds));
            int length = (int)(sampleRate * seconds);
            int fade = sampleRate / 2;
            var raw = new float[length + fade];
            var random = new Random(5);
            double brown = 0, air = 0, hpIn = 0, hpOut = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                double white = random.NextDouble() * 2 - 1;
                brown = (brown + 0.02 * white) * 0.998;
                hpOut = brown - hpIn + 0.995 * hpOut; // remove the drift (DC) from the rumble
                hpIn = brown;
                air += 0.05 * (white - air);
                double t = (double)i / sampleRate;
                double hum = 0.04 * Math.Sin(2 * Math.PI * 100 * t) + 0.015 * Math.Sin(2 * Math.PI * 200 * t);
                raw[i] = (float)(3.0 * hpOut + 0.25 * air + hum);
            }
            var loop = new float[length];
            for (int i = 0; i < length; i++) loop[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                // Equal-power cross-fade: the overflow past the end blends into the start.
                double k = (double)i / fade;
                loop[i] = (float)(raw[i] * Math.Sin(k * Math.PI / 2) + raw[length + i] * Math.Cos(k * Math.PI / 2));
            }
            Normalise(loop, Math.Min(peak, MaxPeak));
            return loop;
        }

        private static float[] Buffer(int rate, float seconds) => new float[Math.Max(1, (int)(rate * seconds))];

        /// <summary>Sine with a 2 ms attack and exponential decay.</summary>
        private static void AddTone(float[] s, int rate, float start, float hz, float amp, float decay)
        {
            int first = (int)(start * rate);
            for (int i = first; i < s.Length; i++)
            {
                double t = (double)(i - first) / rate;
                double env = Math.Min(1.0, t / 0.002) * Math.Exp(-t * decay);
                s[i] += (float)(amp * env * Math.Sin(2 * Math.PI * hz * t));
            }
        }

        /// <summary>Low-passed noise burst (alpha = one-pole filter coefficient, 1 = unfiltered).</summary>
        private static void AddNoise(float[] s, int rate, Random random, float start, float duration, float amp, float decay, float alpha)
        {
            int first = (int)(start * rate);
            int last = Math.Min(s.Length, first + (int)(duration * rate));
            double y = 0;
            for (int i = first; i < last; i++)
            {
                double t = (double)(i - first) / rate;
                y += alpha * ((random.NextDouble() * 2 - 1) - y);
                s[i] += (float)(amp * y * Math.Exp(-t * decay));
            }
        }

        /// <summary>Soft square wave with flat level and short attack/release (an electric buzz).</summary>
        private static void AddBuzz(float[] s, int rate, float start, float duration, float hz, float amp)
        {
            int first = (int)(start * rate);
            int last = Math.Min(s.Length, first + (int)(duration * rate));
            for (int i = first; i < last; i++)
            {
                double t = (double)(i - first) / rate;
                double env = Math.Min(1.0, Math.Min(t / 0.008, (duration - t) / 0.012));
                s[i] += (float)(amp * Math.Max(0.0, env) * Math.Tanh(4 * Math.Sin(2 * Math.PI * hz * t)));
            }
        }

        /// <summary>Flat beep with a little third harmonic (a warning tone).</summary>
        private static void AddBeep(float[] s, int rate, float start, float duration, float hz, float amp)
        {
            int first = (int)(start * rate);
            int last = Math.Min(s.Length, first + (int)(duration * rate));
            for (int i = first; i < last; i++)
            {
                double t = (double)(i - first) / rate;
                double env = Math.Min(1.0, Math.Min(t / 0.006, (duration - t) / 0.02));
                double w = Math.Sin(2 * Math.PI * hz * t) + 0.25 * Math.Sin(2 * Math.PI * 3 * hz * t);
                s[i] += (float)(amp * Math.Max(0.0, env) * w);
            }
        }

        /// <summary>Bell-like note: fundamental with two harmonics, slow decay.</summary>
        private static void AddBell(float[] s, int rate, float start, float hz, float amp)
        {
            int first = (int)(start * rate);
            for (int i = first; i < s.Length; i++)
            {
                double t = (double)(i - first) / rate;
                double env = Math.Min(1.0, t / 0.004) * Math.Exp(-t * 3.2);
                double w = Math.Sin(2 * Math.PI * hz * t) + 0.35 * Math.Sin(4 * Math.PI * hz * t) + 0.12 * Math.Sin(6 * Math.PI * hz * t);
                s[i] += (float)(amp * env * w);
            }
        }

        private static void Normalise(float[] s, float peak)
        {
            float max = 0f;
            foreach (var v in s) max = Math.Max(max, Math.Abs(v));
            if (max <= 1e-9f) return;
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
        }

        /// <summary>Linear fade from exactly zero at the first sample and to exactly zero at the last.</summary>
        private static void FadeEdges(float[] s, int rate, float inSeconds, float outSeconds)
        {
            int fadeIn = Math.Min(s.Length, Math.Max(1, (int)(inSeconds * rate)));
            int fadeOut = Math.Min(s.Length, Math.Max(1, (int)(outSeconds * rate)));
            for (int i = 0; i < fadeIn; i++) s[i] *= (float)i / fadeIn;
            for (int i = 0; i < fadeOut; i++) s[s.Length - 1 - i] *= (float)i / fadeOut;
        }
    }
}
