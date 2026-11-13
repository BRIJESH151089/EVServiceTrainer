using System;

namespace EVTrainer.Core
{
    /// <summary>What happens to a hand tool (the torque wrench) when it is let go. Tools never fall.</summary>
    public enum ToolRelease
    {
        /// <summary>Let go while on a bolt that is not finished: it stays on the bolt; grab the handle to continue.</summary>
        ParkOnBolt,
        /// <summary>Let go in the air within reach: it stays exactly where it is (no gravity).</summary>
        FloatInPlace,
        /// <summary>Let go below the bench top or out of reach: it glides back to its place on the stand.</summary>
        ReturnHome,
    }

    /// <summary>What happens to a bolt that is let go.</summary>
    public enum BoltRelease
    {
        /// <summary>It went into a hole (started or cross-threaded) and stays there.</summary>
        StayInHole,
        /// <summary>Loose: it glides back to its slot in the bolt tray (bolts never fall).</summary>
        ReturnToTray,
    }

    /// <summary>"No dropping" rules for tools and bolts.</summary>
    public static class ToolReleaseRule
    {
        /// <param name="attachedToBolt">The wrench was on a bolt when let go.</param>
        /// <param name="boltFinished">That bolt is torqued / over-torqued.</param>
        /// <param name="releaseHeight">Height of the wrench when let go (metres, world up).</param>
        /// <param name="minFloatHeight">Below this (the bench top) it is put away instead of floating.</param>
        /// <param name="distanceFromUser">Distance from the trainee's head when let go.</param>
        /// <param name="maxFloatDistance">Further than this it is put away instead of floating.</param>
        public static ToolRelease ForWrench(bool attachedToBolt, bool boltFinished, float releaseHeight, float minFloatHeight,
            float distanceFromUser, float maxFloatDistance)
        {
            if (attachedToBolt && !boltFinished) return ToolRelease.ParkOnBolt;
            if (float.IsNaN(releaseHeight) || float.IsNaN(distanceFromUser)) return ToolRelease.ReturnHome;
            if (releaseHeight < minFloatHeight || distanceFromUser > maxFloatDistance) return ToolRelease.ReturnHome;
            return ToolRelease.FloatInPlace;
        }

        public static BoltRelease ForBolt(bool droppedIntoHole) => droppedIntoHole ? BoltRelease.StayInHole : BoltRelease.ReturnToTray;
    }
}
