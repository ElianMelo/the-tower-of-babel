using NaughtyAttributes.Editor;
using NUnit.Framework;

namespace NaughtyAttributes.Tests
{
    public class ValueBoundTests
    {
        private class Owner
        {
#pragma warning disable CS0414
            private float minField = 2f;
            private int maxIntField = 7;
#pragma warning restore CS0414
            private float MinProperty => 3f;
            private float GetMax() => 9f;
            private string notNumeric = "x";
        }

        [TestCase("minField", 2f)]
        [TestCase("maxIntField", 7f)]
        [TestCase("MinProperty", 3f)]
        [TestCase("GetMax", 9f)]
        public void TryResolve_ReadsNumericMembers(string name, float expected)
        {
            Assert.IsTrue(ValueBoundUtility.TryResolve(new Owner(), name, out float value));
            Assert.AreEqual(expected, value);
        }

        [TestCase("notNumeric")]
        [TestCase("missing")]
        public void TryResolve_FailsForInvalidMembers(string name)
        {
            Assert.IsFalse(ValueBoundUtility.TryResolve(new Owner(), name, out _));
        }

        [Test]
        public void Attributes_StoreConstructorArguments()
        {
            Assert.AreEqual("a", new MinValueAttribute("a").MinValueName);
            Assert.AreEqual("b", new MaxValueAttribute("b").MaxValueName);
            Assert.AreEqual(4f, new MinValueAttribute(4).MinValue);
            Assert.IsNull(new MaxValueAttribute(1f).MaxValueName);
            Assert.IsTrue(new ExpandableAttribute(isReadonly: true).IsReadonly);
            Assert.IsFalse(new ExpandableAttribute().IsReadonly);
        }
    }
}
