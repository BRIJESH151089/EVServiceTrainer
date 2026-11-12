using System;
using EVTrainer.Core;
using NUnit.Framework;

namespace EVTrainer.Tests.EditMode
{
    public class AssemblyDefinitionTests
    {
        private static PartSpec Part(string id, params string[] prerequisites) =>
            new PartSpec(id, id, PartPose.Identity, 0.01f, 5f, prerequisites: prerequisites);

        [Test]
        public void Parts_KeepOrder_AndAreFoundById()
        {
            var def = new AssemblyDefinition("battery_pack", "EV Battery Pack",
                new[] { Part("cooling_plate"), Part("module_1", "cooling_plate") });

            Assert.That(def.Parts.Count, Is.EqualTo(2));
            Assert.That(def.Parts[0].Id, Is.EqualTo("cooling_plate"));
            Assert.That(def.TryGetPart("module_1", out var module), Is.True);
            Assert.That(module.Prerequisites, Is.EquivalentTo(new[] { "cooling_plate" }));
            Assert.That(def.TryGetPart("missing", out _), Is.False);
        }

        [Test]
        public void DuplicatePartIds_Throw()
        {
            Assert.Throws<ArgumentException>(() =>
                new AssemblyDefinition("a", "A", new[] { Part("x"), Part("x") }));
        }

        [Test]
        public void UnknownOrSelfPrerequisite_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new AssemblyDefinition("a", "A", new[] { Part("x", "missing") }));
            Assert.Throws<ArgumentException>(() =>
                new AssemblyDefinition("a", "A", new[] { Part("x", "x") }));
        }
    }
}
