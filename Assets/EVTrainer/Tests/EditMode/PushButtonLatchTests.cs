using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    /// <summary>Latching buttons (mode choice) stay pressed in after firing; momentary ones spring back.</summary>
    public class PushButtonLatchTests
    {
        [Test]
        public void Latching_FiresOnce_ThenStaysPressedIn_AndIgnoresPushes()
        {
            var b = new PushButtonLogic(0.7f, 0.15f, latching: true);

            Assert.That(b.Update(0.8f), Is.True);
            Assert.That(b.IsLatched, Is.True);
            Assert.That(b.DisplayDepth(0f), Is.EqualTo(1f), "held fully in after the finger leaves");
            Assert.That(b.Update(0f), Is.False);
            Assert.That(b.Update(1f), Is.False, "pushing a latched button does nothing");
            Assert.That(b.IsLatched, Is.True);
        }

        [Test]
        public void Unlatch_SpringsBack_AndRearmsAfterTheCapReturns()
        {
            var b = new PushButtonLogic(0.7f, 0.15f, latching: true);
            b.Update(1f);
            b.Unlatch();

            Assert.That(b.IsLatched, Is.False);
            Assert.That(b.DisplayDepth(0.3f), Is.EqualTo(0.3f));
            Assert.That(b.Update(0.9f), Is.False, "not re-armed until the cap is back out");
            b.Update(0.1f);
            Assert.That(b.Update(0.9f), Is.True);
        }

        [Test]
        public void Momentary_NeverLatches()
        {
            var b = new PushButtonLogic();
            Assert.That(b.Latching, Is.False);
            Assert.That(b.Update(1f), Is.True);
            Assert.That(b.IsLatched, Is.False);
            Assert.That(b.DisplayDepth(0f), Is.EqualTo(0f));
            b.Latch();
            Assert.That(b.IsLatched, Is.False, "Latch() is ignored on a momentary button");
        }

        [Test]
        public void Latch_WithoutAClick_AndReset_ClearsIt()
        {
            var b = new PushButtonLogic(latching: true);
            b.Latch();
            Assert.That(b.IsLatched, Is.True);
            Assert.That(b.Update(1f), Is.False, "no click when latched from code");
            b.Reset();
            Assert.That(b.IsLatched, Is.False);
            Assert.That(b.IsArmed, Is.True);
            Assert.That(b.Update(1f), Is.True);
        }
    }
}
