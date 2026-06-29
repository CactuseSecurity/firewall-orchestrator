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

            List<OwnerInformation> value = RuleFieldSourceResolver.ResolveOwnerInformation(
                rule,
                @"[""owner_key""]");

            ClassicAssert.AreEqual(1, value.Count);
            ClassicAssert.AreEqual(123, value[0].Id);
            ClassicAssert.AreEqual("owner-from-custom", value[0].ExtAppId);
        }

        [Test]
        public void ResolveOwnerInformation_ShouldOmitExtAppIdWhenNoMappingIsConfigured()
        {
            Rule rule = CreateRule(
                OwnerMappingSourceStm.CustomField,
                "{'owner_key':'owner-from-custom'}",
                123);

            List<OwnerInformation> value = RuleFieldSourceResolver.ResolveOwnerInformation(rule, "");

            ClassicAssert.AreEqual(1, value.Count);
            ClassicAssert.AreEqual(123, value[0].Id);
            ClassicAssert.IsNull(value[0].ExtAppId);
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
        public void ResolveOwnerInformation_ShouldReturnAllOwnersForNonCustomFieldMappings()
        {
            Rule rule = CreateRule(OwnerMappingSourceStm.NameField, "{'owner_key':'owner-from-custom'}", 123, 456);

            List<OwnerInformation> value = RuleFieldSourceResolver.ResolveOwnerInformation(
                rule,
                @"[""owner_key""]");

            ClassicAssert.AreEqual(2, value.Count);
            ClassicAssert.AreEqual(123, value[0].Id);
            ClassicAssert.AreEqual(456, value[1].Id);
            ClassicAssert.AreEqual("owner-from-custom", value[0].ExtAppId);
        }

        private static Rule CreateRule(int ownerId, string customFields)
        {
            return CreateRule(OwnerMappingSourceStm.CustomField, customFields, ownerId);
        }

        private static Rule CreateRule(OwnerMappingSourceStm mappingSource, string customFields, params int[] ownerIds)
        {
            return new Rule
            {
                RuleOwner = ownerIds.Select(ownerId => new RuleOwner
                {
                    OwnerId = ownerId,
                    OwnerMappingSourceId = (int)mappingSource
                }).ToArray(),
                CustomFields = customFields
            };
        }
    }
}
