using System;
using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.UI
{
    /// <summary>
    /// Mode selection: two latching push buttons, "Learn" and "Test". The chosen one stays pressed in (selected) and
    /// the other is disabled until Restart, so the panel also shows the current mode during the run.
    /// </summary>
    public sealed class StartPanelView : MonoBehaviour
    {
        [Tooltip("Module title; set at runtime from the loaded module data.")]
        [SerializeField] private TMPro.TMP_Text _title;
        [Tooltip("\"Choose a mode\" before the run; the chosen mode after.")]
        [SerializeField] private TMPro.TMP_Text _prompt;
        [SerializeField] private PushButtonView _learnButton;
        [SerializeField] private PushButtonView _testButton;

        private bool _chosen;

        /// <summary>Raised once when the trainee picks a mode.</summary>
        public event Action<TrainingMode> ModeChosen;

        public bool HasChosen => _chosen;

        private void Awake()
        {
            if (_learnButton != null) _learnButton.Clicked += () => Choose(TrainingMode.Learn);
            if (_testButton != null) _testButton.Clicked += () => Choose(TrainingMode.Test);
        }

        /// <summary>Shows the panel with the title of the module about to be trained, both buttons available.</summary>
        public void Show(string moduleTitle)
        {
            if (_title != null && !string.IsNullOrEmpty(moduleTitle)) _title.text = moduleTitle;
            gameObject.SetActive(true);
        }

        private void Choose(TrainingMode mode)
        {
            if (_chosen) return;
            _chosen = true;
            DevLog.Info($"Start panel: '{mode}' chosen (button latched until Restart).");
            var other = mode == TrainingMode.Learn ? _testButton : _learnButton;
            if (other != null) other.SetInteractable(false);
            if (_prompt != null) _prompt.text = $"{mode} mode. Press Restart to change.";
            ModeChosen?.Invoke(mode);
        }
    }
}
