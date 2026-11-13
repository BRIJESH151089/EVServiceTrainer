using System;
using System.Collections.Generic;
using EVTrainer.Core;
using EVTrainer.XR;
using UnityEngine;

namespace EVTrainer.App
{
    /// <summary>
    /// Authoring data for one training module (e.g. the battery pack). Target poses are relative
    /// to the module's assembly origin (the fixture), so the same data works wherever the fixture
    /// is placed. Converted into a plain Core <see cref="AssemblyDefinition"/> at start-up.
    /// </summary>
    [CreateAssetMenu(menuName = "EV Trainer/Assembly Module", fileName = "AssemblyModule")]
    public sealed class AssemblyModuleDefinition : ScriptableObject
    {
        /// <summary>One SLOT. Identical parts share a type id and can go into any free slot of that type.</summary>
        [Serializable]
        public sealed class PartEntry
        {
            [Tooltip("Slot id, unique in the module (e.g. module_2).")]
            public string id;
            [Tooltip("Part type. Empty = the slot id (a unique part). Identical parts share a type (e.g. battery_module).")]
            public string typeId;
            public string displayName;
            [Tooltip("Name of the part type in sentences, e.g. \"battery modules\". Empty = display name.")]
            public string groupName;
            [Tooltip("Step text in the HUD, shared by consecutive slots of one type. Empty = \"Fit <display name>\".")]
            public string instruction;
            public PartSymmetry symmetry;
            [Tooltip("Part name on the Learn-mode label, without a slot number (e.g. \"Battery module\"). Empty = display name.")]
            public string partName;
            [Tooltip("One-line Learn-mode hint on the part label (e.g. \"+ terminal faces front\").")]
            public string hint;
            [Tooltip("Target position relative to the assembly origin (metres).")]
            public Vector3 localPosition;
            [Tooltip("Target rotation relative to the assembly origin (Euler degrees).")]
            public Vector3 localEulerAngles;
            [Min(0f)] public float positionToleranceMetres = 0.015f;
            [Range(0f, 180f)] public float angleToleranceDegrees = 8f;
            public bool requiresTwoHands;
            [Tooltip("Already in place when the module starts (e.g. the tray on the fixture).")]
            public bool preInstalled;
            [Tooltip("Ids of parts that must be installed first.")]
            public string[] prerequisites = Array.Empty<string>();
        }

        /// <summary>One threaded hole, relative to the part the bolt set fastens (its installed target pose).</summary>
        [Serializable]
        public sealed class HoleEntry
        {
            public string id;
            [Tooltip("Hole centre on the part's surface, relative to the part's target pose (metres).")]
            public Vector3 localPosition;
            [Tooltip("Hole orientation relative to the part; the bolt axis is local +Y (out of the hole).")]
            public Vector3 localEulerAngles;
        }

        /// <summary>Bolts that fasten one part (e.g. the 8 cover bolts), the tightening pattern and the spec.</summary>
        [Serializable]
        public sealed class BoltSetEntry
        {
            public string id;
            [Tooltip("Part id the bolts fasten; bolts become available once it is installed. Hole positions are relative to it.")]
            public string prerequisitePartId;
            [Tooltip("Name in sentences, e.g. \"cover bolts\".")]
            public string groupName;
            public string handStartInstruction = "Hand-start bolts";
            public string torqueInstruction = "Torque bolts in pattern";
            public HoleEntry[] holes = Array.Empty<HoleEntry>();
            [Tooltip("Hole ids in the required tightening order (e.g. a cross pattern).")]
            public string[] patternOrder = Array.Empty<string>();
            [Min(0.1f)] public float threadTurnsToSeat = 3f;
            [Min(0.1f)] public float torqueSpecNm = 8f;
            [Min(0f)] public float torqueToleranceNm = 0.4f;
            [Min(0.001f)] public float stiffnessNmPerDegree = 0.1f;
            [Range(0f, 45f)] public float maxStartTiltDegrees = 10f;
            [Tooltip("Bolt degrees per degree of wrist twist when threading by hand.")]
            [Min(0.1f)] public float handTurnGearing = 4f;
            [Header("Bolt drop-in (capture zone around a free hole, per mode)")]
            [Min(0.005f)] public float learnBoltCaptureMetres = 0.06f;
            [Range(0f, 60f)] public float learnBoltCaptureDegrees = 30f;
            [Min(0.005f)] public float testBoltCaptureMetres = 0.03f;
            [Range(0f, 60f)] public float testBoltCaptureDegrees = 15f;
            [Tooltip("Learn: a bolt dropped in crooked (within the capture angle) is straightened instead of cross-threading.")]
            public bool straightenInLearn = true;
            [Tooltip("Learn tip shown when a crooked bolt was straightened.")]
            public string straightenTip = "In a real job, keep the bolt straight or it cross-threads.";
            [Header("Torque wrench")]
            [Tooltip("The socket mouth attaches within this distance of the bolt axis...")]
            [Min(0.005f)] public float wrenchAttachRadiusMetres = 0.03f;
            [Tooltip("...from this far above the top of the bolt head...")]
            [Min(0f)] public float wrenchAttachMaxAboveMetres = 0.04f;
            [Tooltip("...to this far below it (socket pushed over the head)...")]
            [Min(0f)] public float wrenchAttachMaxBelowMetres = 0.03f;
            [Tooltip("...with the socket within this angle of the bolt axis.")]
            [Range(0f, 90f)] public float wrenchAttachMaxTiltDegrees = 45f;
            [Tooltip("Moving the holding hand this far off the swing plane takes the wrench off.")]
            [Min(0.02f)] public float wrenchDetachOffPlaneMetres = 0.15f;
            [Tooltip("Soft ratchet click every this many degrees of back-swing.")]
            [Min(1f)] public float ratchetClickDegrees = 6f;
            [Tooltip("Torque ring: a full ring is this many times the upper torque limit.")]
            [Min(1f)] public float torqueRingFullScale = 1.25f;
            [Header("Tools never fall")]
            [Tooltip("Let go further than this from the trainee's head, the wrench glides back to its stand instead of floating.")]
            [Min(0.2f)] public float toolFloatMaxDistanceMetres = 1f;
            [Tooltip("Seconds for the wrench (to its stand) or a loose bolt (to the tray) to glide back.")]
            [Min(0.05f)] public float toolReturnSeconds = 0.6f;
            [Header("Learn-mode labels")]
            public string boltName = "Bolt";
            public string boltHint;
            public string wrenchName = "Torque wrench";
            public string wrenchHint;
        }

        [SerializeField] private string _id = "module";
        [SerializeField] private string _title = "Module";
        [Header("Snap assist (capture zone: released within it, a part or bolt is pulled into its exact pose)")]
        [SerializeField, Min(0f)] private float _learnCaptureMetres = 0.05f;
        [SerializeField, Range(0f, 90f)] private float _learnCaptureDegrees = 25f;
        [SerializeField, Min(0f)] private float _testCaptureMetres = 0.03f;
        [SerializeField, Range(0f, 90f)] private float _testCaptureDegrees = 15f;
        [SerializeField] private List<PartEntry> _parts = new List<PartEntry>();
        [SerializeField] private List<BoltSetEntry> _boltSets = new List<BoltSetEntry>();

        public string Id => _id;
        public string Title => _title;
        public IReadOnlyList<PartEntry> Parts => _parts;
        public IReadOnlyList<BoltSetEntry> BoltSets => _boltSets;

        /// <summary>Builds the Core definition with target poses in world space, placed by <paramref name="origin"/>.</summary>
        public AssemblyDefinition ToCore(Transform origin)
        {
            if (origin == null) throw new ArgumentNullException(nameof(origin));

            var specs = new List<PartSpec>(_parts.Count);
            var targets = new Dictionary<string, (Vector3 position, Quaternion rotation)>();
            foreach (var p in _parts)
            {
                var worldPosition = origin.TransformPoint(p.localPosition);
                var worldRotation = origin.rotation * Quaternion.Euler(p.localEulerAngles);
                targets[p.id] = (worldPosition, worldRotation);
                specs.Add(new PartSpec(
                    p.id, p.displayName,
                    new PartPose(worldPosition.ToNumerics(), worldRotation.ToNumerics()),
                    p.positionToleranceMetres, p.angleToleranceDegrees,
                    p.requiresTwoHands, p.prerequisites, p.preInstalled, p.instruction,
                    p.typeId, p.groupName, p.symmetry, p.partName, p.hint));
            }

            var sets = new List<BoltSetSpec>(_boltSets.Count);
            foreach (var s in _boltSets)
            {
                // Holes ride on the fastened part's target pose (falls back to the assembly origin).
                var basePosition = origin.position;
                var baseRotation = origin.rotation;
                if (!string.IsNullOrEmpty(s.prerequisitePartId) && targets.TryGetValue(s.prerequisitePartId, out var t))
                {
                    basePosition = t.position;
                    baseRotation = t.rotation;
                }
                var bolts = new List<BoltSpec>(s.holes.Length);
                foreach (var h in s.holes)
                {
                    var position = basePosition + baseRotation * h.localPosition;
                    var rotation = baseRotation * Quaternion.Euler(h.localEulerAngles);
                    bolts.Add(new BoltSpec(h.id, new PartPose(position.ToNumerics(), rotation.ToNumerics()),
                        s.threadTurnsToSeat, s.torqueSpecNm, s.torqueToleranceNm, s.stiffnessNmPerDegree, s.maxStartTiltDegrees,
                        s.handTurnGearing));
                }
                sets.Add(new BoltSetSpec(s.id, string.IsNullOrEmpty(s.prerequisitePartId) ? null : s.prerequisitePartId,
                    bolts, s.patternOrder, s.handStartInstruction, s.torqueInstruction, s.groupName,
                    new WrenchSpec(s.wrenchAttachRadiusMetres, s.wrenchAttachMaxAboveMetres, s.wrenchAttachMaxBelowMetres,
                        s.wrenchAttachMaxTiltDegrees, s.wrenchDetachOffPlaneMetres, s.ratchetClickDegrees),
                    new SnapAssist(new CaptureZone(s.learnBoltCaptureMetres, s.learnBoltCaptureDegrees),
                        new CaptureZone(s.testBoltCaptureMetres, s.testBoltCaptureDegrees)),
                    s.straightenInLearn));
            }
            var snapAssist = new SnapAssist(new CaptureZone(_learnCaptureMetres, _learnCaptureDegrees),
                new CaptureZone(_testCaptureMetres, _testCaptureDegrees));
            return new AssemblyDefinition(_id, _title, specs, sets, snapAssist);
        }
    }
}
