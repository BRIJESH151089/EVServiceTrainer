using System;
using EVTrainer.Core;
using EVTrainer.UI;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// End of a run: builds the Core <see cref="TrainingReport"/> (scoring rules from the module data), shows it on the
    /// info panel in place of the step list, saves it as JSON (<see cref="ReportStore"/>), and offers Retry Test and
    /// Practice (a fresh run straight in that mode) next to the exploded-view buttons. The mistakes list pages with two
    /// small push buttons when it is long.
    /// </summary>
    public sealed class RunReportController : MonoBehaviour
    {
        [SerializeField] private AssemblyTrainingController _training;
        [SerializeField] private ReportPanelView _panel;
        [SerializeField] private PushButtonView _retryTestButton;
        [SerializeField] private PushButtonView _practiceButton;
        [SerializeField] private PushButtonView _previousPageButton;
        [SerializeField] private PushButtonView _nextPageButton;

        /// <summary>The report of the run that just completed (null before).</summary>
        public TrainingReport Report { get; private set; }

        /// <summary>Raised once the report is built and shown.</summary>
        public event Action<TrainingReport> ReportReady;

        private void Awake() => SetButtons(completed: false);

        // At scene start (behind the fade-in), not at the completion moment: size the report texts and wake the buttons
        // that first appear on completion (their text components allocate when they first become active).
        private void Start()
        {
            if (_panel != null) _panel.Prewarm();
            if (_retryTestButton != null) WakeOnce(_retryTestButton.transform.parent);
            if (_previousPageButton != null) WakeOnce(_previousPageButton.transform.parent);
        }

        /// <summary>Activates <paramref name="row"/> and all its children for one moment, then restores their states.</summary>
        private static void WakeOnce(Transform row)
        {
            if (row == null) return;
            bool rowActive = row.gameObject.activeSelf;
            var states = new bool[row.childCount];
            for (int i = 0; i < states.Length; i++) states[i] = row.GetChild(i).gameObject.activeSelf;
            row.gameObject.SetActive(true);
            for (int i = 0; i < states.Length; i++) row.GetChild(i).gameObject.SetActive(true);
            for (int i = 0; i < states.Length; i++) row.GetChild(i).gameObject.SetActive(states[i]);
            row.gameObject.SetActive(rowActive);
        }

        private void OnEnable()
        {
            if (_training != null) _training.Completed += OnCompleted;
            if (_retryTestButton != null) _retryTestButton.Clicked += OnRetryTest;
            if (_practiceButton != null) _practiceButton.Clicked += OnPractice;
            if (_previousPageButton != null) _previousPageButton.Clicked += OnPreviousPage;
            if (_nextPageButton != null) _nextPageButton.Clicked += OnNextPage;
        }

        private void OnDisable()
        {
            if (_training != null) _training.Completed -= OnCompleted;
            if (_retryTestButton != null) _retryTestButton.Clicked -= OnRetryTest;
            if (_practiceButton != null) _practiceButton.Clicked -= OnPractice;
            if (_previousPageButton != null) _previousPageButton.Clicked -= OnPreviousPage;
            if (_nextPageButton != null) _nextPageButton.Clicked -= OnNextPage;
        }

        private void OnCompleted()
        {
            var progress = _training.Progress;
            var end = DateTimeOffset.Now;
            Report = TrainingReportBuilder.Build(progress, _training.ScoringRules, Guid.NewGuid().ToString("N"),
                _training.RunStartedAt, end, _training.RunTime);
            if (_panel != null) _panel.Show(Report);
            SetButtons(completed: true);
            DevLog.Info($"Run complete: {Report.result} {Report.score} in {ReportFormat.Duration(Report.durationSeconds)}, " +
                        $"{Report.summary.mistakeCount} mistakes ({Report.summary.criticalCount} critical).");
            ReportReady?.Invoke(Report);
            StartCoroutine(SaveNextFrame(Report, end));
        }

        /// <summary>Saved one frame after the panel appears, so the completion frame stays light.</summary>
        private System.Collections.IEnumerator SaveNextFrame(TrainingReport report, DateTimeOffset end)
        {
            yield return null;
            SavedPath = ReportStore.Save(report, end);
            DevLog.Info("Report saved: " + SavedPath);
        }

        /// <summary>Where the last report was written (empty before).</summary>
        public string SavedPath { get; private set; } = "";

        private void SetButtons(bool completed)
        {
            SetVisible(_retryTestButton, completed);
            SetVisible(_practiceButton, completed);
            bool paged = completed && _panel != null && _panel.PageCount > 1;
            SetVisible(_previousPageButton, paged);
            SetVisible(_nextPageButton, paged);
        }

        private static void SetVisible(PushButtonView button, bool visible)
        {
            if (button != null && button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
        }

        private void OnRetryTest() => _training.Restart(TrainingMode.Test);
        private void OnPractice() => _training.Restart(TrainingMode.Learn);
        private void OnPreviousPage() { if (_panel != null) _panel.PreviousPage(); }
        private void OnNextPage() { if (_panel != null) _panel.NextPage(); }
    }
}
