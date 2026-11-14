using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    /// <summary>Controller poke point from the grip pose (world: +X right, +Y up, +Z forward).</summary>
    public class ControllerPokePointTests
    {
        private static readonly Vector3 Chest = new Vector3(0.2f, 1.2f, 0.3f);
        private static PartPose Held => GripPose.HeldStraightAhead(Chest);
        private static GloveMount Mount(HandSide side) => GloveMount.Fit(side, GloveMount.DefaultGripPointInGlove);

        private static Vector3 PokeWorld(HandSide side)
        {
            var local = ControllerPokePoint.PositionInGrip(side, Mount(side));
            return Held.Position + Vector3.Transform(local, Held.Rotation);
        }

        private static Vector3 Knuckle(HandSide side) =>
            Mount(side).GlovePointToWorld(Held, GloveHandLayout.Joint("IndexProximal", side));

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void HeldStraightAhead_PokePointIsInFrontOfTheKnuckles_AtIndexHeight(HandSide side)
        {
            var poke = PokeWorld(side);
            var knuckle = Knuckle(side);
            Assert.That(poke.Z - knuckle.Z, Is.GreaterThan(0.07f), "a finger length in front of the index knuckle");
            Assert.That(poke.Z - Chest.Z, Is.GreaterThan(0.08f), "well in front of the grip centre");
            Assert.That(System.Math.Abs(poke.Y - knuckle.Y), Is.LessThan(0.03f), "at index height");
        }

        [Test]
        public void PokePoint_IsTheExtendedIndexFingertip_NotTheCurledOne()
        {
            var mount = Mount(HandSide.Left);
            var identity = new PartPose(Vector3.Zero, Quaternion.Identity);
            var extended = mount.GlovePointToWorld(identity, GloveHandLayout.CurledTip(Finger.Index, 0f, HandSide.Left));
            var curled = mount.GlovePointToWorld(identity, GloveHandLayout.CurledTip(Finger.Index, 1f, HandSide.Left));
            var poke = ControllerPokePoint.PositionInGrip(HandSide.Left, mount);
            Assert.That(Vector3.Distance(poke, extended), Is.LessThan(ControllerPokePoint.DefaultPadMetres + 1e-4f));
            Assert.That(Vector3.Distance(poke, curled), Is.GreaterThan(0.05f), "grip/trigger curl never moves the poke point");
        }

        [Test]
        public void PokePoints_AreMirrorImages()
        {
            var l = ControllerPokePoint.PositionInGrip(HandSide.Left, Mount(HandSide.Left));
            var r = ControllerPokePoint.PositionInGrip(HandSide.Right, Mount(HandSide.Right));
            Assert.That(r.X, Is.EqualTo(-l.X).Within(1e-5f));
            Assert.That(r.Y, Is.EqualTo(l.Y).Within(1e-5f));
            Assert.That(r.Z, Is.EqualTo(l.Z).Within(1e-5f));
        }

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void PokeRotation_PointsAlongTheIndexFinger_Forward(HandSide side)
        {
            var q = Quaternion.Concatenate(ControllerPokePoint.RotationInGrip(side, Mount(side)), Held.Rotation);
            Assert.That(Vector3.Dot(Vector3.Transform(Vector3.UnitZ, q), Vector3.UnitZ), Is.GreaterThan(0.9f), "pokes forward");
        }

        [Test]
        public void PointingPose_IndexStraight_OthersClosed()
        {
            var p = ControllerHandPose.Pointing;
            Assert.That(p.Index, Is.EqualTo(0f));
            Assert.That(p.Middle, Is.EqualTo(1f));
            Assert.That(p.Ring, Is.EqualTo(1f));
            Assert.That(p.Little, Is.EqualTo(1f));
        }
    }
}
