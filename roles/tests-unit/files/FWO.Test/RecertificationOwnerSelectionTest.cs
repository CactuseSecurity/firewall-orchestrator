using FWO.Data;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class RecertificationOwnerSelectionTest
    {
        [Test]
        public void SelectOwnerReturnsNullForEmptyOwnerList()
        {
            FwoOwner? selectedOwner = RecertificationOwnerSelection.SelectOwner([], []);

            Assert.That(selectedOwner, Is.Null);
        }

        [Test]
        public void SelectOwnerReturnsFirstOwnerIfNoRecertifiableOwnerExists()
        {
            List<FwoOwner> owners =
            [
                new() { Id = 7, Name = "Owner A" },
                new() { Id = 9, Name = "Owner B" }
            ];

            FwoOwner? selectedOwner = RecertificationOwnerSelection.SelectOwner(owners, [42]);

            Assert.That(selectedOwner?.Id, Is.EqualTo(7));
        }

        [Test]
        public void SelectOwnerPrefersFirstRecertifiableOwner()
        {
            List<FwoOwner> owners =
            [
                new() { Id = 3, Name = "Owner C" },
                new() { Id = 11, Name = "Owner D" },
                new() { Id = 17, Name = "Owner E" }
            ];

            FwoOwner? selectedOwner = RecertificationOwnerSelection.SelectOwner(owners, [17, 11]);

            Assert.That(selectedOwner?.Id, Is.EqualTo(11));
        }

        [Test]
        public void SelectOwnerReturnsFirstOwnerIfNoRecertifiableOwnerIdsAreProvided()
        {
            List<FwoOwner> owners =
            [
                new() { Id = 4, Name = "Owner F" },
                new() { Id = 5, Name = "Owner G" }
            ];

            FwoOwner? selectedOwner = RecertificationOwnerSelection.SelectOwner(owners, []);

            Assert.That(selectedOwner?.Id, Is.EqualTo(4));
        }
    }
}
