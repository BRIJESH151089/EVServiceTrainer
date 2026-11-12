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

        [SerializeField] private Color _warningStatus = new Color(0.62f, 0.42f, 0.05f, 0.95f);
        [SerializeField] private Button _restartButton;

        /// <summary>Raised when the trainee presses Restart.</summary>
        public event System.Action RestartRequested;

        private void Awake()
        {
            if (_restartButton != null) _restartButton.onClick.AddListener(() => RestartRequested?.Invoke());
        }

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
                    ? InstallResultText.Describe(lastResult.Value, definition, progress.Mode) + " Module complete."
                    : "Module complete.";
                statusColor = _goodStatus;
            }
            else if (lastResult.HasValue)
            {
                status = InstallResultText.Describe(lastResult.Value, definition, progress.Mode);
                statusColor = lastResult.Value.IsInstalled ? _goodStatus : _badStatus;
            }
            else
            {
                var current = progress.CurrentPart;
                if (current == null) status = "";
                else if (learn) status = $"Next: {current.Instruction}. Match the glowing outline.";
                else status = $"{StepCounter(progress)}. Work from memory: no hints in Test mode."; // never names the next step
                statusColor = _neutralStatus;
            }

            if (_status != null) _status.text = status;
            if (_statusBackground != null) _statusBackground.color = statusColor;
        }

        /// <summary>Badge before a mode is chosen: neutral "MODE?".</summary>
        public void ShowModeNotChosen()
        {
            if (_modeBadge != null) _modeBadge.text = "MODE?";
            if (_modeBadgeBackground != null) _modeBadgeBackground.color = new Color(0.35f, 0.37f, 0.4f, 1f);
        }

        /// <summary>Temporary status line (e.g. "Heavy part: use both hands"); the next Render replaces it.</summary>
        public void ShowNotice(string text, bool warning)
        {
            if (_status != null) _status.text = text;
            if (_statusBackground != null) _statusBackground.color = warning ? _warningStatus : _neutralStatus;
        }

        /// <summary>"Step N of M": N is the first unfinished step (M when complete).</summary>
        private static string StepCounter(AssemblyProgress progress)
        {
            var steps = progress.Definition.Steps;
            var current = progress.CurrentStep;
            int n = current != null ? current.Index + 1 : steps.Count;
            return $"Step {n} of {steps.Count}";
        }

        private string BuildStepList(AssemblyProgress progress)
        {
            // One line per step; steps with several identical parts show a count, e.g. "Install battery modules (2/4)".
            // Test mode lists only completed steps plus the step counter: upcoming steps are not revealed.
            _sb.Clear();
            var steps = progress.Definition.Steps;
            var current = progress.CurrentStep;
            if (progress.Mode == TrainingMode.Test)
            {
                foreach (var step in steps)
                {
                    if (progress.InstalledCount(step) < step.Slots.Count) continue;
                    string done = step.Slots.Count > 1 ? $"{step.Instruction} ({step.Slots.Count}/{step.Slots.Count})" : step.Instruction;
                    _sb.Append("<color=").Append(DoneColor).Append(">[x]  ").Append(done).Append("</color>\n");
                }
                _sb.Append("<color=").Append(CurrentColor).Append("><b>").Append(StepCounter(progress)).Append("</b></color>");
                return _sb.ToString();
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                int done = progress.InstalledCount(step);
                bool complete = done == step.Slots.Count;
                string text = step.Slots.Count > 1 ? $"{step.Instruction} ({done}/{step.Slots.Count})" : step.Instruction;

                if (complete)
                    _sb.Append("<color=").Append(DoneColor).Append(">[x]  ").Append(text).Append("</color>");
                else if (step == current)
                    _sb.Append("<color=").Append(CurrentColor).Append("><b>></b>    <b>").Append(text).Append("</b></color>");
                else
                    _sb.Append("<color=").Append(PendingColor).Append(">[  ]  ").Append(text).Append("</color>");
                if (i < steps.Count - 1) _sb.Append('\n');
            }
            return _sb.ToString();
        }
    }
}
