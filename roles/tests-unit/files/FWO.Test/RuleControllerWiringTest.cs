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

        [Test]
        public void ConvertRuleList_ShouldFlattenServiceObjectsAndRemoveDuplicates()
        {
            List<RuleDetail> rules = InvokeConvertRuleList(
                [CreateRuleWithDuplicateNetworkAndServiceObjects()],
                fieldSourceMapping: null);

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
                [CreateRuleWithNestedServiceGroups()],
                fieldSourceMapping: null);

            ClassicAssert.AreEqual(1, rules.Count);
            ClassicAssert.AreEqual(1, rules[0].Service.Count);
            ClassicAssert.AreEqual("Nested Service", rules[0].Service[0].Name);
            ClassicAssert.AreEqual("Nested Service (8443/TCP)", rules[0].ServiceShort);
        }

        [Test]
        public void ConvertRuleList_ShouldExposeDestinationPortAndProtocolForServices()
        {
            List<RuleDetail> rules = InvokeConvertRuleList(
                [CreateRuleWithPortAndProtocolService()],
                fieldSourceMapping: null);

            ClassicAssert.AreEqual(1, rules.Count);
            ClassicAssert.AreEqual(1, rules[0].Service.Count);
            ClassicAssert.AreEqual(443, rules[0].Service[0].Port);
            ClassicAssert.AreEqual("TCP", rules[0].Service[0].Protocol);
            ClassicAssert.AreEqual("Ported Service (443/TCP)", rules[0].ServiceShort);
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
            globalConfig.CustomFieldOwnerKey = @"[""owner_key""]";
            globalConfig.CustomFieldChangeIdKey = @"[""change_key""]";

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
            NetworkService outerServiceGroup = CreateServiceGroup(300, "Outer Service Group", innerServiceGroup);

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

        private static NetworkService CreateServiceGroup(long id, string name, NetworkService member)
        {
            return new NetworkService
            {
                Id = id,
                Name = name,
                Type = new NetworkServiceType { Name = ServiceType.Group },
                ServiceGroupFlats = [new GroupFlat<NetworkService> { Id = member.Id, Object = member }]
            };
        }
    }
}
