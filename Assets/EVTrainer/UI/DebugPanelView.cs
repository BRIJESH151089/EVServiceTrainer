using System.Globalization;
using System.Text;
using EVTrainer.Core;
using UnityEngine;
using UnityEngine.UI;

namespace EVTrainer.UI
{
    /// <summary>Everything the Phase 1 debug panel shows, as plain data.</summary>
    public readonly struct DebugPanelData
    {
        public DebugPanelData(InputFrame left, InputFrame right, bool isHeld, string heldBy,
            SnapEvaluation snap, float positionToleranceMetres, float angleToleranceDegrees)
        {
            Left = left;
            Right = right;
            IsHeld = isHeld;
            HeldBy = heldBy;
            Snap = snap;
            PositionToleranceMetres = positionToleranceMetres;
            AngleToleranceDegrees = angleToleranceDegrees;
        }

        public InputFrame Left { get; }
        public InputFrame Right { get; }
        public bool IsHeld { get; }
        public string HeldBy { get; }
        public SnapEvaluation Snap { get; }
        public float PositionToleranceMetres { get; }
        public float AngleToleranceDegrees { get; }
    }

    /// <summary>World-space debug panel: input values, grab state and live snap error.</summary>
    public sealed class DebugPanelView : MonoBehaviour
    {
        [SerializeField] private Text _label;
        [SerializeField] private Graphic _background;
        [SerializeField] private Color _outOfToleranceColor = new Color(0.15f, 0.15f, 0.18f, 0.9f);
        [SerializeField] private Color _withinToleranceColor = new Color(0.05f, 0.55f, 0.15f, 0.9f);

        private readonly StringBuilder _sb = new StringBuilder(512);

        public void Render(in DebugPanelData data)
        {
            var c = CultureInfo.InvariantCulture;
            _sb.Clear();
            _sb.AppendLine("EV Service Trainer - Phase 1 Debug");
            _sb.AppendLine();
            AppendFrame(_sb, "Left ", data.Left, c);
            AppendFrame(_sb, "Right", data.Right, c);
            _sb.AppendLine();
            _sb.Append("Grab: ").AppendLine(data.IsHeld ? $"HELD ({data.HeldBy})" : "released");
            _sb.AppendLine();
            _sb.Append("Position error: ")
               .Append((data.Snap.PositionErrorMetres * 100f).ToString("0.0", c)).Append(" cm  (tol ")
               .Append((data.PositionToleranceMetres * 100f).ToString("0.0", c)).Append(" cm) ")
               .AppendLine(data.Snap.IsPositionWithinTolerance ? "OK" : "--");
            _sb.Append("Angle error:    ")
               .Append(data.Snap.AngleErrorDegrees.ToString("0.0", c)).Append(" deg (tol ")
               .Append(data.AngleToleranceDegrees.ToString("0.0", c)).Append(" deg) ")
               .AppendLine(data.Snap.IsAngleWithinTolerance ? "OK" : "--");
            _sb.AppendLine();
            _sb.Append(data.Snap.IsWithinTolerance ? "WITHIN TOLERANCE" : "out of tolerance");

            if (_label != null) _label.text = _sb.ToString();
            if (_background != null) _background.color = data.Snap.IsWithinTolerance ? _withinToleranceColor : _outOfToleranceColor;
        }

        private static void AppendFrame(StringBuilder sb, string name, InputFrame f, CultureInfo c)
        {
            sb.Append(name).Append("  src=").Append(f.Source);
            if (!f.IsTracked)
            {
                sb.AppendLine("  (not tracked)");
                return;
            }
            sb.Append("  grip=").Append(f.Grip.ToString("0.00", c));
            if (f.Source == InputSource.Hand)
                sb.Append("  pinch=").Append(f.Pinch.ToString("0.00", c));
            else
                sb.Append("  trigger=").Append(f.Trigger.ToString("0.00", c));
            sb.AppendLine();
        }
    }
}
