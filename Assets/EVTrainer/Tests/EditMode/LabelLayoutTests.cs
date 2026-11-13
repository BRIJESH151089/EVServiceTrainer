using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class LabelLayoutTests
    {
        private static readonly Vector3 Eye = new Vector3(0f, 1.6f, 0f);

        [Test]
        public void GroupText_NamesOneOrCountsSeveral()
        {
            Assert.That(LabelLayout.GroupText("Battery module", 4), Is.EqualTo("Battery module ×4"));
            Assert.That(LabelLayout.GroupText("Cooling plate", 1), Is.EqualTo("Cooling plate"));
            Assert.That(LabelLayout.GroupText("Busbar", 0), Is.Empty);
            Assert.That(LabelLayout.Capitalise("cover bolts"), Is.EqualTo("Cover bolts"));
        }

        [Test]
        public void SeparateLabels_StayWhereTheyAre()
        {
            var anchors = new[] { new Vector3(-0.6f, 1.0f, 0.3f), new Vector3(0.6f, 1.0f, 0.3f) };
            var placed = LabelLayout.Place(anchors, Eye);
            Assert.That(placed[0], Is.EqualTo(anchors[0]));
            Assert.That(placed[1], Is.EqualTo(anchors[1]));
        }

        [Test]
        public void LabelBehindAnother_FromTheViewer_IsLiftedClear_NearerOneStays()
        {
            // Busbars in front, BMS just behind them on the same line of sight.
            var near = new Vector3(0.57f, 0.95f, 0.58f);
            var far = new Vector3(0.62f, 0.95f, 0.66f);
            var placed = LabelLayout.Place(new[] { far, near }, Eye);

            Assert.That(placed[1], Is.EqualTo(near), "nearer label keeps its place");
            Assert.That(placed[0].Y, Is.GreaterThan(far.Y), "the one behind is raised");
            LabelLayout.Direction(placed[0] - Eye, out float b0, out float e0);
            LabelLayout.Direction(near - Eye, out float b1, out float e1);
            Assert.That(System.Math.Abs(e0 - e1) >= 4f || System.Math.Abs(AxisAngle.WrapDelta(b0 - b1)) >= 9f, Is.True);
        }

        [Test]
        public void Direction_BearingAndElevation()
        {
            LabelLayout.Direction(new Vector3(1f, 0f, 1f), out float b, out float e);
            Assert.That(b, Is.EqualTo(45f).Within(1e-3f));
            Assert.That(e, Is.EqualTo(0f).Within(1e-3f));
            LabelLayout.Direction(new Vector3(0f, 1f, 1f), out b, out e);
            Assert.That(e, Is.EqualTo(45f).Within(1e-3f));
        }
    }
}
