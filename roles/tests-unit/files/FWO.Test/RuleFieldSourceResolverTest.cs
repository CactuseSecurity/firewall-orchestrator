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
        public void ResolveOwnerInformationSource_ShouldDefaultToDatabase()
        {
            FieldSource source = RuleFieldSourceResolver.ResolveOwnerInformationSource(null);

            ClassicAssert.AreEqual(FieldSource.Database, source);
        }

        [Test]
        public void ResolveChangeIdSource_ShouldDefaultToCustomField()
        {
            FieldSource source = RuleFieldSourceResolver.ResolveChangeIdSource(null);

            ClassicAssert.AreEqual(FieldSource.CustomField, source);
        }

        [Test]
        public void ResolveOwnerInformation_ShouldUseDatabaseOwnerId()
        {
            Rule rule = CreateRule(
                ownerId: 123,
                customFields: "{'owner_key':'owner-from-custom'}");

            string value = RuleFieldSourceResolver.ResolveOwnerInformation(
                rule,
                FieldSource.Database,
                "owner_key",
                RuleFieldSourceResolver.NotFoundValue);

            ClassicAssert.AreEqual("123", value);
        }

        [Test]
        public void ResolveOwnerInformation_ShouldUseCustomFieldValue()
        {
            Rule rule = CreateRule(
                ownerId: 123,
                customFields: "{'owner_key':'owner-from-custom'}");

            string value = RuleFieldSourceResolver.ResolveOwnerInformation(
                rule,
                FieldSource.CustomField,
                "owner_key",
                RuleFieldSourceResolver.NotFoundValue);

            ClassicAssert.AreEqual("owner-from-custom", value);
        }

        [Test]
        public void ResolveChangeId_ShouldUseCustomFieldValue()
        {
            Rule rule = CreateRule(
                ownerId: 123,
                customFields: "{'change_key':'chg-4711'}");

            string value = RuleFieldSourceResolver.ResolveChangeId(
                rule,
                FieldSource.CustomField,
                "change_key",
                RuleFieldSourceResolver.NotFoundValue);

            ClassicAssert.AreEqual("chg-4711", value);
        }

        [Test]
        public void ResolveChangeId_ShouldReturnNotFoundForDatabase()
        {
            Rule rule = CreateRule(
                ownerId: 123,
                customFields: "{'change_key':'chg-4711'}");

            string value = RuleFieldSourceResolver.ResolveChangeId(
                rule,
                FieldSource.Database,
                "change_key",
                RuleFieldSourceResolver.NotFoundValue);

            ClassicAssert.AreEqual(RuleFieldSourceResolver.NotFoundValue, value);
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
