using System.Collections;
using UnityEngine;

namespace EVTrainer.XR
{
    /// <summary>
    /// "Tools never fall": remembers where a hand tool or bolt was laid out, holds it still (kinematic, no gravity) and
    /// glides it back there when asked. Used by the wrench and bolt adapters.
    /// </summary>
    internal sealed class ToolHome
    {
        private readonly MonoBehaviour _owner;
        private readonly Transform _transform;
        private readonly Rigidbody _rigidbody;
        private readonly Vector3 _homePosition;
        private readonly Quaternion _homeRotation;
        private Coroutine _move;

        public ToolHome(MonoBehaviour owner, Rigidbody rigidbody)
        {
            _owner = owner;
            _transform = owner.transform;
            _rigidbody = rigidbody;
            _homePosition = _transform.position;
            _homeRotation = _transform.rotation;
        }

        public bool IsReturning => _move != null;

        /// <summary>Stays exactly where it is: kinematic, no gravity, no leftover motion.</summary>
        public void HoldStill()
        {
            if (_rigidbody == null) return;
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
        }

        /// <summary>Glides back to the laid-out pose over <paramref name="seconds"/>, held still on the way.</summary>
        public void Return(float seconds)
        {
            Cancel();
            HoldStill();
            _move = _owner.StartCoroutine(Glide(Mathf.Max(0.01f, seconds)));
        }

        /// <summary>Stops a return in progress (e.g. the trainee grabbed it on the way).</summary>
        public void Cancel()
        {
            if (_move == null) return;
            _owner.StopCoroutine(_move);
            _move = null;
        }

        private IEnumerator Glide(float seconds)
        {
            var fromPosition = _transform.position;
            var fromRotation = _transform.rotation;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                Place(Vector3.Lerp(fromPosition, _homePosition, k), Quaternion.Slerp(fromRotation, _homeRotation, k));
                yield return null;
            }
            Place(_homePosition, _homeRotation);
            _move = null;
        }

        private void Place(Vector3 position, Quaternion rotation)
        {
            _transform.SetPositionAndRotation(position, rotation);
            if (_rigidbody == null) return;
            _rigidbody.position = position;
            _rigidbody.rotation = rotation;
        }
    }
}
