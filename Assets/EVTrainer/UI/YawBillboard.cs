using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.UI
{
    /// <summary>
    /// Turns a world-space label to face the viewer, yaw only, smoothly (Core <see cref="YawFollower"/>: dead zone +
    /// easing). Runs in LateUpdate, after tracking and anything that placed the label this frame, so it never lags or
    /// jitters with head noise. UI text faces -Z, so the label looks away from the viewer.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class YawBillboard : MonoBehaviour
    {
        [SerializeField, Range(0f, 20f)] private float _deadZoneDegrees = 4f;
        [SerializeField, Range(0.02f, 1f)] private float _timeConstantSeconds = 0.18f;
        [Tooltip("Fixed backward tilt so labels below eye level face up toward the viewer (not head-tracked: no jitter).")]
        [SerializeField, Range(0f, 80f)] private float _tiltBackDegrees;

        public float TiltBackDegrees
        {
            get => _tiltBackDegrees;
            set => _tiltBackDegrees = value;
        }

        private YawFollower _follower;
        private Transform _viewer;

        private void OnEnable()
        {
            _follower = new YawFollower(_deadZoneDegrees, _timeConstantSeconds);
            Face(snap: true);
        }

        private void LateUpdate() => Face(snap: false);

        private void Face(bool snap)
        {
            if (_viewer == null)
            {
                if (Camera.main == null) return;
                _viewer = Camera.main.transform;
            }
            var away = transform.position - _viewer.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f) return;
            float target = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
            float yaw = snap ? Snap(target) : _follower.Update(target, Time.deltaTime);
            transform.rotation = Quaternion.Euler(_tiltBackDegrees, yaw, 0f);
        }

        private float Snap(float target)
        {
            _follower.Snap(target);
            return _follower.Yaw;
        }
    }
}
