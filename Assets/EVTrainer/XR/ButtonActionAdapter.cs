using UnityEngine;
using UnityEngine.InputSystem;

namespace EVTrainer.XR
{
    /// <summary>
    /// One input button (e.g. the left controller's X button) exposed as a plain pressed-this-frame flag,
    /// so App code never touches the Input System directly.
    /// </summary>
    public sealed class ButtonActionAdapter : MonoBehaviour
    {
        [SerializeField] private InputActionProperty _action = new InputActionProperty(
            new InputAction("Button", InputActionType.Button));

        public bool WasPressedThisFrame => _action.action != null && _action.action.WasPressedThisFrame();

        private void OnEnable() => _action.action?.Enable();

        private void OnDisable()
        {
            // Only disable actions this component owns; referenced actions belong to their asset.
            if (_action.reference == null) _action.action?.Disable();
        }
    }
}
