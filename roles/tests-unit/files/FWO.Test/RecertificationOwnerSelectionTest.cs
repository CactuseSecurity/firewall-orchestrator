using FWO.Data;
using FWO.Ui.Services;
using NUnit.Framework;
using System.Collections.Generic;
using System.Security.Claims;

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

        [Test]
        public void ResolveOwnerIdsUsesConfiguredIdsWhenPresent()
        {
            List<int> configuredOwnerIds = [4, 9];
            List<Claim> claims = [new("x-hasura-recertifiable-owners", "{11,12}")];

            List<int> resolvedOwnerIds = RecertificationOwnerSelection.ResolveOwnerIds(configuredOwnerIds, claims, "x-hasura-recertifiable-owners");

            Assert.That(resolvedOwnerIds, Is.EqualTo(new List<int> { 4, 9 }));
        }

        [Test]
        public void ResolveOwnerIdsFallsBackToClaimValues()
        {
            List<int> configuredOwnerIds = [];
            List<Claim> claims = [new("x-hasura-recertifiable-owners", "{11,12}")];

            List<int> resolvedOwnerIds = RecertificationOwnerSelection.ResolveOwnerIds(configuredOwnerIds, claims, "x-hasura-recertifiable-owners");

            Assert.That(resolvedOwnerIds, Is.EqualTo(new List<int> { 11, 12 }));
        }

        [Test]
        public void CanWriteSelectedOwnerReturnsTrueForAdmin()
        {
            FwoOwner selectedOwner = new() { Id = 7, Name = "Owner" };

            bool canWrite = RecertificationOwnerSelection.CanWriteSelectedOwner(selectedOwner, true, false, []);

            Assert.That(canWrite, Is.True);
        }

        [Test]
        public void CanWriteSelectedOwnerRequiresRecertifierMembershipForNonAdmin()
        {
            FwoOwner selectedOwner = new() { Id = 7, Name = "Owner" };

            bool canWrite = RecertificationOwnerSelection.CanWriteSelectedOwner(selectedOwner, false, false, [7]);

            Assert.That(canWrite, Is.False);
        }

        [Test]
        public void CanWriteSelectedOwnerChecksRecertifiableIdsForRecertifier()
        {
            FwoOwner selectedOwner = new() { Id = 7, Name = "Owner" };

            bool canWrite = RecertificationOwnerSelection.CanWriteSelectedOwner(selectedOwner, false, true, [9]);

            Assert.That(canWrite, Is.False);
        }
    }
}
