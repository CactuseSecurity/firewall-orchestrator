using System.Reflection;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server.Controllers;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleControllerWiringTest
    {
        private static readonly MethodInfo ConvertRuleListMethod =
            typeof(RuleController).GetMethod("ConvertRuleList", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(RuleController).FullName, "ConvertRuleList");

        [Test]
        public void ConvertRuleList_ShouldKeepCurrentDefaultBehaviorWhenMappingIsMissing()
        {
            List<RuleDetail> rules = InvokeConvertRuleList(
                [CreateRule(ownerId: 123, customFields: "{'owner_key':'owner-from-custom','change_key':'chg-4711'}")],
                fieldSourceMapping: null);

            ClassicAssert.AreEqual(1, rules.Count);
            ClassicAssert.AreEqual("123", rules[0].OwnerInformation);
            ClassicAssert.AreEqual("chg-4711", rules[0].ChangeID);
        }

        [Test]
        public void ConvertRuleList_ShouldUseCustomFieldsWhenRequested()
        {
            List<RuleDetail> rules = InvokeConvertRuleList(
                [CreateRule(ownerId: 123, customFields: "{'owner_key':'owner-from-custom','change_key':'chg-4711'}")],
                new FieldSourceMapping
                {
                    OwnerInformation = FieldSource.CustomField,
                    ChangeId = FieldSource.CustomField
                });

            ClassicAssert.AreEqual(1, rules.Count);
            ClassicAssert.AreEqual("owner-from-custom", rules[0].OwnerInformation);
            ClassicAssert.AreEqual("chg-4711", rules[0].ChangeID);
        }

        [Test]
        public void ConvertRuleList_ShouldUseDatabaseOwnerAndUnsupportedDatabaseChangeId()
        {
            List<RuleDetail> rules = InvokeConvertRuleList(
                [CreateRule(ownerId: 123, customFields: "{'owner_key':'owner-from-custom','change_key':'chg-4711'}")],
                new FieldSourceMapping
                {
                    OwnerInformation = FieldSource.Database,
                    ChangeId = FieldSource.Database
                });

            ClassicAssert.AreEqual(1, rules.Count);
            ClassicAssert.AreEqual("123", rules[0].OwnerInformation);
            ClassicAssert.AreEqual(RuleFieldSourceResolver.NotFoundValue, rules[0].ChangeID);
        }

        private static List<RuleDetail> InvokeConvertRuleList(List<Rule> rules, FieldSourceMapping? fieldSourceMapping)
        {
            UserConfig userConfig = CreateUserConfig();
            object? result = ConvertRuleListMethod.Invoke(null, [rules, userConfig, fieldSourceMapping]);
            return (List<RuleDetail>)(result ?? throw new AssertionException("Expected rule details."));
        }

        private static UserConfig CreateUserConfig()
        {
            GlobalConfig globalConfig = new();
            globalConfig.LangDict[GlobalConst.kEnglish] = new Dictionary<string, string>();
            globalConfig.OverDict[GlobalConst.kEnglish] = new Dictionary<string, string>();
            globalConfig.CustomFieldOwnerKey = "owner_key";
            globalConfig.CustomFieldChangeIdKey = "change_key";

            return new UserConfig(globalConfig, registerOnChangeHandler: false);
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
