using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FwoOwnerTest
    {
        [Test]
        public void CopyConstructor_PreservesRecertActive()
        {
            FwoOwner original = new()
            {
                RecertActive = true
            };

            FwoOwner copy = new(original);

            Assert.That(copy.RecertActive, Is.True);
        }
    }
}
