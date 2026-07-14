using FWO.Basics;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class EmailRecipientSelectionTest
    {
        [Test]
        public void ParseLegacyOwnerGroupOnlyMapsToSupportingResponsible()
        {
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(nameof(EmailRecipientOption.OwnerGroupOnly), [1, 2, 3]);

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.EqualTo(new[] { GlobalConst.kOwnerResponsibleTypeSupporting }));
        }

        [Test]
        public void ParseLegacyAllOwnerResponsiblesUsesActiveResponsibleTypes()
        {
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(nameof(EmailRecipientOption.AllOwnerResponsibles), [1, 3]);

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OwnerResponsibleTypeIds.OrderBy(id => id), Is.EqualTo(new[] { 1, 3 }));
        }

        [Test]
        public void ParseJsonDerivesNoneFromEffectiveSelections()
        {
            string rawConfig = "{\"none\":true,\"other_addresses\":true,\"owner_responsible_type_ids\":[1,2]}";
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(rawConfig, [1, 2, 3]);

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OtherAddresses, Is.True);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void ToConfigValueWithoutRecipientsStoresNone()
        {
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = false,
                OwnerResponsibleTypeIds = []
            };

            Assert.That(selection.ToConfigValue([1, 2]), Is.EqualTo(nameof(EmailRecipientOption.None)));
        }

        [Test]
        public void ToConfigValueWithEmptyOtherAddressListStoresNone()
        {
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = []
            };

            Assert.That(selection.ToConfigValue([1, 2]), Is.EqualTo(nameof(EmailRecipientOption.None)));
        }

        [Test]
        public void ParseJsonWithEmptyOtherAddressListClearsOtherAddresses()
        {
            string rawConfig = "{\"none\":false,\"other_addresses\":true,\"other_address_list\":[],\"owner_responsible_type_ids\":[]}";

            EmailRecipientSelection selection = EmailRecipientSelection.Parse(rawConfig, [1, 2]);

            Assert.That(selection.None, Is.True);
            Assert.That(selection.OtherAddresses, Is.False);
        }

        [Test]
        public void ParseLegacyOtherAddressesKeepsSelectionForLegacyAddressMerge()
        {
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(nameof(EmailRecipientOption.OtherAddresses), [1, 2]);

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OtherAddresses, Is.True);
        }

        [Test]
        public void ParseJsonKeepsSanitizedOtherAddressList()
        {
            string rawConfig = "{\"none\":false,\"other_addresses\":true,\"other_address_list\":[\" a@test \",\"A@test\",\"b@test\"],\"owner_responsible_type_ids\":[]}";

            EmailRecipientSelection selection = EmailRecipientSelection.Parse(rawConfig, [1, 2]);

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OtherAddresses, Is.True);
            Assert.That(selection.OtherAddressList, Is.EqualTo(new[] { "a@test", "b@test" }));
        }

        [Test]
        public void GetOwnerResponsibleTypeFallbackOrderUsesHighestSortOrderFirst()
        {
            EmailRecipientSelection selection = new()
            {
                None = false,
                OwnerResponsibleTypeIds = [1, 2, 3]
            };

            List<OwnerResponsibleType> ownerResponsibleTypes =
            [
                new OwnerResponsibleType { Id = 1, Active = true, SortOrder = 10 },
                new OwnerResponsibleType { Id = 2, Active = true, SortOrder = 50 },
                new OwnerResponsibleType { Id = 3, Active = false, SortOrder = 100 }
            ];

            List<int> fallbackOrder = selection.GetOwnerResponsibleTypeFallbackOrder(ownerResponsibleTypes).ToList();

            Assert.That(fallbackOrder, Is.EqualTo(new[] { 2, 1 }));
        }

        [Test]
        public void ParseLegacyFallbackOptionEnablesEnsureAtLeastOneNotification()
        {
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(nameof(EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty), [1, 2]);

            Assert.That(selection.None, Is.False);
            Assert.That(selection.EnsureAtLeastOneNotification, Is.True);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.EqualTo(new[] { 2, 1 }));
        }

        [Test]
        public void ParseInvalidJsonFallsBackToLegacyValue()
        {
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(
                "{invalid-json",
                [GlobalConst.kOwnerResponsibleTypeMain, GlobalConst.kOwnerResponsibleTypeSupporting]);

            Assert.That(selection.None, Is.True);
            Assert.That(selection.OtherAddresses, Is.False);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.Empty);
        }
    }
}
