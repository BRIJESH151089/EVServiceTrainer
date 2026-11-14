#if UNITY_EDITOR
using System.Reflection;
using EVTrainer.Core;
using EVTrainer.XR;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// Editor only: makes the XRI "XR Interaction Simulator" hold its controllers like a trainee does on the Quest. The
    /// simulator starts (and resets) its controllers at the headset's rotation, which for an OpenXR grip pose means the
    /// fingers point up. This sets both controllers to Core <see cref="GripPose.HeldStraightAhead"/> (turned with the
    /// headset's yaw) at start and again after each simulator reset, so poke, grab and the gloves behave as on the
    /// Quest. The simulator keeps its controller state private, so it is set by reflection (Editor tooling only).
    /// </summary>
    internal sealed class SimulatorGripPose : MonoBehaviour
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private Component _simulator;
        private FieldInfo _hmd, _left, _right, _leftEuler, _rightEuler;
        private FieldInfo _stateRotation, _hmdRotation;
        private float _nextCheck;

        public static void Attach(GameObject simulator)
        {
            var sim = simulator.GetComponent("XRInteractionSimulator");
            if (sim == null) return;
            var holder = simulator.AddComponent<SimulatorGripPose>();
            holder._simulator = sim;
            if (!holder.Bind()) { Destroy(holder); return; }
            holder.Apply();
        }

        private bool Bind()
        {
            var t = _simulator.GetType();
            _hmd = t.GetField("m_HMDState", Private);
            _left = t.GetField("m_LeftControllerState", Private);
            _right = t.GetField("m_RightControllerState", Private);
            _leftEuler = t.GetField("m_LeftControllerEuler", Private);
            _rightEuler = t.GetField("m_RightControllerEuler", Private);
            if (_hmd == null || _left == null || _right == null || _leftEuler == null || _rightEuler == null)
            {
                Debug.LogWarning("[EVTrainer] XR Interaction Simulator internals changed: controllers keep the simulator's default rotation.");
                return false;
            }
            _stateRotation = _left.FieldType.GetField("deviceRotation");
            _hmdRotation = _hmd.FieldType.GetField("deviceRotation");
            return _stateRotation != null && _hmdRotation != null;
        }

        /// <summary>Held straight ahead, facing where the simulated headset faces.</summary>
        private Quaternion Held()
        {
            var head = (Quaternion)_hmdRotation.GetValue(_hmd.GetValue(_simulator));
            var yaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            return yaw * GripPose.HeldStraightAhead(System.Numerics.Vector3.Zero).Rotation.ToUnity();
        }

        private void Apply()
        {
            var held = Held();
            Set(_left, _leftEuler, held);
            Set(_right, _rightEuler, held);
        }

        private void Set(FieldInfo state, FieldInfo euler, Quaternion rotation)
        {
            object boxed = state.GetValue(_simulator);
            _stateRotation.SetValue(boxed, rotation);
            state.SetValue(_simulator, boxed);
            euler.SetValue(_simulator, rotation.eulerAngles);
        }

        private void LateUpdate()
        {
            // The simulator's Reset puts the controllers back at the headset's rotation: hold them again.
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.25f;
            var head = (Quaternion)_hmdRotation.GetValue(_hmd.GetValue(_simulator));
            var left = (Quaternion)_stateRotation.GetValue(_left.GetValue(_simulator));
            if (Quaternion.Angle(head, left) < 0.5f) Apply();
        }
    }
}
#endif
