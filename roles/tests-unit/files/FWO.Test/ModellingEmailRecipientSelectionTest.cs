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
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(nameof(EmailRecipientOption.OwnerGroupOnly), new List<int> { 1, 2, 3 });

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.EqualTo(new List<int> { GlobalConst.kOwnerResponsibleTypeSupporting }));
        }

        [Test]
        public void ParseLegacyAllOwnerResponsiblesUsesActiveResponsibleTypes()
        {
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(nameof(EmailRecipientOption.AllOwnerResponsibles), new List<int> { 1, 3 });

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OwnerResponsibleTypeIds.OrderBy(id => id), Is.EqualTo(new List<int> { 1, 3 }));
        }

        [Test]
        public void ParseLegacyAllOwnerResponsiblesReturnsEmptyWhenNoResponsibleTypesAreActive()
        {
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(nameof(EmailRecipientOption.AllOwnerResponsibles), Array.Empty<int>());

            Assert.That(selection.None, Is.True);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.Empty);
        }

        [Test]
        public void ParseJsonDerivesNoneFromEffectiveSelections()
        {
            string rawConfig = "{\"none\":true,\"other_addresses\":true,\"owner_responsible_type_ids\":[1,2]}";
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(rawConfig, new List<int> { 1, 2, 3 });

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OtherAddresses, Is.True);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.EqualTo(new List<int> { 1, 2 }));
        }

        [Test]
        public void ToConfigValueWithoutRecipientsStoresNone()
        {
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = false,
                OwnerResponsibleTypeIds = new List<int>()
            };

            Assert.That(selection.ToConfigValue(new List<int> { 1, 2 }), Is.EqualTo(nameof(EmailRecipientOption.None)));
        }

        [Test]
        public void ToConfigValueWithEmptyOtherAddressListStoresNone()
        {
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = new List<string>()
            };

            Assert.That(selection.ToConfigValue(new List<int> { 1, 2 }), Is.EqualTo(nameof(EmailRecipientOption.None)));
        }

        [Test]
        public void ParseJsonWithEmptyOtherAddressListClearsOtherAddresses()
        {
            string rawConfig = "{\"none\":false,\"other_addresses\":true,\"other_address_list\":[],\"owner_responsible_type_ids\":[]}";

            EmailRecipientSelection selection = EmailRecipientSelection.Parse(rawConfig, new List<int> { 1, 2 });

            Assert.That(selection.None, Is.True);
            Assert.That(selection.OtherAddresses, Is.False);
        }

        [Test]
        public void ParseLegacyOtherAddressesKeepsSelectionForLegacyAddressMerge()
        {
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(nameof(EmailRecipientOption.OtherAddresses), new List<int> { 1, 2 });

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OtherAddresses, Is.True);
        }

        [Test]
        public void ParseJsonKeepsSanitizedOtherAddressList()
        {
            string rawConfig = "{\"none\":false,\"other_addresses\":true,\"other_address_list\":[\" a@test \",\"A@test\",\"b@test\"],\"owner_responsible_type_ids\":[]}";

            EmailRecipientSelection selection = EmailRecipientSelection.Parse(rawConfig, new List<int> { 1, 2 });

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OtherAddresses, Is.True);
            Assert.That(selection.OtherAddressList, Is.EqualTo(new List<string> { "a@test", "b@test" }));
        }

        [Test]
        public void ParseJsonKeepsRequesterSelection()
        {
            string rawConfig = "{\"none\":false,\"other_addresses\":true,\"other_address_list\":[\"cc@example.test\"],\"requester\":true,\"owner_responsible_type_ids\":[]}";
            List<int> activeResponsibleTypeIds = new List<int> { 1, 2 };

            EmailRecipientSelection selection = EmailRecipientSelection.Parse(rawConfig, activeResponsibleTypeIds);

            Assert.That(selection.None, Is.False);
            Assert.That(selection.Requester, Is.True);
            Assert.That(selection.OtherAddresses, Is.True);
        }

        [Test]
        public void ToConfigValueKeepsRequesterSelection()
        {
            EmailRecipientSelection selection = new()
            {
                None = false,
                Requester = true
            };
            List<int> activeResponsibleTypeIds = new List<int> { 1, 2 };

            string configValue = selection.ToConfigValue(activeResponsibleTypeIds);

            Assert.That(configValue, Does.Contain("\"requester\":true"));
        }

        [Test]
        public void GetOwnerResponsibleTypeFallbackOrderUsesHighestSortOrderFirst()
        {
            EmailRecipientSelection selection = new()
            {
                None = false,
                OwnerResponsibleTypeIds = new List<int> { 1, 2, 3 }
            };

            List<OwnerResponsibleType> ownerResponsibleTypes = new List<OwnerResponsibleType>
            {
                new OwnerResponsibleType { Id = 1, Active = true, SortOrder = 10 },
                new OwnerResponsibleType { Id = 2, Active = true, SortOrder = 50 },
                new OwnerResponsibleType { Id = 3, Active = false, SortOrder = 100 }
            };

            List<int> fallbackOrder = selection.GetOwnerResponsibleTypeFallbackOrder(ownerResponsibleTypes).ToList();

            Assert.That(fallbackOrder, Is.EqualTo(new List<int> { 2, 1 }));
        }

        [Test]
        public void ParseLegacyFallbackOptionEnablesEnsureAtLeastOneNotification()
        {
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(nameof(EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty), new List<int> { 1, 2 });

            Assert.That(selection.None, Is.False);
            Assert.That(selection.EnsureAtLeastOneNotification, Is.True);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.EqualTo(new List<int> { 2, 1 }));
        }

        [Test]
        public void ParseInvalidJsonFallsBackToLegacyValue()
        {
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(
                "{invalid-json",
                new List<int> { GlobalConst.kOwnerResponsibleTypeMain, GlobalConst.kOwnerResponsibleTypeSupporting });

            Assert.That(selection.None, Is.True);
            Assert.That(selection.OtherAddresses, Is.False);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.Empty);
        }
    }
}
