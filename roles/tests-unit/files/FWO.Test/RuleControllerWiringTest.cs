using System.Reflection;
using System.Linq;
using System.Text.Json;
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
        private static readonly JsonSerializerOptions WebJsonSerializerOptions = new(JsonSerializerDefaults.Web);

        private static readonly MethodInfo ConvertRuleListMethod =
            typeof(RuleController).GetMethod("ConvertRuleList", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(RuleController).FullName, "ConvertRuleList");

        [Test]
        public void ConvertRuleList_ShouldPopulateNestedOwnerAndAdditionalInformation()
        {
            List<RuleDetail> rules = InvokeConvertRuleList(
                [CreateRule(ownerId: 123, customFields: "{'owner_key':'owner-from-custom','change_key':'chg-4711'}")]);

            ClassicAssert.AreEqual(1, rules.Count);
            ClassicAssert.AreEqual(123, rules[0].OwnerInformation.Id);
            ClassicAssert.AreEqual("owner-from-custom", rules[0].OwnerInformation.ExtAppId);
            ClassicAssert.AreEqual("chg-4711", rules[0].AdditionalInformation.ChangeId);

            string json = JsonSerializer.Serialize(rules[0], WebJsonSerializerOptions);
            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement root = document.RootElement;
            ClassicAssert.IsTrue(root.TryGetProperty("ownerInformation", out JsonElement ownerInformation));
            ClassicAssert.AreEqual(123, ownerInformation.GetProperty("id").GetInt32());
            ClassicAssert.AreEqual("owner-from-custom", ownerInformation.GetProperty("extAppId").GetString());

            ClassicAssert.IsTrue(root.TryGetProperty("additionalInformation", out JsonElement additionalInformation));
            ClassicAssert.AreEqual("chg-4711", additionalInformation.GetProperty("changeId").GetString());
        }

        [Test]
        public void ConvertRuleList_ShouldReturnEmptyAdditionalInformationWhenChangeIdMappingMissing()
        {
            List<RuleDetail> rules = InvokeConvertRuleList(
                [CreateRule(ownerId: 123, customFields: "{'owner_key':'owner-from-custom','change_key':'chg-4711'}")],
                CreateUserConfig(changeIdKeys: ""));

            ClassicAssert.AreEqual(1, rules.Count);
            ClassicAssert.AreEqual(123, rules[0].OwnerInformation.Id);
            ClassicAssert.AreEqual("owner-from-custom", rules[0].OwnerInformation.ExtAppId);
            ClassicAssert.IsNull(rules[0].AdditionalInformation.ChangeId);

            string json = JsonSerializer.Serialize(rules[0], WebJsonSerializerOptions);
            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement root = document.RootElement;
            ClassicAssert.IsTrue(root.TryGetProperty("additionalInformation", out JsonElement additionalInformation));
            ClassicAssert.AreEqual("{}", additionalInformation.GetRawText());
        }

        [Test]
        public void ConvertRuleList_ShouldFlattenServiceObjectsAndRemoveDuplicates()
        {
            List<RuleDetail> rules = InvokeConvertRuleList(
                [CreateRuleWithDuplicateNetworkAndServiceObjects()]);

            ClassicAssert.AreEqual(1, rules.Count);

            RuleDetail rule = rules[0];
            ClassicAssert.AreEqual(2, rule.Source.Count);
            ClassicAssert.AreEqual("Source Group", rule.Source[0].Name);
            ClassicAssert.AreEqual("Shared Source", rule.Source[1].Name);

            ClassicAssert.AreEqual(2, rule.Destination.Count);
            ClassicAssert.AreEqual("Destination Group", rule.Destination[0].Name);
            ClassicAssert.AreEqual("Shared Destination", rule.Destination[1].Name);

            ClassicAssert.AreEqual(1, rule.Service.Count);
            ClassicAssert.AreEqual("Shared Service", rule.Service[0].Name);
            ClassicAssert.AreEqual("Shared Service (443/TCP)", rule.ServiceShort);
        }

        [Test]
        public void ConvertRuleList_ShouldFlattenNestedServiceGroups()
        {
            List<RuleDetail> rules = InvokeConvertRuleList(
                [CreateRuleWithNestedServiceGroups()]);

            ClassicAssert.AreEqual(1, rules.Count);
            ClassicAssert.AreEqual(1, rules[0].Service.Count);
            ClassicAssert.AreEqual("Nested Service", rules[0].Service[0].Name);
            ClassicAssert.AreEqual("Nested Service (8443/TCP)", rules[0].ServiceShort);
        }

        [Test]
        public void ConvertRuleList_ShouldExposeDestinationPortAndProtocolForServices()
        {
            List<RuleDetail> rules = InvokeConvertRuleList(
                [CreateRuleWithPortAndProtocolService()]);

            ClassicAssert.AreEqual(1, rules.Count);
            ClassicAssert.AreEqual(1, rules[0].Service.Count);
            ClassicAssert.AreEqual(443, rules[0].Service[0].Port);
            ClassicAssert.AreEqual("TCP", rules[0].Service[0].Protocol);
            ClassicAssert.AreEqual("Ported Service (443/TCP)", rules[0].ServiceShort);
        }

        private static List<RuleDetail> InvokeConvertRuleList(List<Rule> rules, UserConfig? userConfig = null)
        {
            UserConfig effectiveUserConfig = userConfig ?? CreateUserConfig();
            object? result = ConvertRuleListMethod.Invoke(null, [rules, effectiveUserConfig]);
            return (List<RuleDetail>)(result ?? throw new AssertionException("Expected rule details."));
        }

        private static UserConfig CreateUserConfig(string ownerKeys = @"[""owner_key""]", string changeIdKeys = @"[""change_key""]")
        {
            GlobalConfig globalConfig = new();
            globalConfig.LangDict[GlobalConst.kEnglish] = new Dictionary<string, string>();
            globalConfig.OverDict[GlobalConst.kEnglish] = new Dictionary<string, string>();
            globalConfig.CustomFieldOwnerKey = ownerKeys;
            globalConfig.CustomFieldChangeIdKey = changeIdKeys;

            return UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
        }

        private static Rule CreateRule(int ownerId, string customFields)
        {
            return new Rule
            {
                RuleOwner = [new RuleOwner { OwnerId = ownerId }],
                CustomFields = customFields
            };
        }

        private static Rule CreateRuleWithDuplicateNetworkAndServiceObjects()
        {
            NetworkObject sharedSource = CreateNetworkObject(101, "Shared Source", "10.0.0.1", "10.0.0.1");
            NetworkObject sharedDestination = CreateNetworkObject(201, "Shared Destination", "10.0.0.2", "10.0.0.2");
            NetworkService sharedService = CreateServiceObject(301, "Shared Service", 443, 6, "TCP");

            return new Rule
            {
                Froms =
                [
                    CreateNetworkLocation("source-group-user", CreateNetworkObjectGroup(100, "Source Group", sharedSource)),
                    CreateNetworkLocation("source-member-user", sharedSource)
                ],
                Tos =
                [
                    CreateNetworkLocation("destination-group-user", CreateNetworkObjectGroup(200, "Destination Group", sharedDestination)),
                    CreateNetworkLocation("destination-member-user", sharedDestination)
                ],
                Services =
                [
                    new ServiceWrapper { Content = CreateServiceGroup(300, "Service Group", sharedService) },
                    new ServiceWrapper { Content = sharedService }
                ]
            };
        }

        private static Rule CreateRuleWithNestedServiceGroups()
        {
            NetworkService nestedService = CreateServiceObject(401, "Nested Service", 8443, 6, "TCP");
            NetworkService innerServiceGroup = CreateServiceGroup(400, "Inner Service Group", nestedService);
            NetworkService outerServiceGroup = CreateServiceGroup(300, "Outer Service Group", innerServiceGroup, nestedService);

            return new Rule
            {
                Services =
                [
                    new ServiceWrapper { Content = outerServiceGroup }
                ]
            };
        }

        private static NetworkLocation CreateNetworkLocation(string userName, NetworkObject networkObject)
        {
            return new NetworkLocation(new NetworkUser { Name = userName }, networkObject);
        }

        private static NetworkObject CreateNetworkObject(long id, string name, string ip, string ipEnd)
        {
            return new NetworkObject
            {
                Id = id,
                Name = name,
                IP = ip,
                IpEnd = ipEnd,
                Type = new NetworkObjectType { Name = "host" }
            };
        }

        private static NetworkObject CreateNetworkObjectGroup(long id, string name, NetworkObject member)
        {
            return new NetworkObject
            {
                Id = id,
                Name = name,
                Type = new NetworkObjectType { Name = ObjectType.Group },
                ObjectGroupFlats = [new GroupFlat<NetworkObject> { Id = member.Id, Object = member }]
            };
        }

        private static Rule CreateRuleWithPortAndProtocolService()
        {
            return new Rule
            {
                Services =
                [
                    new ServiceWrapper
                    {
                        Content = CreateServiceObject(401, "Ported Service", 443, 6, "TCP")
                    }
                ]
            };
        }

        private static NetworkService CreateServiceObject(long id, string name, int destinationPort, int protocolId, string protocol)
        {
            return new NetworkService
            {
                Id = id,
                Name = name,
                DestinationPort = destinationPort,
                Protocol = new NetworkProtocol { Id = protocolId, Name = protocol },
                Type = new NetworkServiceType { Name = "tcp" }
            };
        }

        private static NetworkService CreateServiceGroup(long id, string name, params NetworkService[] members)
        {
            return new NetworkService
            {
                Id = id,
                Name = name,
                Type = new NetworkServiceType { Name = ServiceType.Group },
                ServiceGroupFlats = members.Select(member => new GroupFlat<NetworkService> { Id = member.Id, Object = member }).ToArray()
            };
        }
    }
}
