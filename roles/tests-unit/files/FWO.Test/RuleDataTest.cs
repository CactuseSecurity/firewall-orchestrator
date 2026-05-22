using FWO.Data;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class RuleDataTest
    {
        [Test]
        public void RuleTimes_AreDeserialized_WhenRemovedIsNull()
        {
            const string serializedRule = """
                {
                    "rule_id": 1001,
                    "rule_times": [
                        {
                            "rule_time_id": 10,
                            "rule_id": 1001,
                            "time_obj_id": 55,
                            "created": 200,
                            "removed": null,
                            "time_object": {
                                "time_obj_id": 55,
                                "time_obj_name": "Office Hours",
                                "time_obj_uid": "uid-55",
                                "start_time": "2025-01-01T08:00:00Z",
                                "end_time": "2025-01-01T17:00:00Z",
                                "created": 200
                            }
                        }
                    ]
                }
                """;

            Rule? deserializedRule = JsonConvert.DeserializeObject<Rule>(serializedRule);

            Assert.That(deserializedRule, Is.Not.Null);
            Assert.That(deserializedRule!.RuleTimes.Count, Is.EqualTo(1));
            Assert.That(deserializedRule.RuleTimes[0].Id, Is.EqualTo(10));
            Assert.That(deserializedRule.RuleTimes[0].RuleId, Is.EqualTo(1001));
            Assert.That(deserializedRule.RuleTimes[0].TimeObjId, Is.EqualTo(55));
            Assert.That(deserializedRule.RuleTimes[0].Created, Is.EqualTo(200));
            Assert.That(deserializedRule.RuleTimes[0].Removed, Is.Null);
            Assert.That(deserializedRule.RuleTimes[0].TimeObj, Is.Not.Null);
            TimeObject timeObject = deserializedRule.RuleTimes[0].TimeObj!;
            Assert.That(timeObject.Id, Is.EqualTo(55));
            Assert.That(timeObject.Name, Is.EqualTo("Office Hours"));
        }

        [Test]
        public void NormalizedRule_FromRule_PreservesNatAndTranslationFlags()
        {
            Rule rule = new()
            {
                NatRule = true,
                AccessRule = false,
                XlateRule = "1366",
                TranslatedRule = new Rule { Uid = "translated-rule" },
                RuleOrderNumber = 7,
                OrderNumber = 7.0,
                Disabled = false,
                SourceNegated = false,
                Source = "any",
                SourceRefs = "",
                DestinationNegated = false,
                Destination = "any",
                DestinationRefs = "",
                ServiceNegated = false,
                Service = "any",
                ServiceRefs = "",
                Action = "accept",
                Track = "none",
                Implied = false,
                Metadata = new RuleMetadata()
            };

            NormalizedRule normalizedRule = NormalizedRule.FromRule(rule);

            Assert.That(normalizedRule.NatRule, Is.True);
            Assert.That(normalizedRule.AccessRule, Is.False);
            Assert.That(normalizedRule.XlateRule, Is.EqualTo("translated-rule"));

            string serialized = JsonConvert.SerializeObject(normalizedRule);
            Assert.That(serialized, Does.Contain("\"nat_rule\":true"));
            Assert.That(serialized, Does.Contain("\"access_rule\":false"));
            Assert.That(serialized, Does.Contain("\"xlate_rule_uid\":\"translated-rule\""));
        }
    }
}
