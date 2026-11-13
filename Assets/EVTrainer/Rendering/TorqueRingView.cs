using UnityEngine;

namespace EVTrainer.Rendering
{
    /// <summary>
    /// Progress ring flat round a bolt while it is torqued: a dark track, the green target zone (spec ± tolerance)
    /// and a fill that grows clockwise with torque. Done: the ring turns green and a check mark appears above the bolt;
    /// over-torqued: red. Built from procedural meshes on one opaque unlit material.
    /// </summary>
    public sealed class TorqueRingView : MonoBehaviour
    {
        public enum Look { Rising, Done, Over }

        [SerializeField] private Material _material;
        [SerializeField, Min(0.001f)] private float _innerRadius = 0.022f;
        [SerializeField, Min(0.002f)] private float _outerRadius = 0.031f;
        [Tooltip("The zone band sticks out this much further than the ring.")]
        [SerializeField, Min(0f)] private float _zoneOverhang = 0.004f;
        [SerializeField] private Color _trackColor = new Color(0.12f, 0.13f, 0.15f);
        [SerializeField] private Color _zoneColor = new Color(0.1f, 0.55f, 0.2f);
        [SerializeField] private Color _risingColor = new Color(1f, 0.78f, 0.15f);
        [SerializeField] private Color _doneColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private Color _overColor = new Color(1f, 0.2f, 0.12f);
        [SerializeField, Min(0f)] private float _checkHeight = 0.06f;

        private IndicatorPart _track, _zone, _fill, _checkShort, _checkLong;
        private float _shownFill = -1f, _shownZoneStart = -1f, _shownZoneEnd = -1f;
        private Transform _check;
        private Transform _viewer;

        private void Awake() => Init();

        private void Init()
        {
            if (_track != null) return;
            _track = new IndicatorPart(transform, "Track", _material);
            _zone = new IndicatorPart(transform, "Zone", _material);
            _fill = new IndicatorPart(transform, "Fill", _material);
            _zone.Transform.localPosition = new Vector3(0f, 0.0005f, 0f);
            _fill.Transform.localPosition = new Vector3(0f, 0.001f, 0f);
            ArcMesh.Build(_track.Mesh, _innerRadius, _outerRadius, 0f, 360f);
            _track.SetColor(_trackColor);
            _zone.SetColor(_zoneColor);

            // Check mark: two bars in a small upright plane that faces the viewer (yaw only).
            _check = new GameObject("Check").transform;
            _check.SetParent(transform, false);
            _checkShort = Bar("Short", new Vector3(-0.009f, 0.004f, 0f), -45f, 0.016f);
            _checkLong = Bar("Long", new Vector3(0.006f, 0.010f, 0f), 50f, 0.032f);
            if (Camera.main != null) _viewer = Camera.main.transform;
            Hide();
        }

        private IndicatorPart Bar(string name, Vector3 at, float roll, float length)
        {
            var part = new IndicatorPart(_check, name, _material);
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.Mesh.vertices = tmp.GetComponent<MeshFilter>().sharedMesh.vertices;
            part.Mesh.triangles = tmp.GetComponent<MeshFilter>().sharedMesh.triangles;
            part.Mesh.RecalculateBounds();
            Destroy(tmp);
            part.Transform.localPosition = at;
            part.Transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            part.Transform.localScale = new Vector3(length, 0.006f, 0.004f);
            part.SetColor(_doneColor);
            return part;
        }

        /// <param name="center">Bolt centre on the surface.</param>
        /// <param name="axis">Bolt axis (out of the hole).</param>
        /// <param name="zeroDirection">Where the ring starts (0), e.g. towards the trainee; it fills clockwise from there.</param>
        public void Show(Vector3 center, Vector3 axis, Vector3 zeroDirection, float fill01, float zoneStart01, float zoneEnd01, Look look)
        {
            Init();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            var forward = Vector3.ProjectOnPlane(zeroDirection, axis);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.ProjectOnPlane(Vector3.forward, axis);
            transform.SetPositionAndRotation(center + axis * 0.001f, Quaternion.LookRotation(forward.normalized, axis));

            if (!Mathf.Approximately(zoneStart01, _shownZoneStart) || !Mathf.Approximately(zoneEnd01, _shownZoneEnd))
            {
                _shownZoneStart = zoneStart01;
                _shownZoneEnd = zoneEnd01;
                ArcMesh.Build(_zone.Mesh, _innerRadius - _zoneOverhang * 0.5f, _outerRadius + _zoneOverhang, zoneStart01 * 360f, zoneEnd01 * 360f, 3f);
            }
            fill01 = Mathf.Clamp01(fill01);
            if (Mathf.Abs(fill01 - _shownFill) > 0.002f)
            {
                _shownFill = fill01;
                ArcMesh.Build(_fill.Mesh, _innerRadius, _outerRadius, 0f, fill01 * 360f);
            }
            _fill.SetColor(look == Look.Done ? _doneColor : look == Look.Over ? _overColor : _risingColor);

            bool done = look == Look.Done;
            if (_check.gameObject.activeSelf != done) _check.gameObject.SetActive(done);
            if (done)
            {
                _check.position = center + axis * _checkHeight;
                if (_viewer != null)
                {
                    var away = _check.position - _viewer.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 1e-6f) _check.rotation = Quaternion.LookRotation(away, Vector3.up);
                }
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public bool IsShown => gameObject.activeSelf;
    }
}
