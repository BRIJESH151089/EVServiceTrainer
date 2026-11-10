using System;
using System.Numerics;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class SnapEvaluatorTests
    {
        private const float PosTol = 0.01f;  // 1 cm
        private const float AngleTol = 5f;   // degrees

        private static readonly Vector3 TargetPosition = new Vector3(0.2f, 0.9f, -0.1f);
        private static readonly Quaternion TargetRotation = Quaternion.CreateFromYawPitchRoll(Rad(30f), 0f, 0f);

        private static float Rad(float degrees) => degrees * (float)Math.PI / 180f;

        private static PartSpec Spec() => new PartSpec(
            "cooling_plate", "Cooling Plate",
            new PartPose(TargetPosition, TargetRotation),
            PosTol, AngleTol);

        private static PartPose OffsetPosition(float metres) =>
            new PartPose(TargetPosition + new Vector3(metres, 0f, 0f), TargetRotation);

        private static PartPose RotatedAboutUp(float degrees) =>
            new PartPose(TargetPosition, TargetRotation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, Rad(degrees)));

        [Test]
        public void ExactPose_IsWithinTolerance()
        {
            var result = SnapEvaluator.Evaluate(Spec(), new PartPose(TargetPosition, TargetRotation));

            Assert.That(result.PositionErrorMetres, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(result.AngleErrorDegrees, Is.EqualTo(0f).Within(0.01f));
            Assert.That(result.IsWithinTolerance, Is.True);
        }

        [Test]
        public void Position_JustInsideTolerance_Passes()
        {
            var result = SnapEvaluator.Evaluate(Spec(), OffsetPosition(0.0095f));

            Assert.That(result.PositionErrorMetres, Is.EqualTo(0.0095f).Within(1e-5f));
            Assert.That(result.IsPositionWithinTolerance, Is.True);
            Assert.That(result.IsWithinTolerance, Is.True);
        }

        [Test]
        public void Position_JustOutsideTolerance_Fails()
        {
            var result = SnapEvaluator.Evaluate(Spec(), OffsetPosition(0.0105f));

            Assert.That(result.PositionErrorMetres, Is.EqualTo(0.0105f).Within(1e-5f));
            Assert.That(result.IsPositionWithinTolerance, Is.False);
            Assert.That(result.IsWithinTolerance, Is.False);
        }

        [Test]
        public void Angle_JustInsideTolerance_Passes()
        {
            var result = SnapEvaluator.Evaluate(Spec(), RotatedAboutUp(4.9f));

            Assert.That(result.AngleErrorDegrees, Is.EqualTo(4.9f).Within(0.01f));
            Assert.That(result.IsAngleWithinTolerance, Is.True);
            Assert.That(result.IsWithinTolerance, Is.True);
        }

        [Test]
        public void Angle_JustOutsideTolerance_Fails()
        {
            var result = SnapEvaluator.Evaluate(Spec(), RotatedAboutUp(5.1f));

            Assert.That(result.AngleErrorDegrees, Is.EqualTo(5.1f).Within(0.01f));
            Assert.That(result.IsAngleWithinTolerance, Is.False);
            Assert.That(result.IsWithinTolerance, Is.False);
        }

        [Test]
        public void FlippedPart_180Degrees_Fails()
        {
            // Correct position, but turned upside down (180 degrees about the part's forward axis).
            var flipped = new PartPose(TargetPosition, TargetRotation * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, Rad(180f)));

            var result = SnapEvaluator.Evaluate(Spec(), flipped);

            Assert.That(result.PositionErrorMetres, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(result.AngleErrorDegrees, Is.EqualTo(180f).Within(0.01f));
            Assert.That(result.IsWithinTolerance, Is.False);
        }

        [Test]
        public void NegatedQuaternion_IsSameRotation()
        {
            var negated = new PartPose(TargetPosition, Quaternion.Negate(TargetRotation));

            var result = SnapEvaluator.Evaluate(Spec(), negated);

            Assert.That(result.AngleErrorDegrees, Is.EqualTo(0f).Within(0.01f));
            Assert.That(result.IsWithinTolerance, Is.True);
        }
    }
}
