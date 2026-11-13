using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class ScreenFadeCurveTests
    {
        [Test]
        public void FadeOut_GoesEvenlyToBlack()
        {
            Assert.That(ScreenFadeCurve.Alpha(0f, 0.3f, toBlack: true), Is.EqualTo(0f));
            Assert.That(ScreenFadeCurve.Alpha(0.15f, 0.3f, toBlack: true), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(ScreenFadeCurve.Alpha(0.3f, 0.3f, toBlack: true), Is.EqualTo(1f));
            Assert.That(ScreenFadeCurve.Alpha(5f, 0.3f, toBlack: true), Is.EqualTo(1f), "stays black");
        }

        [Test]
        public void FadeIn_GoesFromBlackToClear()
        {
            Assert.That(ScreenFadeCurve.Alpha(0f, 0.3f, toBlack: false), Is.EqualTo(1f), "a reloaded scene starts black");
            Assert.That(ScreenFadeCurve.Alpha(0.075f, 0.3f, toBlack: false), Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(ScreenFadeCurve.Alpha(0.3f, 0.3f, toBlack: false), Is.EqualTo(0f));
            Assert.That(ScreenFadeCurve.IsFinished(0.29f, 0.3f), Is.False);
            Assert.That(ScreenFadeCurve.IsFinished(0.3f, 0.3f), Is.True);
        }

        [Test]
        public void ZeroDuration_IsAnInstantCut_BadTimeIsSafe()
        {
            Assert.That(ScreenFadeCurve.Alpha(0f, 0f, toBlack: true), Is.EqualTo(1f));
            Assert.That(ScreenFadeCurve.Alpha(0f, 0f, toBlack: false), Is.EqualTo(0f));
            Assert.That(ScreenFadeCurve.IsFinished(0f, 0f), Is.True);
            Assert.That(ScreenFadeCurve.Alpha(float.NaN, 0.3f, toBlack: true), Is.EqualTo(0f));
            Assert.That(ScreenFadeCurve.Alpha(-1f, 0.3f, toBlack: true), Is.EqualTo(0f));
        }
    }
}
