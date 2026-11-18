using FWO.Api.Client.Queries;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the gateways existing rules take effect on according to the import, which a rule_modify or rule_delete
    /// changes the rule on: the gateways of all rules with the same uid are merged without duplicates, and the number of
    /// rules read at once is capped.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class RuleEnforcingGatewaysTest
    {
        private const int kManagementId = 1;
        private static readonly DeviceWrapper[] kGatewaysAAndB = [Gateway(1, "gw-b"), Gateway(2, "gw-a")];
        private static readonly DeviceWrapper[] kGatewaysBAndC = [Gateway(2, "gw-a"), Gateway(3, "gw-c")];
        private static readonly DeviceWrapper[] kGatewayD = [Gateway(4, "gw-d")];
        private static readonly List<int> kMergedGatewayIds = [1, 2, 3];
        private static readonly List<string> kOrderedNames = ["gw-a", "gw-b", "gw-c", "gw-d"];
        private static readonly List<string> kFirstRule = ["r1"];
        private static readonly List<string> kNoRules = [];

        private static DeviceWrapper Gateway(int id, string name) => new() { Content = new Device { Id = id, Name = name } };

        private static InMemoryDeviceSpecificSettingsApiConnection Api()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            api.EnforcingRules.Add(new Rule { Uid = "r1", EnforcingGateways = kGatewaysAAndB });
            api.EnforcingRules.Add(new Rule { Uid = "r1", EnforcingGateways = kGatewaysBAndC });
            api.EnforcingRules.Add(new Rule { Uid = "r2", EnforcingGateways = kGatewayD });
            api.EnforcingRules.Add(new Rule { Uid = "", EnforcingGateways = kGatewayD });
            return api;
        }

        [Test]
        public async Task Load_MergesTheGatewaysOfAllRulesWithTheSameUid()
        {
            Dictionary<string, List<Device>> gateways = await RuleEnforcingGateways.Load(Api(), kManagementId, kFirstRule);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(gateways["r1"].Select(gateway => gateway.Id), Is.EqualTo(kMergedGatewayIds));
                Assert.That(gateways.Keys, Has.No.Member(""), "rules without uid are left out");
            }
        }

        [Test]
        public async Task Load_WithoutRuleUids_ReadsNothing()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = Api();

            Dictionary<string, List<Device>> gateways = await RuleEnforcingGateways.Load(api, kManagementId, kNoRules);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(gateways, Is.Empty);
                Assert.That(api.Calls.Where(call => call.Query == RuleQueries.getRuleEnforcingGateways), Is.Empty);
            }
        }

        [Test]
        public void Load_MoreRuleUidsThanTheLimit_IsRefused()
        {
            List<string> ruleUids = [.. Enumerable.Range(0, RuleEnforcingGateways.kMaxRules + 1).Select(index => $"r{index}")];

            Assert.ThrowsAsync<CollectionLimitExceededException>(() => RuleEnforcingGateways.Load(Api(), kManagementId, ruleUids));
        }

        [Test]
        public void Load_MoreRulesReturnedThanTheLimit_IsRefused()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            api.EnforcingRules.AddRange(Enumerable.Range(0, RuleEnforcingGateways.kMaxRules + 1).Select(_ => new Rule { Uid = "r1", EnforcingGateways = kGatewayD }));

            Assert.ThrowsAsync<CollectionLimitExceededException>(() => RuleEnforcingGateways.Load(api, kManagementId, kFirstRule));
        }

        [Test]
        public async Task LoadOfImplTask_GivesEveryGatewayOnceOrderedByName()
        {
            WfImplTask implTask = new()
            {
                Id = 7,
                TaskType = WfTaskType.rule_modify.ToString(),
                ImplElements =
                [
                    new WfImplElement { Field = ElemFieldType.rule.ToString(), RuleUid = "r1" },
                    new WfImplElement { Field = ElemFieldType.rule.ToString(), RuleUid = "r2" }
                ]
            };

            List<Device> gateways = await RuleEnforcingGateways.LoadOfImplTask(Api(), kManagementId, implTask);

            Assert.That(gateways.Select(gateway => gateway.Name), Is.EqualTo(kOrderedNames));
        }
    }
}
