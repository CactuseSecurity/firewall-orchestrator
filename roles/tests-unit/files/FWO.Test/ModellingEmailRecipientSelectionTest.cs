using FWO.Basics;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class ModellingEmailRecipientSelectionTest
    {
        [Test]
        public void ParseLegacyOwnerGroupOnlyMapsToSupportingResponsible()
        {
            ModellingEmailRecipientSelection selection = ModellingEmailRecipientSelection.Parse(nameof(EmailRecipientOption.OwnerGroupOnly), [1, 2, 3]);

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.EqualTo(new[] { GlobalConst.kOwnerResponsibleTypeSupporting }));
        }

        [Test]
        public void ParseLegacyAllOwnerResponsiblesUsesActiveResponsibleTypes()
        {
            ModellingEmailRecipientSelection selection = ModellingEmailRecipientSelection.Parse(nameof(EmailRecipientOption.AllOwnerResponsibles), [1, 3]);

            Assert.That(selection.None, Is.False);
            Assert.That(selection.OwnerResponsibleTypeIds.OrderBy(id => id), Is.EqualTo(new[] { 1, 3 }));
        }

        [Test]
        public void ParseJsonNoneDisablesOtherSelections()
        {
            string rawConfig = "{\"none\":true,\"other_addresses\":true,\"owner_responsible_type_ids\":[1,2]}";
            ModellingEmailRecipientSelection selection = ModellingEmailRecipientSelection.Parse(rawConfig, [1, 2, 3]);

            Assert.That(selection.None, Is.True);
            Assert.That(selection.OtherAddresses, Is.False);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.Empty);
        }

        [Test]
        public void ToConfigValueWithoutRecipientsStoresNone()
        {
            ModellingEmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = false,
                OwnerResponsibleTypeIds = []
            };

            Assert.That(selection.ToConfigValue([1, 2]), Is.EqualTo(nameof(EmailRecipientOption.None)));
        }

        [Test]
        public void GetOwnerResponsibleTypeFallbackOrderUsesHighestSortOrderFirst()
        {
            ModellingEmailRecipientSelection selection = new()
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
            ModellingEmailRecipientSelection selection = ModellingEmailRecipientSelection.Parse(nameof(EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty), [1, 2]);

            Assert.That(selection.None, Is.False);
            Assert.That(selection.EnsureAtLeastOneNotification, Is.True);
            Assert.That(selection.OwnerResponsibleTypeIds, Is.EqualTo(new[] { 2, 1 }));
        }
    }
}
