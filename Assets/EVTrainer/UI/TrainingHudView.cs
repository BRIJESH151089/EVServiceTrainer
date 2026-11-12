using System.Text;
using EVTrainer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EVTrainer.UI
{
    /// <summary>
    /// World-space training HUD: module title, mode badge, the step list from the module definition
    /// (done / current / pending) and a one-line status for the last install attempt.
    /// </summary>
    public sealed class TrainingHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _modeBadge;
        [SerializeField] private Graphic _modeBadgeBackground;
        [SerializeField] private TMP_Text _steps;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Graphic _statusBackground;

        [Header("Colours")]
        [SerializeField] private Color _learnColor = new Color(0.12f, 0.45f, 0.85f, 1f);
        [SerializeField] private Color _testColor = new Color(0.85f, 0.35f, 0.1f, 1f);
        [SerializeField] private Color _neutralStatus = new Color(0.16f, 0.17f, 0.2f, 0.95f);
        [SerializeField] private Color _goodStatus = new Color(0.08f, 0.5f, 0.2f, 0.95f);
        [SerializeField] private Color _badStatus = new Color(0.6f, 0.15f, 0.1f, 0.95f);

        private const string DoneColor = "#7FD88F";
        private const string CurrentColor = "#FFD34D";
        private const string PendingColor = "#9AA0A8";

        private readonly StringBuilder _sb = new StringBuilder(512);

        public void Render(AssemblyProgress progress, InstallResult? lastResult)
        {
            var definition = progress.Definition;
            if (_title != null) _title.text = definition.Title;

            bool learn = progress.Mode == TrainingMode.Learn;
            if (_modeBadge != null) _modeBadge.text = learn ? "LEARN" : "TEST";
            if (_modeBadgeBackground != null) _modeBadgeBackground.color = learn ? _learnColor : _testColor;

            if (_steps != null) _steps.text = BuildStepList(progress);

            string status;
            Color statusColor;
            if (progress.IsComplete && (lastResult == null || lastResult.Value.IsInstalled))
            {
                status = lastResult.HasValue
                    ? InstallResultText.Describe(lastResult.Value, definition) + " Module complete."
                    : "Module complete.";
                statusColor = _goodStatus;
            }
            else if (lastResult.HasValue)
            {
                status = InstallResultText.Describe(lastResult.Value, definition);
                statusColor = lastResult.Value.IsInstalled ? _goodStatus : _badStatus;
            }
            else
            {
                var current = progress.CurrentPart;
                status = current != null
                    ? (learn ? $"Next: {current.Instruction}. Match the glowing outline." : $"Next: {current.Instruction}.")
                    : "";
                statusColor = _neutralStatus;
            }

            if (_status != null) _status.text = status;
            if (_statusBackground != null) _statusBackground.color = statusColor;
        }

        private string BuildStepList(AssemblyProgress progress)
        {
            _sb.Clear();
            var parts = progress.Definition.Parts;
            int current = progress.CurrentStepIndex;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (progress.IsInstalled(part.Id))
                    _sb.Append("<color=").Append(DoneColor).Append(">[x]  ").Append(part.Instruction).Append("</color>");
                else if (i == current)
                    _sb.Append("<color=").Append(CurrentColor).Append("><b>></b>    <b>").Append(part.Instruction).Append("</b></color>");
                else
                    _sb.Append("<color=").Append(PendingColor).Append(">[  ]  ").Append(part.Instruction).Append("</color>");
                if (i < parts.Count - 1) _sb.Append('\n');
            }
            return _sb.ToString();
        }
    }
}
