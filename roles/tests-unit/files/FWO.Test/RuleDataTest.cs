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
        public void FlowLinkFields_AreDeserialized()
        {
            const string serialized = """
                {
                    "rule_id": 2001,
                    "flow_access_id": 3001,
                    "flow_active": true,
                    "flow_access": {
                        "access_id": 3001
                    }
                }
                """;

            Rule? rule = JsonConvert.DeserializeObject<Rule>(serialized);

            Assert.That(rule, Is.Not.Null);
            Assert.That(rule!.FlowAccessId, Is.EqualTo(3001));
            Assert.That(rule.FlowAccess, Is.Not.Null);
            Assert.That(rule.FlowAccess!.Id, Is.EqualTo(3001));
        }

        [Test]
        public void PublicEntities_FlowLinkFields_AreDeserialized()
        {
            const string serializedNetworkObject = """
                {
                    "obj_id": 4001,
                    "flow_nwobj_id": 5001,
                    "flow_active": true,
                    "flow_nwobject": {
                        "nwobj_id": 5001
                    }
                }
                """;
            const string serializedNetworkService = """
                {
                    "svc_id": 4002,
                    "flow_svcobj_id": 5002,
                    "flow_active": false,
                    "flow_svcobject": {
                        "svcobj_id": 5002
                    }
                }
                """;
            const string serializedTimeObject = """
                {
                    "time_obj_id": 4003,
                    "flow_timeobj_id": 5003,
                    "flow_active": true,
                    "flow_timeobj": {
                        "timeobj_id": 5003
                    }
                }
                """;

            NetworkObject? networkObject = JsonConvert.DeserializeObject<NetworkObject>(serializedNetworkObject);
            NetworkService? networkService = JsonConvert.DeserializeObject<NetworkService>(serializedNetworkService);
            TimeObject? timeObject = JsonConvert.DeserializeObject<TimeObject>(serializedTimeObject);

            Assert.That(networkObject, Is.Not.Null);
            Assert.That(networkObject!.FlowNetworkObjectId, Is.EqualTo(5001));
            Assert.That(networkObject.FlowActive, Is.True);
            Assert.That(networkObject.FlowNwObject, Is.Not.Null);
            Assert.That(networkObject.FlowNwObject!.Id, Is.EqualTo(5001));

            Assert.That(networkService, Is.Not.Null);
            Assert.That(networkService!.FlowServiceObjectId, Is.EqualTo(5002));
            Assert.That(networkService.FlowActive, Is.False);
            Assert.That(networkService.FlowSvcObject, Is.Not.Null);
            Assert.That(networkService.FlowSvcObject!.Id, Is.EqualTo(5002));

            Assert.That(timeObject, Is.Not.Null);
            Assert.That(timeObject!.FlowTimeObjectId, Is.EqualTo(5003));
            Assert.That(timeObject.FlowActive, Is.True);
            Assert.That(timeObject.FlowTimeObject, Is.Not.Null);
            Assert.That(timeObject.FlowTimeObject!.Id, Is.EqualTo(5003));
        }
    }
}
