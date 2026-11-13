using EVTrainer.Core;
using TMPro;
using UnityEngine;

namespace EVTrainer.UI
{
    /// <summary>Live torque readout on the wrench handle: "6.4 / 8.0 N·m", white below spec, green in the window, red over.</summary>
    public sealed class TorqueGaugeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Color _normal = Color.white;
        [SerializeField] private Color _inSpec = new Color(0.35f, 1f, 0.45f);
        [SerializeField] private Color _over = new Color(1f, 0.3f, 0.25f);

        private float _shownTorque = -1f;
        private BoltState _shownState;
        private bool _idle;

        /// <summary>Not on a bolt: shows the target only.</summary>
        public void ShowIdle(float specNm)
        {
            if (_idle || _text == null) return;
            _idle = true;
            _shownTorque = -1f;
            _text.SetText("-- / {0:1} N·m", specNm); // TMP formats without allocating
            _text.color = _normal;
        }

        public void Show(float torqueNm, float specNm, BoltState state)
        {
            if (_text == null) return;
            if (!_idle && Mathf.Abs(torqueNm - _shownTorque) < 0.05f && state == _shownState) return;
            _idle = false;
            _shownTorque = torqueNm;
            _shownState = state;
            _text.SetText("{0:1} / {1:1} N·m", torqueNm, specNm); // every torque change: no garbage
            _text.color = state == BoltState.OverTorqued ? _over : state == BoltState.Torqued ? _inSpec : _normal;
        }
    }
}
