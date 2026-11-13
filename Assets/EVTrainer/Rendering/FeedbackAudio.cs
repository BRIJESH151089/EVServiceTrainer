using System;
using EVTrainer.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Plays the self-made sound set (Core <see cref="SoundSynth"/>, generated once at start-up, no audio assets).
    /// Workshop sounds play in 3D at their source (part, bolt, wrench) from a small pool of voices on the SFX group;
    /// interface sounds play in 2D on the UI group; a quiet room-tone loop runs on the Ambience group.
    /// </summary>
    public sealed class FeedbackAudio : MonoBehaviour
    {
        [SerializeField] private AudioMixerGroup _sfxGroup;
        [SerializeField] private AudioMixerGroup _uiGroup;
        [SerializeField] private AudioMixerGroup _ambienceGroup;
        [Tooltip("3D voices for overlapping workshop sounds (oldest is reused).")]
        [SerializeField, Range(2, 16)] private int _voices = 6;
        [Tooltip("Full volume within this distance of the source.")]
        [SerializeField, Min(0.05f)] private float _minDistance = 0.35f;
        [SerializeField, Min(1f)] private float _maxDistance = 6f;
        [SerializeField] private bool _playAmbience = true;

        private static AudioClip[] s_Clips;
        private static AudioClip s_Ambience;

        private AudioSource[] _pool;
        private int _next;
        private AudioSource _ui;

        /// <summary>The clip for a cue (shared, created on first use). UI views use it for their own 2D sources.</summary>
        public static AudioClip Clip(FeedbackCue cue)
        {
            if (s_Clips == null)
            {
                var cues = (FeedbackCue[])Enum.GetValues(typeof(FeedbackCue));
                s_Clips = new AudioClip[cues.Length];
                foreach (var c in cues)
                {
                    var samples = SoundSynth.Generate(c);
                    var clip = AudioClip.Create(c.ToString(), samples.Length, 1, SoundSynth.DefaultSampleRate, false);
                    clip.SetData(samples, 0);
                    s_Clips[(int)c] = clip;
                }
            }
            return s_Clips[(int)cue];
        }

        private void Awake()
        {
            _pool = new AudioSource[_voices];
            for (int i = 0; i < _voices; i++)
            {
                var go = new GameObject("Voice " + i);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                Configure(source, _sfxGroup, spatial: true);
                _pool[i] = source;
            }
            _ui = gameObject.AddComponent<AudioSource>();
            Configure(_ui, _uiGroup, spatial: false);
            Clip(FeedbackCue.Snap); // build the set now, not in the middle of an interaction

            if (_playAmbience)
            {
                if (s_Ambience == null)
                {
                    const int rate = 22050;
                    var loop = SoundSynth.AmbienceLoop(rate);
                    s_Ambience = AudioClip.Create("WorkshopAmbience", loop.Length, 1, rate, false);
                    s_Ambience.SetData(loop, 0);
                }
                var ambience = gameObject.AddComponent<AudioSource>();
                Configure(ambience, _ambienceGroup, spatial: false);
                ambience.clip = s_Ambience;
                ambience.loop = true;
                ambience.priority = 200; // first to go if voices run out
                ambience.Play();
            }
        }

        private void Configure(AudioSource source, AudioMixerGroup group, bool spatial)
        {
            source.playOnAwake = false;
            source.outputAudioMixerGroup = group;
            source.spatialBlend = spatial ? 1f : 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = _minDistance;
            source.maxDistance = _maxDistance;
        }

        /// <summary>Plays a cue: at <paramref name="position"/> for workshop sounds, in the head for interface sounds.</summary>
        public void Play(FeedbackCue cue, Vector3 position, float volume = 1f)
        {
            var clip = Clip(cue);
            if (!FeedbackRouting.IsSpatial(cue))
            {
                if (_ui != null) _ui.PlayOneShot(clip, volume);
                return;
            }
            if (_pool == null || _pool.Length == 0) return;
            var voice = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            voice.transform.position = position;
            voice.PlayOneShot(clip, volume);
        }
    }
}
