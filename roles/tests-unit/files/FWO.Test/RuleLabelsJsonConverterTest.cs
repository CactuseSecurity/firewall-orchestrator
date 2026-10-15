using FWO.Data;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// The labels of a rule side travel from the importer through the database and the normalized config of the
    /// middleware back to the importer, which compares them with the newly imported labels. They have to keep their
    /// shape on this way: a single value as string, several values of one key as array.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class RuleLabelsJsonConverterTest
    {
        private const string kLabelsJson = "{\"rule_src_labels\":{\"AppRole\":[\"AR1\",\"AR2\"],\"Stage\":\"Prod\"},\"rule_dst_labels\":null}";
        private const string kAppRoleKey = "AppRole";
        private const string kStageKey = "Stage";
        private static readonly List<string> kAppRoleValues = ["AR1", "AR2"];
        private static readonly List<string> kStageValues = ["Prod"];

        /// <summary>
        /// A label object read from the database keeps all values, single values become one element lists.
        /// </summary>
        [Test]
        public void ReadJson_AcceptsStringsAndArrays()
        {
            Rule rule = JsonConvert.DeserializeObject<Rule>(kLabelsJson) ?? throw new AssertionException("rule is null");

            Assert.Multiple(() =>
            {
                Assert.That(rule.SourceLabels?[kAppRoleKey], Is.EqualTo(kAppRoleValues));
                Assert.That(rule.SourceLabels?[kStageKey], Is.EqualTo(kStageValues));
                Assert.That(rule.DestinationLabels, Is.Null);
            });
        }

        /// <summary>
        /// Anything but an object is rejected instead of being read as empty labels.
        /// </summary>
        [Test]
        public void ReadJson_RejectsNonObjects()
        {
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Rule>("{\"rule_src_labels\":[\"AR1\"]}"));
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
                Assert.That(json, Does.Contain("\"rule_src_labels\":{\"AppRole\":[\"AR1\",\"AR2\"],\"Stage\":\"Prod\"}"));
                Assert.That(json, Does.Contain("\"rule_dst_labels\":null"));
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
