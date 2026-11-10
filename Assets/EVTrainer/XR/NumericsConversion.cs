using EVTrainer.Core;
using UnityEngine;

namespace EVTrainer.XR
{
    /// <summary>Converts Unity maths types to the plain System.Numerics types used by Core.</summary>
    public static class NumericsConversion
    {
        public static System.Numerics.Vector3 ToNumerics(this Vector3 v) =>
            new System.Numerics.Vector3(v.x, v.y, v.z);

        public static System.Numerics.Quaternion ToNumerics(this Quaternion q) =>
            new System.Numerics.Quaternion(q.x, q.y, q.z, q.w);

        public static Vector3 ToUnity(this System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        public static Quaternion ToUnity(this System.Numerics.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);

        /// <summary>World pose of a transform as a Core <see cref="PartPose"/>.</summary>
        public static PartPose ToPartPose(this Transform t) =>
            new PartPose(t.position.ToNumerics(), t.rotation.ToNumerics());
    }
}
