using System.Numerics;

namespace EVTrainer.Core
{
    /// <summary>A rigid pose (position in metres + rotation) in plain System.Numerics types.</summary>
    public readonly struct PartPose
    {
        public PartPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }

        public static PartPose Identity => new PartPose(Vector3.Zero, Quaternion.Identity);

        public override string ToString() => $"PartPose(pos={Position}, rot={Rotation})";
    }
}
