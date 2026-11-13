using System;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class ContactShadowTests
    {
        [Test]
        public void FullWhenTouching_FadesWithHeight_GoneAtTheFadeHeight()
        {
            Assert.That(ContactShadow.Strength(0f), Is.EqualTo(1f));
            Assert.That(ContactShadow.Strength(-0.01f), Is.EqualTo(1f), "slightly inside the surface still counts as touching");
            Assert.That(ContactShadow.Strength(0.1f, 0.4f), Is.EqualTo(0.5625f).Within(1e-5f));
            Assert.That(ContactShadow.Strength(0.2f, 0.4f), Is.LessThan(ContactShadow.Strength(0.1f, 0.4f)));
            Assert.That(ContactShadow.Strength(0.45f), Is.EqualTo(0f));
            Assert.That(ContactShadow.Strength(2f), Is.EqualTo(0f));
            Assert.That(ContactShadow.Strength(float.NaN), Is.EqualTo(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ContactShadow.Strength(0.1f, 0f));
        }
    }
}
