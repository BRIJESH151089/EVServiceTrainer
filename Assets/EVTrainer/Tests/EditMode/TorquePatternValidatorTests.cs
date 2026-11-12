using System;
using System.Collections.Generic;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class TorquePatternValidatorTests
    {
        // Holes numbered around the perimeter; cross pattern 1,5,3,7,2,6,4,8.
        private static readonly string[] Cross = { "h1", "h5", "h3", "h7", "h2", "h6", "h4", "h8" };

        private static TorquePatternValidator Validator() => new TorquePatternValidator(Cross);

        [Test]
        public void SequenceNumber_IsThePositionInThePattern()
        {
            var v = Validator();
            Assert.That(v.SequenceNumber("h1"), Is.EqualTo(1));
            Assert.That(v.SequenceNumber("h5"), Is.EqualTo(2));
            Assert.That(v.SequenceNumber("h8"), Is.EqualTo(8));
            Assert.That(v.SequenceNumber("nope"), Is.Zero);
        }

        [Test]
        public void Check_NextIsOk_OthersAreWrongSequence_DoneIsOk()
        {
            var v = Validator();
            var done = new HashSet<string>();

            Assert.That(v.Check("h1", done.Contains), Is.EqualTo(PatternCheck.Ok));
            Assert.That(v.Check("h2", done.Contains), Is.EqualTo(PatternCheck.WrongSequence));
            done.Add("h1");
            Assert.That(v.NextExpected(done.Contains), Is.EqualTo("h5"));
            Assert.That(v.Check("h5", done.Contains), Is.EqualTo(PatternCheck.Ok));
            Assert.That(v.Check("h1", done.Contains), Is.EqualTo(PatternCheck.Ok), "re-torquing a finished bolt");
        }

        [Test]
        public void NextExpected_IsNull_WhenAllDone()
        {
            Assert.That(Validator().NextExpected(_ => true), Is.Null);
        }

        [Test]
        public void Evaluate_ExactOrder_IsOk()
        {
            Assert.That(Validator().Evaluate(Cross), Is.EqualTo(PatternCheck.Ok));
        }

        [Test]
        public void Evaluate_AllPresentButOutOfOrder_IsWrongSequence()
        {
            var circle = new[] { "h1", "h2", "h3", "h4", "h5", "h6", "h7", "h8" };
            Assert.That(Validator().Evaluate(circle), Is.EqualTo(PatternCheck.WrongSequence));
        }

        [Test]
        public void Evaluate_MissingHoles_IsSkipped()
        {
            Assert.That(Validator().Evaluate(new[] { "h1", "h5", "h3" }), Is.EqualTo(PatternCheck.Skipped));
        }

        [Test]
        public void InvalidPatterns_Throw()
        {
            Assert.Throws<ArgumentException>(() => new TorquePatternValidator(new string[0]));
            Assert.Throws<ArgumentException>(() => new TorquePatternValidator(new[] { "a", "a" }));
            Assert.Throws<ArgumentNullException>(() => new TorquePatternValidator(null));
        }
    }
}
