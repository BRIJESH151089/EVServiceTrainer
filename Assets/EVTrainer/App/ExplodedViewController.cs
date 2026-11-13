using System.Collections.Generic;
using EVTrainer.Core;
using EVTrainer.UI;
using EVTrainer.XR;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// After "Module complete" (both modes): "Exploded view" moves the installed parts apart along their explode
    /// offsets (module data) with name labels, and "Assemble" brings them back. Learn only: "Watch disassembly" plays
    /// the procedure in reverse, one step at a time with the step name on the info panel (Core
    /// <see cref="DisassemblySequence"/> and <see cref="ExplodeTimeline"/>). Parts are posed by this script alone:
    /// colliders off and nothing grabbable while apart, so no physics touches them.
    /// </summary>
    public sealed class ExplodedViewController : MonoBehaviour
    {
        [SerializeField] private AssemblyTrainingController _training;
        [SerializeField] private BoltingController _bolting;
        [SerializeField] private AssemblyModuleDefinition _module;
        [Tooltip("Assembly origin (fixture top centre): explode offsets are in its axes.")]
        [SerializeField] private Transform _assemblyOrigin;
        [Tooltip("Row holding the review buttons (with its backplate); shown once the module is complete.")]
        [SerializeField] private GameObject _buttonRow;
        [SerializeField] private PushButtonView _explodeButton;
        [SerializeField] private PushButtonView _assembleButton;
        [Tooltip("Learn mode only.")]
        [SerializeField] private PushButtonView _disassemblyButton;
        [Tooltip("Template for the name labels shown while the assembly is apart.")]
        [SerializeField] private PartLabelView _labelTemplate;
        [SerializeField, Range(0.2f, 2f)] private float _labelScale = 0.7f;
        [SerializeField, Min(0f)] private float _labelGapMetres = 0.03f;

        private enum State { Hidden, Assembled, Exploding, Exploded, Assembling, Disassembling }

        private sealed class Item
        {
            public Transform Transform;
            public Vector3 Home;
            public Vector3 Offset;
            public int Layer;
            public Collider[] Colliders;
            public bool[] CollidersEnabled; // as they were before the review
            public Renderer[] Renderers;
        }

        private readonly List<Item> _items = new List<Item>();
        private readonly List<PartLabelView> _labels = new List<PartLabelView>();
        private readonly List<System.Numerics.Vector3> _labelAnchors = new List<System.Numerics.Vector3>();
        private readonly List<string> _labelTexts = new List<string>();
        private IReadOnlyList<DisassemblyStep> _steps;
        private ExplodeTimeline _timeline;
        private State _state = State.Hidden;
        private float _startTime;
        private int _shownStep = -1;
        private bool _labelsShown;

        private void Awake()
        {
            SetButtons(State.Hidden);
            if (_labelTemplate != null) _labelTemplate.Hide();
        }

        private void OnEnable()
        {
            if (_training != null) _training.Completed += OnCompleted;
            if (_explodeButton != null) _explodeButton.Clicked += OnExplode;
            if (_assembleButton != null) _assembleButton.Clicked += OnAssemble;
            if (_disassemblyButton != null) _disassemblyButton.Clicked += OnDisassembly;
        }

        private void OnDisable()
        {
            if (_training != null) _training.Completed -= OnCompleted;
            if (_explodeButton != null) _explodeButton.Clicked -= OnExplode;
            if (_assembleButton != null) _assembleButton.Clicked -= OnAssemble;
            if (_disassemblyButton != null) _disassemblyButton.Clicked -= OnDisassembly;
        }

        private void OnCompleted()
        {
            _steps = DisassemblySequence.Build(_training.Progress.Definition);
            if (_steps.Count == 0) return;
            _timeline = new ExplodeTimeline(_steps.Count, _module.ExplodeTiming);
            Enter(State.Assembled);
        }

        // ---------------- Buttons ----------------

        private void OnExplode() => Begin(State.Exploding);
        private void OnDisassembly() => Begin(State.Disassembling);

        private void OnAssemble()
        {
            if (_state != State.Exploded) return;
            HideLabels();
            _training.RestoreHud();
            Enter(State.Assembling);
        }

        private void Begin(State animation)
        {
            if (_state != State.Assembled) return;
            CollectItems(); // poses as they are now (installed), every time: nothing else moves them
            if (_bolting != null) _bolting.SetReviewing(true);
            SetPhysics(false);
            _shownStep = -1;
            if (animation == State.Exploding) _training.ShowNotice("Exploded view. Press Assemble to put it back together.");
            Enter(animation);
        }

        private void Enter(State state)
        {
            _state = state;
            _startTime = Time.time;
            SetButtons(state);
        }

        private void SetButtons(State state)
        {
            bool learn = _training != null && _training.Progress != null && _training.Progress.Mode == TrainingMode.Learn;
            if (_buttonRow != null && _buttonRow.activeSelf != (state != State.Hidden)) _buttonRow.SetActive(state != State.Hidden);
            SetVisible(_explodeButton, state == State.Assembled);
            SetVisible(_disassemblyButton, state == State.Assembled && learn);
            SetVisible(_assembleButton, state == State.Exploded);
        }

        private static void SetVisible(PushButtonView button, bool visible)
        {
            if (button != null && button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
        }

        // ---------------- Animation ----------------

        private void Update()
        {
            if (_timeline == null) return;
            float t = Time.time - _startTime;
            switch (_state)
            {
                case State.Exploding:
                    foreach (var item in _items) Place(item, _timeline.ExplodeAmount(item.Layer, t));
                    if (t >= _timeline.ExplodeSeconds) { ShowLabels(_steps.Count); Enter(State.Exploded); }
                    break;

                case State.Assembling:
                    foreach (var item in _items) Place(item, _timeline.AssembleAmount(item.Layer, t));
                    if (t >= _timeline.ExplodeSeconds)
                    {
                        SetPhysics(true);
                        if (_bolting != null) _bolting.SetReviewing(false);
                        Enter(State.Assembled);
                    }
                    break;

                case State.Disassembling:
                    foreach (var item in _items) Place(item, _timeline.DisassemblyAmount(item.Layer, t));
                    int step = _timeline.DisassemblyStepAt(t);
                    if (step != _shownStep)
                    {
                        _shownStep = step;
                        if (step < _steps.Count)
                        {
                            _training.ShowNotice($"Disassembly {step + 1}/{_steps.Count}: {_steps[step].Name}");
                            if (step > 0) ShowLabels(step); // names of what has come off so far
                        }
                        else
                        {
                            ShowLabels(_steps.Count);
                            _training.ShowNotice("Disassembly review done. Press Assemble to put it back together.");
                            Enter(State.Exploded);
                        }
                    }
                    break;
            }
        }

        private static void Place(Item item, float amount) => item.Transform.position = item.Home + item.Offset * amount;

        private void SetPhysics(bool on)
        {
            foreach (var item in _items)
                for (int i = 0; i < item.Colliders.Length; i++)
                    if (item.Colliders[i] != null) item.Colliders[i].enabled = on && item.CollidersEnabled[i];
        }

        // ---------------- Items ----------------

        /// <summary>Every installed part (by slot) and every bolt, with its layer (disassembly step) and explode offset.</summary>
        private void CollectItems()
        {
            _items.Clear();
            var used = new HashSet<GrabbablePartAdapter>();
            for (int layer = 0; layer < _steps.Count; layer++)
            {
                var step = _steps[layer];
                foreach (var slot in step.Slots)
                {
                    var part = InstalledPartIn(slot, used);
                    if (part == null) continue;
                    used.Add(part);
                    Add(part.transform, layer, _module.ExplodeOffset(slot.Id, _assemblyOrigin));
                }
                if (!step.IsBolts || _bolting == null) continue;

                // Bolts float out of their holes, above the exploded position of the part they fasten.
                var data = _module.BoltSet(step.BoltSet.Id);
                var baseOffset = step.BoltSet.PrerequisitePartId != null ? _module.ExplodeOffset(step.BoltSet.PrerequisitePartId, _assemblyOrigin) : Vector3.zero;
                float lift = data != null ? data.explodeLiftMetres : 0.08f;
                foreach (var spec in step.BoltSet.Bolts)
                {
                    var bolt = _bolting.BoltInHole(spec.HoleId);
                    if (bolt == null) continue;
                    var axis = spec.HolePose.Rotation.ToUnity() * Vector3.up;
                    Add(bolt.transform, layer, baseOffset + axis * lift);
                }
            }
        }

        private void Add(Transform t, int layer, Vector3 offset)
        {
            var colliders = t.GetComponentsInChildren<Collider>();
            var enabled = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++) enabled[i] = colliders[i].enabled;
            _items.Add(new Item
            {
                Transform = t,
                Home = t.position,
                Offset = offset,
                Layer = layer,
                Colliders = colliders,
                CollidersEnabled = enabled,
                Renderers = t.GetComponentsInChildren<Renderer>(),
            });
        }

        /// <summary>The locked part of the slot's type nearest the slot's target (identical parts can sit in any slot).</summary>
        private GrabbablePartAdapter InstalledPartIn(PartSpec slot, HashSet<GrabbablePartAdapter> used)
        {
            var target = slot.TargetPose.Position.ToUnity();
            GrabbablePartAdapter best = null;
            float bestDistance = 0.05f;
            foreach (var part in _training.Parts)
            {
                if (!part.IsLocked || part.PartTypeId != slot.TypeId || used.Contains(part)) continue;
                float d = Vector3.Distance(part.transform.position, target);
                if (d < bestDistance) { best = part; bestDistance = d; }
            }
            return best;
        }

        // ---------------- Labels ----------------

        /// <summary>One name label per step ("Battery module ×4", "Cover bolts ×8") over the first <paramref name="layers"/> layers.</summary>
        private void ShowLabels(int layers)
        {
            if (_labelTemplate == null) return;
            _labelTexts.Clear();
            _labelAnchors.Clear();
            for (int layer = 0; layer < layers && layer < _steps.Count; layer++)
            {
                var step = _steps[layer];
                var b = BoundsOfLayer(layer, out int count);
                if (count == 0) continue;
                string name = step.IsBolts ? LabelLayout.Capitalise(step.BoltSet.GroupName) : step.Slots[0].PartName;
                _labelTexts.Add(LabelLayout.GroupText(name, count));
                _labelAnchors.Add(new Vector3(b.center.x, b.center.y, b.min.z - _labelGapMetres).ToNumerics()); // in front of the layer, facing the trainee
            }

            var viewer = Camera.main != null ? Camera.main.transform.position : Vector3.up * 1.6f;
            var placed = LabelLayout.Place(_labelAnchors, viewer.ToNumerics());
            for (int i = 0; i < _labelTexts.Count; i++)
            {
                if (i >= _labels.Count)
                {
                    var label = Instantiate(_labelTemplate, _labelTemplate.transform.parent);
                    label.name = "Exploded Label " + i;
                    label.transform.localScale = _labelTemplate.transform.localScale * _labelScale;
                    _labels.Add(label);
                }
                _labels[i].Show(_labelTexts[i], null, placed[i].ToUnity());
            }
            for (int i = _labelTexts.Count; i < _labels.Count; i++) _labels[i].Hide();
            _labelsShown = true;
        }

        private void HideLabels()
        {
            if (!_labelsShown) return;
            foreach (var label in _labels) label.Hide();
            _labelsShown = false;
        }

        private Bounds BoundsOfLayer(int layer, out int count)
        {
            count = 0;
            var b = new Bounds();
            foreach (var item in _items)
            {
                if (item.Layer != layer) continue;
                var ib = Bounds(item);
                if (count++ == 0) b = ib; else b.Encapsulate(ib);
            }
            return b;
        }

        private static Bounds Bounds(Item item)
        {
            var rs = item.Renderers;
            if (rs.Length == 0) return new Bounds(item.Transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
