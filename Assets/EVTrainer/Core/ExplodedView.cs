using System;
using System.Collections.Generic;

namespace EVTrainer.Core
{
    /// <summary>One step of the reverse procedure: the parts of one assembly step, or one bolt set (all its bolts out).</summary>
    public sealed class DisassemblyStep
    {
        private static readonly IReadOnlyList<PartSpec> NoSlots = Array.Empty<PartSpec>();

        internal DisassemblyStep(int index, string name, IReadOnlyList<PartSpec> slots, BoltSetSpec boltSet)
        {
            Index = index;
            Name = name;
            Slots = slots ?? NoSlots;
            BoltSet = boltSet;
        }

        /// <summary>0 = first thing to come off.</summary>
        public int Index { get; }

        /// <summary>Step name for the trainee, e.g. "Remove cover bolts".</summary>
        public string Name { get; }

        /// <summary>Part slots removed in this step (empty for a bolt step).</summary>
        public IReadOnlyList<PartSpec> Slots { get; }

        /// <summary>The bolt set removed in this step, or null.</summary>
        public BoltSetSpec BoltSet { get; }

        public bool IsBolts => BoltSet != null;
    }

    /// <summary>
    /// The procedure in reverse, for the disassembly review and the exploded-view layering: bolt sets come out as
    /// one step (hand-start and torque are one job in reverse), then each parts step from last to first.
    /// Pre-installed parts (the tray on the fixture) stay.
    /// </summary>
    public static class DisassemblySequence
    {
        public static IReadOnlyList<DisassemblyStep> Build(AssemblyDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var steps = new List<DisassemblyStep>();
            var setsDone = new HashSet<string>(StringComparer.Ordinal);
            for (int i = definition.Steps.Count - 1; i >= 0; i--)
            {
                var step = definition.Steps[i];
                if (step.Kind == StepKind.Parts)
                {
                    if (step.IsPreInstalled || step.Slots.Count == 0) continue;
                    steps.Add(new DisassemblyStep(steps.Count, "Remove " + step.Slots[0].GroupName, step.Slots, null));
                }
                else if (setsDone.Add(step.BoltSet.Id))
                {
                    steps.Add(new DisassemblyStep(steps.Count, "Remove " + step.BoltSet.GroupName, null, step.BoltSet));
                }
            }
            return steps.AsReadOnly();
        }
    }

    /// <summary>Animation timing for the exploded view and the disassembly review (module data).</summary>
    public readonly struct ExplodeTiming
    {
        public ExplodeTiming(float moveSeconds, float staggerSeconds, float stepSeconds, float pauseSeconds)
        {
            if (float.IsNaN(moveSeconds) || moveSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(moveSeconds));
            if (float.IsNaN(staggerSeconds) || staggerSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(staggerSeconds));
            if (float.IsNaN(stepSeconds) || stepSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            if (float.IsNaN(pauseSeconds) || pauseSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(pauseSeconds));
            MoveSeconds = moveSeconds;
            StaggerSeconds = staggerSeconds;
            StepSeconds = stepSeconds;
            PauseSeconds = pauseSeconds;
        }

        /// <summary>Exploded view: time for one layer to move out (or back in).</summary>
        public float MoveSeconds { get; }

        /// <summary>Exploded view: delay between consecutive layers.</summary>
        public float StaggerSeconds { get; }

        /// <summary>Disassembly review: time for one step's parts to come off.</summary>
        public float StepSeconds { get; }

        /// <summary>Disassembly review: hold after each step so its name can be read.</summary>
        public float PauseSeconds { get; }
    }

    /// <summary>
    /// How far each layer is moved out (0 = assembled, 1 = fully exploded) at a time since the animation started.
    /// Layer 0 is the first to come off (the bolts), the last layer the first part fitted.
    /// Exploded view: layers start one stagger apart, top first; assembling runs the other way, bottom first.
    /// Disassembly review: one layer at a time, each followed by a pause.
    /// Moves ease in and out (smoothstep), so nothing jumps.
    /// </summary>
    public sealed class ExplodeTimeline
    {
        public ExplodeTimeline(int layers, ExplodeTiming timing)
        {
            if (layers < 1) throw new ArgumentOutOfRangeException(nameof(layers));
            Layers = layers;
            Timing = timing;
        }

        public int Layers { get; }
        public ExplodeTiming Timing { get; }

        /// <summary>Whole exploded-view (or assemble) animation.</summary>
        public float ExplodeSeconds => Timing.MoveSeconds + (Layers - 1) * Timing.StaggerSeconds;

        /// <summary>Whole disassembly review, including the pause after the last step.</summary>
        public float DisassemblySeconds => Layers * (Timing.StepSeconds + Timing.PauseSeconds);

        /// <summary>Exploded view: amount of a layer at <paramref name="seconds"/> after "Exploded view" was pressed.</summary>
        public float ExplodeAmount(int layer, float seconds) =>
            Ease((seconds - Check(layer) * Timing.StaggerSeconds) / Timing.MoveSeconds);

        /// <summary>Assemble: amount of a layer at <paramref name="seconds"/> after "Assemble" was pressed (1 → 0).</summary>
        public float AssembleAmount(int layer, float seconds) =>
            1f - Ease((seconds - (Layers - 1 - Check(layer)) * Timing.StaggerSeconds) / Timing.MoveSeconds);

        /// <summary>Disassembly review: the step being shown, or <see cref="Layers"/> once the review has finished.</summary>
        public int DisassemblyStepAt(float seconds)
        {
            if (float.IsNaN(seconds) || seconds < 0f) return 0;
            int step = (int)(seconds / (Timing.StepSeconds + Timing.PauseSeconds));
            return Math.Min(step, Layers);
        }

        /// <summary>Disassembly review: amount of a layer (it moves only during its own step).</summary>
        public float DisassemblyAmount(int layer, float seconds) =>
            Ease((seconds - Check(layer) * (Timing.StepSeconds + Timing.PauseSeconds)) / Timing.StepSeconds);

        /// <summary>Smoothstep of x clamped to 0..1.</summary>
        public static float Ease(float x)
        {
            if (float.IsNaN(x) || x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            return x * x * (3f - 2f * x);
        }

        private int Check(int layer)
        {
            if (layer < 0 || layer >= Layers) throw new ArgumentOutOfRangeException(nameof(layer));
            return layer;
        }
    }
}
