using System;
using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.UI
{
    /// <summary>Mode selection shown before a run: two physical push buttons, "Learn mode" and "Test mode".</summary>
    public sealed class StartPanelView : MonoBehaviour
    {
        [Tooltip("Module title; set at runtime from the loaded module data.")]
        [SerializeField] private TMPro.TMP_Text _title;
        [SerializeField] private PushButtonView _learnButton;
        [SerializeField] private PushButtonView _testButton;

        /// <summary>Raised once when the trainee picks a mode; the panel hides itself.</summary>
        public event Action<TrainingMode> ModeChosen;

        private void Awake()
        {
            if (_learnButton != null) _learnButton.Clicked += () => Choose(TrainingMode.Learn);
            if (_testButton != null) _testButton.Clicked += () => Choose(TrainingMode.Test);
        }

        /// <summary>Shows the panel with the title of the module about to be trained.</summary>
        public void Show(string moduleTitle)
        {
            if (_title != null && !string.IsNullOrEmpty(moduleTitle)) _title.text = moduleTitle;
            gameObject.SetActive(true);
        }

        private void Choose(TrainingMode mode)
        {
            if (!gameObject.activeSelf) return;
            Debug.Log($"[EVTrainer] Start panel: '{mode}' button clicked; hiding panel.");
            gameObject.SetActive(false); // whole panel: buttons, colliders and interactables
            ModeChosen?.Invoke(mode);
        }
    }
}
