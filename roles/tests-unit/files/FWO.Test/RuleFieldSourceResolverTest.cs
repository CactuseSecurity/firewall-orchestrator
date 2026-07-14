using System.Linq;
using FWO.Basics;
using FWO.Data;
using FWO.Middleware.Server.Controllers;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleFieldSourceResolverTest
    {
        [Test]
        public void ResolveOwnerInformation_ShouldUseDatabaseOwnerIdAndCustomFieldExtAppId()
        {
            Rule rule = CreateRule(
                OwnerMappingSourceStm.CustomField,
                "{'owner_key':'owner-from-custom'}",
                123);

            OwnerInformation value = RuleFieldSourceResolver.ResolveOwnerInformation(
                rule,
                @"[""owner_key""]");

            ClassicAssert.AreEqual(1, value.OwnerIds.Count);
            ClassicAssert.AreEqual(123, value.OwnerIds[0]);
            ClassicAssert.AreEqual("owner-from-custom", value.ExtAppId);
        }

        [Test]
        public void ResolveOwnerInformation_ShouldOmitExtAppIdWhenNoMappingIsConfigured()
        {
            Rule rule = CreateRule(
                OwnerMappingSourceStm.CustomField,
                "{'owner_key':'owner-from-custom'}",
                123);

            OwnerInformation value = RuleFieldSourceResolver.ResolveOwnerInformation(rule, "");

            ClassicAssert.AreEqual(1, value.OwnerIds.Count);
            ClassicAssert.AreEqual(123, value.OwnerIds[0]);
            ClassicAssert.IsNull(value.ExtAppId);
        }

        [Test]
        public void ResolveAdditionalInformation_ShouldUseChangeIdWhenConfigured()
        {
            Rule rule = CreateRule(
                OwnerMappingSourceStm.CustomField,
                "{'change_key':'chg-4711'}",
                123);

            AdditionalInformation value = RuleFieldSourceResolver.ResolveAdditionalInformation(rule, @"[""change_key""]");

            ClassicAssert.AreEqual("chg-4711", value.ChangeId);
        }

        [Test]
        public void ResolveAdditionalInformation_ShouldReturnEmptyObjectWhenNoMappingIsConfigured()
        {
            Rule rule = CreateRule(
                OwnerMappingSourceStm.CustomField,
                "{'change_key':'chg-4711'}",
                123);

            AdditionalInformation value = RuleFieldSourceResolver.ResolveAdditionalInformation(rule, "");

            ClassicAssert.IsNull(value.ChangeId);
        }

        [Test]
        public void ResolveAdditionalInformation_ShouldUseJsonArrayCustomFieldKeys()
        {
            Rule rule = CreateRule(
                OwnerMappingSourceStm.CustomField,
                "{'change_key':'chg-4711','fallback_key':'chg-0001'}",
                123);

            AdditionalInformation value = RuleFieldSourceResolver.ResolveAdditionalInformation(
                rule,
                @"[""missing_key"", ""change_key""]");

            ClassicAssert.AreEqual("chg-4711", value.ChangeId);
        }

        [Test]
        public void ResolveOwnerInformation_ShouldRejectMultipleActiveOwners()
        {
            Rule rule = CreateRule(OwnerMappingSourceStm.CustomField, "{'owner_key':'owner-from-custom'}", 123, 456);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                RuleFieldSourceResolver.ResolveOwnerInformation(rule, @"[""owner_key""]"))!;

            StringAssert.Contains("requires exactly one owner", exception.Message);
        }

        [Test]
        public void ResolveOwnerInformation_ShouldIgnoreRemovedOwners()
        {
            Rule rule = CreateRule(
                OwnerMappingSourceStm.CustomField,
                "{'owner_key':'owner-from-custom'}",
                CreateRuleOwner(123, removed: 99),
                CreateRuleOwner(456));

            OwnerInformation value = RuleFieldSourceResolver.ResolveOwnerInformation(
                rule,
                @"[""owner_key""]");

            ClassicAssert.AreEqual(1, value.OwnerIds.Count);
            ClassicAssert.AreEqual(456, value.OwnerIds[0]);
            ClassicAssert.AreEqual("owner-from-custom", value.ExtAppId);
        }

        [Test]
        public void ResolveOwnerInformation_ShouldReturnAllOwnersForNonCustomFieldMappings()
        {
            Rule rule = CreateRule(OwnerMappingSourceStm.NameField, "{'owner_key':'owner-from-custom'}", 123, 456);

            OwnerInformation value = RuleFieldSourceResolver.ResolveOwnerInformation(
                rule,
                @"[""owner_key""]");

            ClassicAssert.AreEqual(2, value.OwnerIds.Count);
            ClassicAssert.AreEqual(123, value.OwnerIds[0]);
            ClassicAssert.AreEqual(456, value.OwnerIds[1]);
            ClassicAssert.AreEqual("owner-from-custom", value.ExtAppId);
        }

        private static Rule CreateRule(int ownerId, string customFields)
        {
            return CreateRule(OwnerMappingSourceStm.CustomField, customFields, ownerId);
        }

        private static Rule CreateRule(OwnerMappingSourceStm mappingSource, string customFields, params int[] ownerIds)
        {
            return CreateRule(
                mappingSource,
                customFields,
                ownerIds.Select(ownerId => CreateRuleOwner(ownerId)).ToArray());
        }

        private static Rule CreateRule(OwnerMappingSourceStm mappingSource, string customFields, params RuleOwner[] owners)
        {
            return new Rule
            {
                RuleOwner = owners.Select(owner =>
                {
                    owner.OwnerMappingSourceId = (int)mappingSource;
                    return owner;
                }).ToArray(),
                CustomFields = customFields
            };
        }

        private static RuleOwner CreateRuleOwner(int ownerId, long? removed = null)
        {
            return new RuleOwner
            {
                OwnerId = ownerId,
                Removed = removed
            };
        }
    }
}
