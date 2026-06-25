using System.Linq;
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
                ownerId: 123,
                customFields: "{'owner_key':'owner-from-custom'}");

            OwnerInformation value = RuleFieldSourceResolver.ResolveOwnerInformation(rule, @"[""owner_key""]");

            ClassicAssert.AreEqual(123, value.Id);
            ClassicAssert.AreEqual("owner-from-custom", value.ExtAppId);
        }

        [Test]
        public void ResolveOwnerInformation_ShouldOmitExtAppIdWhenNoMappingIsConfigured()
        {
            Rule rule = CreateRule(
                ownerId: 123,
                customFields: "{'owner_key':'owner-from-custom'}");

            OwnerInformation value = RuleFieldSourceResolver.ResolveOwnerInformation(rule, "");

            ClassicAssert.AreEqual(123, value.Id);
            ClassicAssert.IsNull(value.ExtAppId);
        }

        [Test]
        public void ResolveAdditionalInformation_ShouldUseChangeIdWhenConfigured()
        {
            Rule rule = CreateRule(
                ownerId: 123,
                customFields: "{'change_key':'chg-4711'}");

            AdditionalInformation value = RuleFieldSourceResolver.ResolveAdditionalInformation(rule, @"[""change_key""]");

            ClassicAssert.AreEqual("chg-4711", value.ChangeId);
        }

        [Test]
        public void ResolveAdditionalInformation_ShouldReturnEmptyObjectWhenNoMappingIsConfigured()
        {
            Rule rule = CreateRule(
                ownerId: 123,
                customFields: "{'change_key':'chg-4711'}");

            AdditionalInformation value = RuleFieldSourceResolver.ResolveAdditionalInformation(rule, "");

            ClassicAssert.IsNull(value.ChangeId);
        }

        [Test]
        public void ResolveAdditionalInformation_ShouldUseJsonArrayCustomFieldKeys()
        {
            Rule rule = CreateRule(
                ownerId: 123,
                customFields: "{'change_key':'chg-4711','fallback_key':'chg-0001'}");

            AdditionalInformation value = RuleFieldSourceResolver.ResolveAdditionalInformation(
                rule,
                @"[""missing_key"", ""change_key""]");

            ClassicAssert.AreEqual("chg-4711", value.ChangeId);
        }

        private static Rule CreateRule(int ownerId, string customFields)
        {
            return new Rule
            {
                RuleOwner = [new RuleOwner { OwnerId = ownerId }],
                CustomFields = customFields
            };
        }
    }
}
