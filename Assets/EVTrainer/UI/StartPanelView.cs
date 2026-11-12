using System;
using EVTrainer.Core;
using UnityEngine;
using UnityEngine.UI;

namespace EVTrainer.UI
{
    /// <summary>World-space mode selection shown before a run: "Learn mode" or "Test mode".</summary>
    public sealed class StartPanelView : MonoBehaviour
    {
        [Tooltip("Module title; set at runtime from the loaded module data.")]
        [SerializeField] private TMPro.TMP_Text _title;
        [SerializeField] private Button _learnButton;
        [SerializeField] private Button _testButton;

        /// <summary>Raised once when the trainee picks a mode; the panel hides itself.</summary>
        public event Action<TrainingMode> ModeChosen;

        private void Awake()
        {
            if (_learnButton != null) _learnButton.onClick.AddListener(() => Choose(TrainingMode.Learn));
            if (_testButton != null) _testButton.onClick.AddListener(() => Choose(TrainingMode.Test));
        }

        /// <summary>Shows the panel with the title of the module about to be trained.</summary>
        public void Show(string moduleTitle)
        {
            if (_title != null && !string.IsNullOrEmpty(moduleTitle)) _title.text = moduleTitle;
            gameObject.SetActive(true);
        }

        private void Choose(TrainingMode mode)
        {
            gameObject.SetActive(false);
            ModeChosen?.Invoke(mode);
        }
    }
}
