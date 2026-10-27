using FWO.Basics.Exceptions;
using FWO.Data.DeviceSpecificSettings;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using System.Text.Json.Nodes;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class AzureRuleCollectionMergeTest
    {
        private const string kGroup = """
            {"properties":{"priority":100,"ruleCollections":[
              {"name":"FWO-Allow","priority":200,"action":{"type":"Allow"},"rules":[{"name":"r1","destinationPorts":["80"]}]},
              {"name":"other","rules":[{"name":"r2"}]}]}}
            """;

        private static readonly List<string> kRulesAfterCreate = ["r1", "r3"];

        private static JsonObject Group() => JsonNode.Parse(kGroup)!.AsObject();

        private static JsonObject Fragment(string json) => JsonNode.Parse(json)!.AsObject();

        private static JsonArray Rules(JsonObject group, int collection) => group["properties"]!["ruleCollections"]![collection]!["rules"]!.AsArray();

        [Test]
        public void Create_AppendsToExistingCollection()
        {
            JsonObject group = Group();
            AzureRuleCollectionMerge.Apply(group, FirewallWriteOperation.RuleCreate, Fragment("""{"ruleCollectionName":"FWO-Allow","rule":{"name":"r3"}}"""));
            Assert.That(Rules(group, 0).Select(rule => rule!["name"]!.GetValue<string>()), Is.EqualTo(kRulesAfterCreate));
        }

        [Test]
        public void Create_ReplacesRuleWithSameName()
        {
            JsonObject group = Group();
            AzureRuleCollectionMerge.Apply(group, FirewallWriteOperation.RuleCreate, Fragment("""{"ruleCollectionName":"FWO-Allow","rule":{"name":"r1","destinationPorts":["443"]}}"""));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Rules(group, 0), Has.Count.EqualTo(1));
                Assert.That(Rules(group, 0)[0]!["destinationPorts"]![0]!.GetValue<string>(), Is.EqualTo("443"));
            }
        }

        [Test]
        public void Create_AddsMissingCollectionWithDefaults()
        {
            JsonObject group = Group();
            AzureRuleCollectionMerge.Apply(group, FirewallWriteOperation.RuleCreate, Fragment("""{"rule":{"name":"r4"}}"""));
            JsonNode added = group["properties"]!["ruleCollections"]![2]!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(added["name"]!.GetValue<string>(), Is.EqualTo("FWO"));
                Assert.That(added["priority"]!.GetValue<int>(), Is.EqualTo(10000));
                Assert.That(added["action"]!["type"]!.GetValue<string>(), Is.EqualTo("Allow"));
                Assert.That(added["ruleCollectionType"]!.GetValue<string>(), Is.EqualTo("FirewallPolicyFilterRuleCollection"));
            }
        }

        [Test]
        public void Modify_ReplacesRuleInAnyCollection()
        {
            JsonObject group = Group();
            AzureRuleCollectionMerge.Apply(group, FirewallWriteOperation.RuleModify, Fragment("""{"rule":{"name":"r2","description":"changed"}}"""));
            Assert.That(Rules(group, 1)[0]!["description"]!.GetValue<string>(), Is.EqualTo("changed"));
        }

        [Test]
        public void Delete_RemovesRuleByName()
        {
            JsonObject group = Group();
            AzureRuleCollectionMerge.Apply(group, FirewallWriteOperation.RuleDelete, Fragment("""{"ruleName":"r2"}"""));
            Assert.That(Rules(group, 1), Is.Empty);
        }

        [TestCase(FirewallWriteOperation.RuleDelete, """{"ruleName":"missing"}""")]
        [TestCase(FirewallWriteOperation.RuleDelete, """{}""")]
        [TestCase(FirewallWriteOperation.RuleModify, """{"rule":{"name":"missing"}}""")]
        [TestCase(FirewallWriteOperation.RuleCreate, """{"rule":{}}""")]
        [TestCase(FirewallWriteOperation.RuleCreate, """{"name":"no rule object"}""")]
        public void InvalidChanges_Fail(FirewallWriteOperation operation, string fragment)
        {
            Assert.Throws<ConfigException>(() => AzureRuleCollectionMerge.Apply(Group(), operation, Fragment(fragment)));
        }

        [Test]
        public void GroupWithoutCollections_Fails()
        {
            Assert.Throws<ConfigException>(() => AzureRuleCollectionMerge.Apply(new JsonObject(), FirewallWriteOperation.RuleCreate,
                Fragment("""{"rule":{"name":"r"}}""")));
        }
    }
}
