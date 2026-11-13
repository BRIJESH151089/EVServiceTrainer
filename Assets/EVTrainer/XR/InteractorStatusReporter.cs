using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace EVTrainer.XR
{
    /// <summary>
    /// Diagnostics: what each interactor (grab and poke) is hovering / selecting, as plain text for the debug panel.
    /// </summary>
    public sealed class InteractorStatusReporter : MonoBehaviour
    {
        [Tooltip("Interactors to report. Empty = every interactor in the scene at start.")]
        [SerializeField] private XRBaseInteractor[] _interactors;

        private readonly StringBuilder _sb = new StringBuilder(512);

        private void Awake()
        {
            if (_interactors == null || _interactors.Length == 0)
                _interactors = FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(_interactors, (a, b) => string.CompareOrdinal(Label(a), Label(b)));
        }

        /// <summary>One line per interactor, e.g. "Right Controller grab: hover Cooling Plate | holds -".</summary>
        public string Describe()
        {
            _sb.Clear();
            foreach (var i in _interactors)
            {
                if (i == null) continue;
                _sb.Append(Label(i)).Append(": ");
                if (!i.isActiveAndEnabled)
                {
                    _sb.AppendLine("inactive");
                    continue;
                }
                _sb.Append("hover ").Append(Names(i.interactablesHovered.Count, k => i.interactablesHovered[k]))
                   .Append(" | holds ").Append(Names(i.interactablesSelected.Count, k => i.interactablesSelected[k]))
                   .AppendLine();
            }
            return _sb.ToString();
        }

        private static string Label(XRBaseInteractor i) => i is XRPokeInteractor ? i.name : $"{i.name} grab";

        private static string Names(int count, System.Func<int, IXRInteractable> get)
        {
            if (count == 0) return "-";
            var s = get(0).transform.name;
            return count == 1 ? s : $"{s} +{count - 1}";
        }
    }
}
