using FWO.Data;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// The labels of a rule side travel from the importer through the database and the normalized config of the
    /// middleware back to the importer, which compares them with the newly imported labels. They have to keep their
    /// shape on this way: a list of ORed AND-groups, within a group a single value as string and several values of one
    /// key as array.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class RuleLabelsJsonConverterTest
    {
        private const string kSourceLabelsJson = "[{\"AppRole\":[\"AR1\",\"AR2\"],\"Stage\":\"Prod\"},{\"AppRole\":\"AR3\"}]";
        private const string kLabelsJson = "{\"rule_src_labels\":" + kSourceLabelsJson + ",\"rule_dst_labels\":null}";
        private const string kAppRoleKey = "AppRole";
        private const string kStageKey = "Stage";
        private static readonly List<string> kAppRoleValues = ["AR1", "AR2"];
        private static readonly List<string> kStageValues = ["Prod"];
        private static readonly List<string> kSecondGroupAppRoleValues = ["AR3"];

        /// <summary>
        /// The label groups read from the database keep their order and all values, single values become one
        /// element lists.
        /// </summary>
        [Test]
        public void ReadJson_KeepsGroupsAndAcceptsStringsAndArrays()
        {
            Rule rule = JsonConvert.DeserializeObject<Rule>(kLabelsJson) ?? throw new AssertionException("rule is null");

            Assert.Multiple(() =>
            {
                Assert.That(rule.SourceLabels, Has.Count.EqualTo(2));
                Assert.That(rule.SourceLabels?[0][kAppRoleKey], Is.EqualTo(kAppRoleValues));
                Assert.That(rule.SourceLabels?[0][kStageKey], Is.EqualTo(kStageValues));
                Assert.That(rule.SourceLabels?[1][kAppRoleKey], Is.EqualTo(kSecondGroupAppRoleValues));
                Assert.That(rule.SourceLabels?[1].ContainsKey(kStageKey), Is.False);
                Assert.That(rule.DestinationLabels, Is.Null);
            });
        }

        /// <summary>
        /// Anything but an array of objects is rejected instead of being read as empty labels, including the former
        /// single object format.
        /// </summary>
        [TestCase("{\"rule_src_labels\":{\"AppRole\":\"AR1\"}}")]
        [TestCase("{\"rule_src_labels\":[\"AR1\"]}")]
        [TestCase("{\"rule_src_labels\":\"AR1\"}")]
        public void ReadJson_RejectsAnythingButGroupArrays(string json)
        {
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Rule>(json));
        }

        /// <summary>
        /// The normalized rule writes the labels back in the shape they were read in.
        /// </summary>
        [Test]
        public void NormalizedRule_WritesLabelsInTheirOriginalShape()
        {
            Rule rule = JsonConvert.DeserializeObject<Rule>(kLabelsJson) ?? throw new AssertionException("rule is null");

            string json = JsonConvert.SerializeObject(NormalizedRule.FromRule(rule));

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("\"rule_src_labels\":" + kSourceLabelsJson));
                Assert.That(json, Does.Contain("\"rule_dst_labels\":null"));
            });
        }

        /// <summary>
        /// An empty group list stays an empty list instead of becoming null.
        /// </summary>
        [Test]
        public void ReadJson_KeepsAnEmptyGroupList()
        {
            Rule rule = JsonConvert.DeserializeObject<Rule>("{\"rule_src_labels\":[]}") ?? throw new AssertionException("rule is null");

            Assert.That(rule.SourceLabels, Is.Empty);
        }

        /// <summary>
        /// System.Text.Json (used e.g. by the JSON rule report export) keeps the label groups when writing and
        /// reading, writing every value list as array.
        /// </summary>
        [Test]
        public void SystemTextJson_KeepsGroupsOnRoundTrip()
        {
            Rule rule = JsonConvert.DeserializeObject<Rule>(kLabelsJson) ?? throw new AssertionException("rule is null");

            string json = System.Text.Json.JsonSerializer.Serialize(rule);
            Rule readBack = System.Text.Json.JsonSerializer.Deserialize<Rule>(json) ?? throw new AssertionException("rule is null");

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("\"rule_src_labels\":[{\"AppRole\":[\"AR1\",\"AR2\"],\"Stage\":[\"Prod\"]},{\"AppRole\":[\"AR3\"]}]"));
                Assert.That(json, Does.Not.Contain("rule_dst_labels"));
                Assert.That(readBack.SourceLabels, Is.EqualTo(rule.SourceLabels));
                Assert.That(readBack.DestinationLabels, Is.Null);
            });
        }

        /// <summary>
        /// The converter writes null labels as null when called directly.
        /// </summary>
        [Test]
        public void WriteJson_WritesNullForMissingLabels()
        {
            using StringWriter text = new();
            using JsonTextWriter writer = new(text);

            new RuleLabelsJsonConverter().WriteJson(writer, null, JsonSerializer.CreateDefault());
            writer.Flush();

            Assert.That(text.ToString(), Is.EqualTo("null"));
        }
    }
}
