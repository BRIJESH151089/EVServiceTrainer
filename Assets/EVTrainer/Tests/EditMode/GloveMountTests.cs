using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    /// <summary>
    /// Controller glove placement from the OpenXR grip pose (Unity axes). World: +X right, +Y up, +Z forward (away from
    /// the trainee). "Held straight ahead" = <see cref="GripPose.HeldStraightAhead"/>, the grip pose a Quest reports
    /// for a controller held in front of the chest, fist forward, thumb on top.
    /// </summary>
    public class GloveMountTests
    {
        private static readonly Vector3 Chest = new Vector3(0.2f, 1.2f, 0.3f);

        private static GloveMount Mount(HandSide side) => GloveMount.Fit(side, GloveMount.DefaultGripPointInGlove);

        private static Vector3 World(HandSide side, string joint, PartPose grip) =>
            Mount(side).GlovePointToWorld(grip, GloveHandLayout.Joint(joint, side));

        private static PartPose Held => GripPose.HeldStraightAhead(Chest);

        [Test]
        public void HeldStraightAhead_GripAxesFollowTheOpenXRDefinition()
        {
            var g = Held.Rotation;
            Assert.That(Vector3.Dot(Vector3.Transform(Vector3.UnitZ, g), Vector3.UnitY), Is.GreaterThan(0.8f), "+Z up the handle (little finger → thumb)");
            Assert.That(Vector3.Dot(Vector3.Transform(Vector3.UnitY, g), -Vector3.UnitZ), Is.GreaterThan(0.8f), "+Y back (knuckles → wrist)");
            Assert.That(Vector3.Dot(Vector3.Transform(Vector3.UnitX, g), Vector3.UnitX), Is.GreaterThan(0.99f), "+X to the user's right");
        }

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void HeldStraightAhead_FingersPointForward_CuffTowardsTheUser(HandSide side)
        {
            var wrist = World(side, "Wrist", Held);
            var middleTip = World(side, "MiddleTip", Held);
            Assert.That(middleTip.Z - wrist.Z, Is.GreaterThan(0.15f), "middle fingertip well in front of the wrist");
        }

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void HeldStraightAhead_WristIsBehindAndSlightlyBelowTheGrip(HandSide side)
        {
            var wrist = World(side, "Wrist", Held);
            Assert.That(wrist.Z, Is.LessThan(Chest.Z - 0.04f), "behind the grip centre");
            Assert.That(wrist.Y, Is.LessThan(Chest.Y), "below the grip centre");
            Assert.That(wrist.Y, Is.GreaterThan(Chest.Y - 0.03f), "only slightly below");
        }

        [TestCase(HandSide.Left)]
        [TestCase(HandSide.Right)]
        public void HeldStraightAhead_ThumbOnTop(HandSide side)
        {
            Assert.That(World(side, "ThumbTip", Held).Y, Is.GreaterThan(World(side, "LittleTip", Held).Y + 0.03f));
        }

        [Test]
        public void HeldStraightAhead_PalmsFaceInwards()
        {
            var leftPalm = Mount(HandSide.Left).GloveDirectionToWorld(Held, GloveHandLayout.PalmNormal);
            var rightPalm = Mount(HandSide.Right).GloveDirectionToWorld(Held, GloveHandLayout.PalmNormal);
            Assert.That(Vector3.Dot(leftPalm, Vector3.UnitX), Is.GreaterThan(0.5f), "left palm faces the user's right");
            Assert.That(Vector3.Dot(rightPalm, -Vector3.UnitX), Is.GreaterThan(0.5f), "right palm faces the user's left");
        }

        [Test]
        public void LeftAndRight_AreMirrorImagesInGripSpace()
        {
            var identity = new PartPose(Vector3.Zero, Quaternion.Identity);
            foreach (var joint in GloveHandLayout.JointNames)
            {
                var l = World(HandSide.Left, joint, identity);
                var r = World(HandSide.Right, joint, identity);
                Assert.That(r.X, Is.EqualTo(-l.X).Within(1e-5f), joint);
                Assert.That(r.Y, Is.EqualTo(l.Y).Within(1e-5f), joint);
                Assert.That(r.Z, Is.EqualTo(l.Z).Within(1e-5f), joint);
            }
        }

        [Test]
        public void IdentityGrip_PointsTheFingersDownTheGripY_AsOpenXRDefines()
        {
            // Documents the convention: an identity grip rotation (the Editor simulator's default) is a hand with its
            // fingers along grip -Y, i.e. pointing down, thumb towards +Z. Not a mount error.
            var identity = new PartPose(Chest, Quaternion.Identity);
            var wrist = World(HandSide.Left, "Wrist", identity);
            var middleTip = World(HandSide.Left, "MiddleTip", identity);
            Assert.That(wrist.Y - middleTip.Y, Is.GreaterThan(0.12f));
        }

        [Test]
        public void GlovePlacement_ThumbSideAndPalmFollowTheLayoutAxes()
        {
            Assert.That(GloveHandLayout.Joint("ThumbTip", HandSide.Left).X, Is.GreaterThan(0.05f), "left thumb on +X");
            Assert.That(GloveHandLayout.Joint("ThumbTip", HandSide.Right).X, Is.LessThan(-0.05f), "right thumb on -X");
            Assert.That(GloveHandLayout.Joint("MiddleTip", HandSide.Left).Z, Is.GreaterThan(0.15f), "fingers along +Z");
            Assert.That(GloveHandLayout.ThumbSide(HandSide.Right), Is.EqualTo(-Vector3.UnitX));
        }

        [Test]
        public void Basis_MapsLocalAxesOntoTheGivenAxes()
        {
            var x = new Vector3(0f, 0f, 1f);
            var y = new Vector3(1f, 0f, 0f);
            var z = Vector3.Cross(x, y);
            var q = GripPose.Basis(x, y, z);
            Assert.That(Vector3.Distance(Vector3.Transform(Vector3.UnitX, q), x), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(Vector3.Transform(Vector3.UnitY, q), y), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(Vector3.Transform(Vector3.UnitZ, q), z), Is.LessThan(1e-5f));
        }

        [TestCase(HandSide.Left, Finger.Index)]
        [TestCase(HandSide.Left, Finger.Middle)]
        [TestCase(HandSide.Left, Finger.Little)]
        [TestCase(HandSide.Left, Finger.Thumb)]
        [TestCase(HandSide.Right, Finger.Index)]
        [TestCase(HandSide.Right, Finger.Ring)]
        [TestCase(HandSide.Right, Finger.Thumb)]
        public void Curl_MovesTheFingertipTowardsThePalm(HandSide side, Finger finger)
        {
            var open = GloveHandLayout.CurledTip(finger, 0f, side);
            var curled = GloveHandLayout.CurledTip(finger, 1f, side);
            Assert.That(Vector3.Distance(open, GloveHandLayout.Joint(finger + "Tip", side)), Is.LessThan(1e-6f), "zero curl = bind pose");
            Assert.That(Vector3.Dot(curled - open, GloveHandLayout.PalmNormal), Is.GreaterThan(0.01f), "tip moves to the palm side");
            if (finger != Finger.Thumb)
                Assert.That(curled.Z, Is.LessThan(open.Z - 0.05f), "a curled finger folds back under the palm");
        }

        [Test]
        public void Grip_CurlsTheControllerFingersTowardsThePalm_InTheWorld()
        {
            // Grip pressed while held straight ahead: middle fingertip ends up on the palm side (left palm faces right).
            var curls = ControllerHandPose.FromInput(1f, 0f);
            var mount = Mount(HandSide.Left);
            var open = mount.GlovePointToWorld(Held, GloveHandLayout.CurledTip(Finger.Middle, 0f, HandSide.Left));
            var fist = mount.GlovePointToWorld(Held, GloveHandLayout.CurledTip(Finger.Middle, curls.Middle, HandSide.Left));
            Assert.That(fist.X - open.X, Is.GreaterThan(0.02f), "towards the user's right, the left palm side");
        }
    }
}
