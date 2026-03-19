using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class OwnerLifeCycleStateTest
    {
        [Test]
        public void CopyConstructor_CopiesActiveState()
        {
            OwnerLifeCycleState source = new()
            {
                Id = 4,
                Name = "Pilot",
                ActiveState = false
            };

            OwnerLifeCycleState copy = new(source);

            Assert.That(copy.Id, Is.EqualTo(4));
            Assert.That(copy.Name, Is.EqualTo("Pilot"));
            Assert.That(copy.ActiveState, Is.False);
        }
    }
}
