using System.Collections.Generic;
using System.Text;
using EVTrainer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EVTrainer.UI
{
    /// <summary>
    /// The completion report on the info panel, in place of the step list: a score ring with the number, a PASS / FAIL
    /// badge (Test) or "Practice summary" (Learn), the key rows and a short mistakes list (most important first, paged).
    /// All texts are built once in <see cref="Show"/>; the animation (fade and settle in, ring fill and count-up) only
    /// sets numbers, so nothing allocates while it plays.
    /// </summary>
    public sealed class ReportPanelView : MonoBehaviour
    {
        [Tooltip("Hidden while the report shows (the step list).")]
        [SerializeField] private GameObject _replaces;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _ring;
        [SerializeField] private TMP_Text _score;
        [SerializeField] private Image _badge;
        [SerializeField] private TMP_Text _badgeText;
        [SerializeField] private TMP_Text _rowLabels;
        [SerializeField] private TMP_Text _rowValues;
        [SerializeField] private TMP_Text _mistakesTitle;
        [SerializeField] private TMP_Text _mistakes;
        [SerializeField] private TMP_Text _page;
        [SerializeField, Min(1)] private int _mistakesPerPage = 3;

        [Header("Colours")]
        [SerializeField] private Color _pass = new Color(0.16f, 0.65f, 0.3f, 1f);
        [SerializeField] private Color _fail = new Color(0.75f, 0.2f, 0.15f, 1f);
        [SerializeField] private Color _practice = new Color(0.12f, 0.45f, 0.85f, 1f);
        [SerializeField] private Color _critical = new Color(1f, 0.45f, 0.4f, 1f);

        [Header("Animation")]
        [SerializeField, Min(0.05f)] private float _fadeSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float _fillSeconds = 0.9f;
        [SerializeField, Range(0.8f, 1f)] private float _startScale = 0.96f;

        private readonly List<string> _pages = new List<string>();
        private readonly List<string> _pageLabels = new List<string>();
        private int _pageIndex;
        private int _targetScore;
        private int _shownScore = -1;
        private float _startTime = -1f;
        private bool _prewarming;

        public bool IsShown => gameObject.activeSelf;
        public int PageCount => _pages.Count;

        private static string[] s_Numbers;

        /// <summary>"0".."100", built once.</summary>
        private static string[] Numbers
        {
            get
            {
                if (s_Numbers != null) return s_Numbers;
                s_Numbers = new string[101];
                for (int i = 0; i <= 100; i++) s_Numbers[i] = i.ToString();
                return s_Numbers;
            }
        }

        /// <summary>
        /// Lays every text out once with full-length sample content (call at scene start, behind the fade-in) so the
        /// text buffers are already sized when the report appears: no allocation spike at the completion moment.
        /// Leaves the panel hidden.
        /// </summary>
        public void Prewarm()
        {
            if (gameObject.activeSelf) return;
            var _ = Numbers;
            if (_group != null) _group.alpha = 0f;
            _prewarming = true;
            gameObject.SetActive(true);
            const string line = "• Torqued too early · Battery Module 4 · 10m 59s  (critical)\n";
            Fill(_score, "100");
            Fill(_badgeText, "Practice summary");
            Fill(_rowLabels, "Steps in correct order\nWrong-order attempts\nFirst-time fits\nTorque in spec\nCross pattern followed\nCritical mistakes\nTotal time\n");
            Fill(_rowValues, "<color=#FF7366>10/10</color>\n100\n100%\n10/10\n<color=#FF7366>Yes</color>\n<color=#FF7366>10</color>\n10m 59s\n");
            Fill(_mistakesTitle, "Mistakes (100)");
            Fill(_mistakes, "<color=#FF7366>" + line + line + line + "</color>");
            Fill(_page, "10/10");
            _prewarming = false;
            gameObject.SetActive(false);
        }

        private static void Fill(TMP_Text text, string sample)
        {
            if (text == null) return;
            text.text = sample;
            text.ForceMeshUpdate(true);
        }

        private void Awake()
        {
            if (_startTime < 0f && !_prewarming) gameObject.SetActive(false);
        }

        /// <summary>Shows the report (builds every text now) and animates it in.</summary>
        public void Show(TrainingReport report)
        {
            if (report == null) return;
            bool practice = report.result == "PRACTICE";
            var colour = practice ? _practice : report.result == "PASS" ? _pass : _fail;
            _targetScore = report.score;
            _shownScore = -1;
            if (_ring != null) { _ring.color = colour; _ring.fillAmount = 0f; }
            if (_badge != null) _badge.color = colour;
            if (_badgeText != null) _badgeText.text = practice ? "Practice summary" : report.result;

            var labels = new StringBuilder();
            var values = new StringBuilder();
            foreach (var row in ReportFormat.KeyRows(report))
            {
                bool bad = (row.Key == "Critical mistakes" && report.summary.criticalCount > 0)
                           || (row.Key == "Cross pattern followed" && !report.summary.crossPatternFollowed);
                labels.Append(row.Key).Append('\n');
                if (bad) values.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(_critical)).Append('>');
                values.Append(row.Value);
                if (bad) values.Append("</color>");
                values.Append('\n');
            }
            if (_rowLabels != null) _rowLabels.text = labels.ToString();
            if (_rowValues != null) _rowValues.text = values.ToString();

            BuildPages(report);
            _pageIndex = 0;
            ShowPage();

            _startTime = Time.unscaledTime;
            if (_replaces != null) _replaces.SetActive(false);
            gameObject.SetActive(true);
            Animate();
        }

        public void Hide()
        {
            _startTime = -1f;
            gameObject.SetActive(false);
            if (_replaces != null) _replaces.SetActive(true);
        }

        public void NextPage() => Turn(+1);
        public void PreviousPage() => Turn(-1);

        private void Turn(int delta)
        {
            if (_pages.Count <= 1) return;
            _pageIndex = (_pageIndex + delta + _pages.Count) % _pages.Count;
            ShowPage();
        }

        private void BuildPages(TrainingReport report)
        {
            _pages.Clear();
            _pageLabels.Clear();
            var sorted = ReportFormat.ByImportance(report.mistakes);
            int count = ReportFormat.PageCount(sorted.Count, _mistakesPerPage);
            var sb = new StringBuilder();
            for (int p = 0; p < count; p++)
            {
                sb.Clear();
                for (int i = p * _mistakesPerPage; i < sorted.Count && i < (p + 1) * _mistakesPerPage; i++)
                {
                    var m = sorted[i];
                    if (m.critical) sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(_critical)).Append(">");
                    sb.Append("• ").Append(ReportFormat.MistakeLine(m));
                    if (m.critical) sb.Append("  (critical)</color>");
                    sb.Append('\n');
                }
                if (sorted.Count == 0) sb.Append("No mistakes. Well done.");
                _pages.Add(sb.ToString());
                _pageLabels.Add(count > 1 ? $"{p + 1}/{count}" : "");
            }
            if (_mistakesTitle != null) _mistakesTitle.text = sorted.Count == 0 ? "Mistakes" : $"Mistakes ({sorted.Count})";
        }

        private void ShowPage()
        {
            if (_pages.Count == 0) return;
            if (_mistakes != null) _mistakes.text = _pages[_pageIndex];
            if (_page != null) _page.text = _pageLabels[_pageIndex];
        }

        private void Update()
        {
            if (_startTime >= 0f) Animate();
        }

        /// <summary>Fade and settle in, then fill the ring and count the score up (no allocation: numbers only).</summary>
        private void Animate()
        {
            float t = Time.unscaledTime - _startTime;
            float fade = Smooth(t / _fadeSeconds);
            if (_group != null) _group.alpha = fade;
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, 1f, fade);
            float fill = Smooth((t - _fadeSeconds * 0.5f) / _fillSeconds);
            if (_ring != null) _ring.fillAmount = fill * _targetScore / 100f;
            int shown = Mathf.Clamp(Mathf.RoundToInt(fill * _targetScore), 0, 100);
            if (shown != _shownScore && _score != null)
            {
                _shownScore = shown;
                _score.text = Numbers[shown]; // cached strings: the count-up allocates nothing
            }
            if (t >= _fadeSeconds * 0.5f + _fillSeconds) _startTime = -1f; // done: stop updating
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
