using System.Collections.Generic;
using EVTrainer.Core;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace EVTrainer.XR
{
    /// <summary>
    /// Reads one hand from the XR Hands subsystem and publishes a plain <see cref="InputFrame"/>
    /// (source = Hand, pinch, grip, palm pose) plus world-space joint positions for visuals.
    /// Also acts as the select input for this hand's XRI interactor (pinch or grip = select),
    /// and moves this transform to the grab point so the interactor follows the hand.
    /// </summary>
    [DefaultExecutionOrder(-29980)] // after XR input poses are applied, well before XRI's interaction manager (-105)
    public sealed class HandTrackingAdapter : MonoBehaviour, IXRInputButtonReader
    {
        [SerializeField] private Handedness _handedness = Handedness.Left;
        [Tooltip("Parent of tracked poses: the XR Origin's Camera Offset. Hand joint poses are relative to it.")]
        [SerializeField] private Transform _trackingSpace;
        [Header("Gesture thresholds (metres)")]
        [SerializeField] private float _pinchClosed = 0.015f;
        [SerializeField] private float _pinchOpen = 0.06f;
        [SerializeField] private float _curlClosed = 0.045f;
        [SerializeField] private float _curlOpen = 0.095f;
        [Header("Select (grab) thresholds on max(pinch, grip)")]
        [SerializeField, Range(0f, 1f)] private float _selectPress = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _selectRelease = 0.5f;
        [Tooltip("A grab only ends after the hand has stayed open this long (stops flicker re-grabs).")]
        [SerializeField, Range(0f, 0.5f)] private float _releaseHoldSeconds = 0.1f;
        [Header("Grab pose smoothing (hands only; controllers stay 1:1) - One Euro filter")]
        [SerializeField, Min(0.01f)] private float _positionMinCutoffHz = 1.5f;
        [SerializeField, Min(0f)] private float _positionBeta = 4f;
        [SerializeField, Min(0.01f)] private float _rotationMinCutoffHz = 1.5f;
        [SerializeField, Min(0f)] private float _rotationBeta = 0.6f;

        private static readonly List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();

        private readonly System.Numerics.Vector3[] _joints = new System.Numerics.Vector3[HandSkeleton.JointCount];
        private XRHandSubsystem _subsystem;
        private ButtonHysteresis _select;
        private ReleaseDebounce _debounce;
        private OneEuroPoseFilter _filter;
        private bool _grabbing;           // debounced select state
        private bool _grabAtPinch;        // grab point chosen when the grab started (pinch point or palm), kept while held
        private HandGestureSettings _settings;
        private float _selectValue;
        private int _pressedFrame = -1;
        private int _releasedFrame = -1;

        public Handedness Handedness => _handedness;

        /// <summary>Latest input frame for this hand. Untracked when the hand is not tracked.</summary>
        public InputFrame Current { get; private set; } = Untracked;

        /// <summary>True while this hand holds a grab (debounced).</summary>
        public bool IsGrabbing => _grabbing;

        /// <summary>Smoothed minus raw grab point (world): the hand mesh is shifted by this while holding, so it moves with the part.</summary>
        public Vector3 GrabSmoothingOffset { get; private set; }

        /// <summary>World-space joint positions indexed by <see cref="HandJoint"/>. Valid only while tracked.</summary>
        public IReadOnlyList<System.Numerics.Vector3> JointPositions => _joints;

        private static InputFrame Untracked => new InputFrame(InputSource.Hand, 0f, 0f, 0f, PartPose.Identity, isTracked: false);

        private void Awake()
        {
            _settings = new HandGestureSettings(_pinchClosed, _pinchOpen, _curlClosed, _curlOpen);
            _select = new ButtonHysteresis(_selectPress, _selectRelease);
            _debounce = new ReleaseDebounce(_releaseHoldSeconds);
            _filter = new OneEuroPoseFilter(new OneEuroSettings(_positionMinCutoffHz, _positionBeta),
                new OneEuroSettings(_rotationMinCutoffHz, _rotationBeta));
        }

        private void OnDisable() => SetUntracked();

        private void Update()
        {
            if (!TryGetHand(out var hand) || !hand.isTracked || !ReadJoints(hand, out var palmPose))
            {
                SetUntracked();
                return;
            }

            float pinch = HandGesture.Pinch(J(HandJoint.ThumbTip), J(HandJoint.IndexTip), _settings);
            float grip = HandGesture.Grip(J(HandJoint.Palm), J(HandJoint.MiddleTip), J(HandJoint.RingTip), J(HandJoint.LittleTip), _settings);

            _selectValue = Mathf.Max(pinch, grip);
            _select.Update(_selectValue);
            bool wasGrabbing = _grabbing;
            _grabbing = _debounce.Update(_select.IsPressed, Time.timeAsDouble);
            if (_grabbing && !wasGrabbing) { _pressedFrame = Time.frameCount; _grabAtPinch = pinch >= grip; }
            if (!_grabbing && wasGrabbing) _releasedFrame = Time.frameCount;

            // Grab point: between thumb and index tips when pinching, otherwise the palm. While a grab lasts the choice is
            // kept (switching mid-grab would jump the held part by centimetres), then the pose is One Euro filtered.
            var pinchPoint = (J(HandJoint.ThumbTip) + J(HandJoint.IndexTip)) * 0.5f;
            bool atPinch = _grabbing ? _grabAtPinch : pinch >= grip;
            var grabPoint = atPinch ? pinchPoint : J(HandJoint.Palm);
            _filter.Update(grabPoint, palmPose.rotation.ToNumerics(), Time.deltaTime);
            transform.SetPositionAndRotation(_filter.Position.ToUnity(), _filter.Rotation.ToUnity());
            GrabSmoothingOffset = (_filter.Position - grabPoint).ToUnity();

            Current = new InputFrame(InputSource.Hand, grip, 0f, pinch,
                new PartPose(palmPose.position.ToNumerics(), palmPose.rotation.ToNumerics()), isTracked: true);
        }

        private System.Numerics.Vector3 J(HandJoint joint) => _joints[(int)joint];

        private bool TryGetHand(out XRHand hand)
        {
            hand = default;
            if (_subsystem == null || !_subsystem.running)
            {
                _subsystem = null;
                SubsystemManager.GetSubsystems(s_Subsystems);
                foreach (var s in s_Subsystems)
                {
                    if (s.running) { _subsystem = s; break; }
                }
                if (_subsystem == null) return false;
            }

            hand = _handedness == Handedness.Right ? _subsystem.rightHand : _subsystem.leftHand;
            return true;
        }

        /// <summary>Copies all joint poses to world space. Returns false if any key joint is missing.</summary>
        private bool ReadJoints(XRHand hand, out Pose palmWorld)
        {
            palmWorld = default;
            var space = _trackingSpace;
            for (int i = 0; i < HandSkeleton.JointCount; i++)
            {
                // XR Hands joint ids are Wrist..LittleTip, mapped to 0..25 by ToIndex/FromIndex (same order as HandJoint).
                var joint = hand.GetJoint(XRHandJointIDUtility.FromIndex(i));
                if (!joint.TryGetPose(out var pose))
                {
                    if (IsKeyJoint(i)) return false;
                    continue;
                }

                var world = space != null ? pose.GetTransformedBy(space) : pose;
                _joints[i] = world.position.ToNumerics();
                if (i == (int)HandJoint.Palm) palmWorld = world;
            }
            return true;
        }

        private static bool IsKeyJoint(int i) =>
            i == (int)HandJoint.Palm || i == (int)HandJoint.ThumbTip || i == (int)HandJoint.IndexTip ||
            i == (int)HandJoint.MiddleTip || i == (int)HandJoint.RingTip || i == (int)HandJoint.LittleTip;

        private void SetUntracked()
        {
            if (_select != null) _select.Reset();
            _debounce?.Reset();
            _filter?.Reset();
            if (_grabbing) _releasedFrame = Time.frameCount; // tracking lost: let go now
            _grabbing = false;
            _selectValue = 0f;
            Current = Untracked;
        }

        // ---- IXRInputButtonReader: lets XRI use this hand's pinch/grip as the interactor's select input ----

        public bool ReadIsPerformed() => _grabbing;
        public bool ReadWasPerformedThisFrame() => _pressedFrame == Time.frameCount;
        public bool ReadWasCompletedThisFrame() => _releasedFrame == Time.frameCount;
        public float ReadValue() => _selectValue;

        public bool TryReadValue(out float value)
        {
            value = _selectValue;
            return Current.IsTracked;
        }
    }
}
