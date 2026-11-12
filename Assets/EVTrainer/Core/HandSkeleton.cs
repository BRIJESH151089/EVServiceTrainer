using System.Collections.Generic;

namespace EVTrainer.Core
{
    /// <summary>
    /// The 26 OpenXR hand joints, in OpenXR order (index 0 = Wrist).
    /// The XR adapter maps the SDK's joint ids onto these indices.
    /// </summary>
    public enum HandJoint
    {
        Wrist = 0,
        Palm,
        ThumbMetacarpal, ThumbProximal, ThumbDistal, ThumbTip,
        IndexMetacarpal, IndexProximal, IndexIntermediate, IndexDistal, IndexTip,
        MiddleMetacarpal, MiddleProximal, MiddleIntermediate, MiddleDistal, MiddleTip,
        RingMetacarpal, RingProximal, RingIntermediate, RingDistal, RingTip,
        LittleMetacarpal, LittleProximal, LittleIntermediate, LittleDistal, LittleTip,
    }

    /// <summary>Topology of the hand: which joints connect to which.</summary>
    public static class HandSkeleton
    {
        public const int JointCount = 26;

        /// <summary>One chain per finger, starting at the wrist and ending at the fingertip.</summary>
        public static readonly IReadOnlyList<IReadOnlyList<HandJoint>> FingerChains = new HandJoint[][]
        {
            new[] { HandJoint.Wrist, HandJoint.ThumbMetacarpal, HandJoint.ThumbProximal, HandJoint.ThumbDistal, HandJoint.ThumbTip },
            new[] { HandJoint.Wrist, HandJoint.IndexMetacarpal, HandJoint.IndexProximal, HandJoint.IndexIntermediate, HandJoint.IndexDistal, HandJoint.IndexTip },
            new[] { HandJoint.Wrist, HandJoint.MiddleMetacarpal, HandJoint.MiddleProximal, HandJoint.MiddleIntermediate, HandJoint.MiddleDistal, HandJoint.MiddleTip },
            new[] { HandJoint.Wrist, HandJoint.RingMetacarpal, HandJoint.RingProximal, HandJoint.RingIntermediate, HandJoint.RingDistal, HandJoint.RingTip },
            new[] { HandJoint.Wrist, HandJoint.LittleMetacarpal, HandJoint.LittleProximal, HandJoint.LittleIntermediate, HandJoint.LittleDistal, HandJoint.LittleTip },
        };
    }
}
