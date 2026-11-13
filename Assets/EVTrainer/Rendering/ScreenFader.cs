using System;
using System.Collections;
using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Comfort fade on the headset camera: a black quad just in front of the eyes (overlay shader, depth test off),
    /// drawn only while it is not fully clear. A freshly loaded scene starts black and fades in, so a restart never
    /// jumps: fade out, reload, fade in (Core <see cref="ScreenFadeCurve"/>).
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        [Tooltip("Material using the EVTrainer/ScreenFade shader.")]
        [SerializeField] private Material _material;
        [SerializeField, Min(0f)] private float _seconds = ScreenFadeCurve.DefaultSeconds;
        [Tooltip("Start black and fade in when the scene loads.")]
        [SerializeField] private bool _fadeInOnStart = true;
        [Tooltip("Distance of the quad in front of the camera (beyond the near clip plane).")]
        [SerializeField, Min(0.02f)] private float _distance = 0.15f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float MaxStepSeconds = 1f / 30f;

        private MeshRenderer _renderer;
        private Material _instance;
        private Coroutine _fade;

        /// <summary>True while fading (input should wait).</summary>
        public bool IsFading => _fade != null;

        private void Awake()
        {
            var quad = new GameObject("Screen Fade Quad");
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = Vector3.forward * _distance;
            float half = _distance * 3f; // covers well over 140° in each direction
            var mesh = new Mesh { name = "ScreenFadeQuad" };
            mesh.vertices = new[] { new Vector3(-half, -half, 0f), new Vector3(-half, half, 0f), new Vector3(half, half, 0f), new Vector3(half, -half, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * half * 2f);
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            _renderer = quad.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _instance = new Material(_material);
            _renderer.sharedMaterial = _instance;
            SetAlpha(_fadeInOnStart ? 1f : 0f);
        }

        private void Start()
        {
            if (_fadeInOnStart) _fade = StartCoroutine(Run(toBlack: false, null));
        }

        private void OnDestroy()
        {
            if (_instance != null) Destroy(_instance);
        }

        /// <summary>Fades to black, then runs <paramref name="whenBlack"/> (e.g. reload the scene).</summary>
        public void FadeOutThen(Action whenBlack)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Run(toBlack: true, whenBlack));
        }

        private IEnumerator Run(bool toBlack, Action done)
        {
            // After a load the first frames are long (hitch): wait for them, and advance by at most a short step per
            // frame, so the fade is always seen as a fade and never swallowed by one slow frame.
            if (!toBlack) { yield return null; yield return null; }
            float t = 0f;
            while (true)
            {
                SetAlpha(ScreenFadeCurve.Alpha(t, _seconds, toBlack));
                if (ScreenFadeCurve.IsFinished(t, _seconds)) break;
                yield return null;
                t += Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds);
            }
            _fade = null;
            done?.Invoke();
        }

        private void SetAlpha(float alpha)
        {
            _renderer.enabled = alpha > 0.001f; // no overdraw once clear
            _instance.SetColor(BaseColorId, new Color(0f, 0f, 0f, alpha));
        }
    }
}
