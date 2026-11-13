using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Learn guidance: a curved arrow round a bolt showing the clockwise swing of the wrench handle. It sweeps
    /// forward along its arc in a loop so the direction reads at a glance. Procedural mesh, opaque unlit material.
    /// </summary>
    public sealed class SwingArrowView : MonoBehaviour
    {
        [SerializeField] private Material _material;
        [SerializeField, Min(0.01f)] private float _radius = 0.09f;
        [SerializeField, Min(0.001f)] private float _width = 0.009f;
        [SerializeField, Range(20f, 300f)] private float _arcDegrees = 100f;
        [Tooltip("The arrow sweeps this far along its arc and back to the start, once per period.")]
        [SerializeField, Range(0f, 90f)] private float _sweepDegrees = 35f;
        [SerializeField, Min(0.2f)] private float _periodSeconds = 1.4f;
        [SerializeField, Min(0f)] private float _heightAboveSurface = 0.03f;
        [SerializeField] private Color _color = new Color(0.2f, 0.85f, 1f);

        private Transform _sweep;
        private IndicatorPart _arc, _head;
        private float _startTime;

        private void Awake() => Init();

        private void Init()
        {
            if (_sweep != null) return;
            _sweep = new GameObject("Sweep").transform;
            _sweep.SetParent(transform, false);
            _arc = new IndicatorPart(_sweep, "Arc", _material);
            _head = new IndicatorPart(_sweep, "Head", _material);
            // Starts a little ahead of the handle so it does not hide under it.
            ArcMesh.Build(_arc.Mesh, _radius - _width * 0.5f, _radius + _width * 0.5f, 10f, 10f + _arcDegrees);
            ArcMesh.BuildArrowHead(_head.Mesh, _radius, 10f + _arcDegrees, _width * 3f, _width * 1.8f);
            _arc.SetColor(_color);
            _head.SetColor(_color);
            Hide();
        }

        /// <param name="center">Bolt centre on the surface.</param>
        /// <param name="axis">Bolt axis.</param>
        /// <param name="handleDirection">Where the handle rests now; the arrow starts there and runs clockwise.</param>
        public void Show(Vector3 center, Vector3 axis, Vector3 handleDirection)
        {
            Init();
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                _startTime = Time.time;
            }
            var forward = Vector3.ProjectOnPlane(handleDirection, axis);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.ProjectOnPlane(Vector3.forward, axis);
            transform.SetPositionAndRotation(center + axis * _heightAboveSurface, Quaternion.LookRotation(forward.normalized, axis));
            float phase = Mathf.Repeat((Time.time - _startTime) / _periodSeconds, 1f);
            _sweep.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(0f, 1f, phase) * _sweepDegrees, 0f);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public bool IsShown => gameObject.activeSelf;
    }
}
