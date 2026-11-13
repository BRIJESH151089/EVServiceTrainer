using System;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class SegmentStretchTests
    {
        [Test]
        public void MatchesTheTrackedLength()
        {
            Assert.That(SegmentStretch.Scale(0.044f, 0.040f), Is.EqualTo(1.1f).Within(1e-5f));
            Assert.That(SegmentStretch.Scale(0.036f, 0.040f), Is.EqualTo(0.9f).Within(1e-5f));
            Assert.That(SegmentStretch.Scale(0.040f, 0.040f), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void TrackingGlitches_AreClamped_OrIgnored()
        {
            Assert.That(SegmentStretch.Scale(0.2f, 0.04f), Is.EqualTo(SegmentStretch.DefaultMax));
            Assert.That(SegmentStretch.Scale(0.001f, 0.04f), Is.EqualTo(SegmentStretch.DefaultMin));
            Assert.That(SegmentStretch.Scale(0f, 0.04f), Is.EqualTo(1f), "no tracked length: keep the model length");
            Assert.That(SegmentStretch.Scale(float.NaN, 0.04f), Is.EqualTo(1f));
            Assert.That(SegmentStretch.Scale(0.04f, 0f), Is.EqualTo(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => SegmentStretch.Scale(0.04f, 0.04f, 0f, 1f));
        }
    }
}
