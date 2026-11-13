using System;
using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.UI
{
    /// <summary>
    /// A physical push button, poke only: the cap (with its label) follows the fingertip / controller tip inward along
    /// this transform's forward, clamped to <see cref="_travel"/>; the click fires at ~70 % travel, once (Core
    /// <see cref="PushButtonLogic"/>), with a click sound and a slightly darker cap until it has returned.
    /// Momentary buttons spring back. Latching buttons (a mode choice) stay pressed in with a highlight colour
    /// until <see cref="Unlatch"/>. Self-made poke-follow (XRI's is sample-only).
    /// </summary>
    public sealed class PushButtonView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static AudioClip s_Click;

        [Tooltip("Component implementing IPushButtonInput (XR poke adapter).")]
        [SerializeField] private MonoBehaviour _input;
        [Tooltip("Moves along this transform's forward; holds the cap mesh and its label.")]
        [SerializeField] private Transform _cap;
        [SerializeField] private Renderer _capRenderer;
        [SerializeField, Min(0.001f)] private float _travel = 0.009f;
        [SerializeField, Range(0.1f, 1f)] private float _clickAt = 0.7f;
        [SerializeField, Range(0f, 0.5f)] private float _rearmAt = 0.15f;
        [Tooltip("Stays pressed in after a click (selected state) until Unlatch. Off = momentary.")]
        [SerializeField] private bool _latching;
        [SerializeField] private Color _latchedColor = new Color(0.2f, 0.75f, 1f);
        [Tooltip("Seconds for the cap to spring back from full travel.")]
        [SerializeField, Min(0.01f)] private float _returnSeconds = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _pressedBrightness = 0.75f;
        [SerializeField, Range(0f, 1f)] private float _disabledBrightness = 0.4f;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;
        [Tooltip("Mixer group for the click (UI). The click is 2D, like all interface sounds.")]
        [SerializeField] private UnityEngine.Audio.AudioMixerGroup _outputGroup;

        private enum Look { Normal, Pressed, Latched, Disabled }

        private IPushButtonInput _source;
        private PushButtonLogic _logic;
        private Vector3 _capRest;
        private float _shown;          // displayed depth 0..1
        private MaterialPropertyBlock _block;
        private Color _baseColor;
        private Look _look = Look.Normal;
        private bool _interactable = true;
        private AudioSource _audio;

        /// <summary>Raised once per press.</summary>
        public event Action Clicked;

        public bool IsLatched => _logic != null && _logic.IsLatched;

        private void Awake()
        {
            _source = _input as IPushButtonInput;
            _logic = new PushButtonLogic(_clickAt, _rearmAt, _latching);
            if (_cap != null) _capRest = _cap.localPosition;
            _block = new MaterialPropertyBlock();
            if (_capRenderer != null && _capRenderer.sharedMaterial != null && _capRenderer.sharedMaterial.HasProperty(BaseColorId))
                _baseColor = _capRenderer.sharedMaterial.GetColor(BaseColorId);

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.outputAudioMixerGroup = _outputGroup;
            if (s_Click == null)
            {
                var samples = SoundSynth.Generate(FeedbackCue.UiPress);
                s_Click = AudioClip.Create("UiPress", samples.Length, 1, SoundSynth.DefaultSampleRate, false);
                s_Click.SetData(samples, 0);
            }
        }

        private void OnDisable()
        {
            // Hidden mid-press: come back at rest. A latched button keeps its selected state.
            if (_logic != null && !_logic.IsLatched)
            {
                _shown = 0f;
                _logic.Reset();
                if (_cap != null) _cap.localPosition = _capRest;
            }
            ApplyLook();
        }

        /// <summary>False: the button ignores pokes and is dimmed (e.g. the mode not chosen). A latched button keeps its look.</summary>
        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            ApplyLook();
        }

        /// <summary>Releases a latched button: it springs back out and can be pressed again.</summary>
        public void Unlatch()
        {
            if (_logic == null) return;
            _logic.Unlatch();
            ApplyLook();
        }

        private void Update()
        {
            float poke = _interactable && _source != null ? _source.PressDepth01 : 0f;
            float target = _logic.DisplayDepth(poke);
            // Follow the finger inward at once; spring back out over _returnSeconds.
            _shown = target >= _shown ? target : Mathf.MoveTowards(_shown, target, Time.deltaTime / _returnSeconds);
            if (_cap != null) _cap.localPosition = _capRest + Vector3.forward * (_travel * _shown);

            if (_logic.Update(_shown)) Fire();
            if (_look == Look.Pressed && _logic.IsArmed) ApplyLook();
        }

        private void Fire()
        {
            if (s_Click != null) _audio.PlayOneShot(s_Click, _volume);
            _source?.NotifyClicked();
            ApplyLook(justFired: true);
            Clicked?.Invoke();
        }

        private void ApplyLook(bool justFired = false)
        {
            var look = _logic != null && _logic.IsLatched ? Look.Latched
                : !_interactable ? Look.Disabled
                : justFired || (_logic != null && !_logic.IsArmed && _look == Look.Pressed) ? Look.Pressed
                : Look.Normal;
            _look = look;
            if (_capRenderer == null) return;
            if (look == Look.Normal)
            {
                _capRenderer.SetPropertyBlock(null);
                return;
            }
            Color c = look == Look.Latched ? _latchedColor
                : look == Look.Disabled ? _baseColor * _disabledBrightness
                : _baseColor * _pressedBrightness;
            c.a = _baseColor.a;
            _block.SetColor(BaseColorId, c);
            _capRenderer.SetPropertyBlock(_block);
        }
    }
}
