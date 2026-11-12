using System;
using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.UI
{
    /// <summary>
    /// A physical push button: the cap (with its label) follows the fingertip inward along this transform's forward,
    /// clamped to <see cref="_travel"/>; the click fires at ~70 % travel, once (Core <see cref="PushButtonLogic"/>),
    /// with a click sound and a slightly darker cap until it has returned. On release the cap springs back.
    /// A distant (ray) click plays the same press animation. Self-made poke-follow (XRI's is sample-only).
    /// </summary>
    public sealed class PushButtonView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static AudioClip s_Click;

        [Tooltip("Component implementing IPushButtonInput (XR poke/ray adapter).")]
        [SerializeField] private MonoBehaviour _input;
        [Tooltip("Moves along this transform's forward; holds the cap mesh and its label.")]
        [SerializeField] private Transform _cap;
        [SerializeField] private Renderer _capRenderer;
        [SerializeField, Min(0.001f)] private float _travel = 0.009f;
        [SerializeField, Range(0.1f, 1f)] private float _clickAt = 0.7f;
        [SerializeField, Range(0f, 0.5f)] private float _rearmAt = 0.15f;
        [Tooltip("Seconds for the cap to spring back from full travel.")]
        [SerializeField, Min(0.01f)] private float _returnSeconds = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _pressedBrightness = 0.75f;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.6f;

        private IPushButtonInput _source;
        private PushButtonLogic _logic;
        private Vector3 _capRest;
        private float _shown;          // displayed depth 0..1
        private float _remoteT = -1f;  // ray-click animation time, -1 = idle
        private MaterialPropertyBlock _block;
        private Color _baseColor;
        private bool _tinted;
        private AudioSource _audio;

        /// <summary>Raised once per press.</summary>
        public event Action Clicked;

        private void Awake()
        {
            _source = _input as IPushButtonInput;
            _logic = new PushButtonLogic(_clickAt, _rearmAt);
            if (_cap != null) _capRest = _cap.localPosition;
            _block = new MaterialPropertyBlock();
            if (_capRenderer != null && _capRenderer.sharedMaterial != null && _capRenderer.sharedMaterial.HasProperty(BaseColorId))
                _baseColor = _capRenderer.sharedMaterial.GetColor(BaseColorId);

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.minDistance = 0.4f;
            if (s_Click == null)
            {
                const int rate = 44100;
                var samples = ProceduralClick.CreateSamples(rate, 0.04f, 3200f, 220f);
                s_Click = AudioClip.Create("ButtonClick", samples.Length, 1, rate, false);
                s_Click.SetData(samples, 0);
            }
        }

        private void OnDisable()
        {
            // Hidden mid-press (e.g. the panel closes on click): come back fresh next time.
            _shown = 0f;
            _remoteT = -1f;
            _remotePeaked = false;
            _logic?.Reset();
            if (_cap != null) _cap.localPosition = _capRest;
            SetTint(false);
        }

        private void Update()
        {
            float poke = _source != null ? _source.PressDepth01 : 0f;
            if (_source != null && _source.ConsumeRemoteClick() && _logic.IsArmed) _remoteT = 0f;

            float target = Mathf.Max(poke, RemoteDepth());
            // Follow the finger inward at once; spring back out over _returnSeconds.
            _shown = target >= _shown ? target : Mathf.MoveTowards(_shown, target, Time.deltaTime / _returnSeconds);
            if (_cap != null) _cap.localPosition = _capRest + Vector3.forward * (_travel * _shown);

            if (_logic.Update(_shown)) Fire();
            if (_tinted && _logic.IsArmed) SetTint(false);
        }

        /// <summary>
        /// Ray click animation: push in over 60 ms, hold 50 ms, then let go (spring-back handles return).
        /// The full-depth frame is guaranteed even if one long frame (a hitch) spans the whole animation,
        /// so a ray click can never be skipped.
        /// </summary>
        private float RemoteDepth()
        {
            if (_remoteT < 0f) return 0f;
            _remoteT += Time.deltaTime;
            if (_remoteT < 0.06f) return _remoteT / 0.06f;
            if (_remoteT < 0.11f || !_remotePeaked)
            {
                _remotePeaked = true;
                return 1f;
            }
            _remoteT = -1f;
            _remotePeaked = false;
            return 0f;
        }

        private bool _remotePeaked;

        private void Fire()
        {
            SetTint(true);
            if (s_Click != null) _audio.PlayOneShot(s_Click, _volume);
            _source?.NotifyClicked();
            Clicked?.Invoke();
        }

        private void SetTint(bool pressed)
        {
            _tinted = pressed;
            if (_capRenderer == null) return;
            if (!pressed)
            {
                _capRenderer.SetPropertyBlock(null);
                return;
            }
            var c = _baseColor * _pressedBrightness;
            c.a = _baseColor.a;
            _block.SetColor(BaseColorId, c);
            _capRenderer.SetPropertyBlock(_block);
        }
    }
}
