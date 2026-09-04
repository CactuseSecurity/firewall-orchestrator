using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Compliance;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Test.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class ComplianceRequestedRulePolicyCheckerTest : ComplianceCheckTestFixture
    {
        private ComplianceRequestedRulePolicyChecker checker = default!;

        [SetUp]
        public override void SetUpTest()
        {
            base.SetUpTest();
            checker = new ComplianceRequestedRulePolicyChecker(UserConfig, ApiConnection);
        }

        [Test]
        public async Task AreRequestTasksCompliant_NoPolicies_ReturnsFalse()
        {
            bool result = await checker.AreRequestTasksCompliant([], [CreateEligibleRequestTask(11)]);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task AreRequestTasksCompliant_NoEligibleRules_ReturnsFalse()
        {
            WfReqTask incompleteTask = new()
            {
                Id = 12,
                ManagementId = 1,
                Title = "Incomplete request",
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1/32", Name = "src" },
                    new WfReqElement { Field = ElemFieldType.rule.ToString(), RuleUid = "rule-12" }
                ]
            };

            bool result = await checker.AreRequestTasksCompliant([5], [incompleteTask]);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task AreRequestTasksCompliant_CanonicalAnyServiceViolatesTechnicalForbiddenService()
        {
            CompliancePolicy policy = new()
            {
                Id = 5,
                Criteria =
                {
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion
                        {
                            Id = 1,
                            CriterionType = nameof(CriterionType.ForbiddenService),
                            Content = "443/TCP"
                        }
                    }
                }
            };
            List<Management> managements = new()
            {
                new Management { Id = 1, Name = "Mgmt1" }
            };
            ApiConnection.AsSub()
                .SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames)
                .Returns(managements);
            ApiConnection.AsSub()
                .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                .Returns(policy);
            WfReqTask task = CreateEligibleRequestTask(13);
            WfReqElement service = task.Elements.Single(element => element.Field == ElemFieldType.service.ToString());
            service.Name = "ANY";
            service.Port = null;
            service.PortEnd = null;
            service.ProtoId = GlobalConst.kAnyIpProtocolId;
            List<int> policyIds = new() { policy.Id };
            List<WfReqTask> requestTasks = new() { task };

            bool result = await checker.AreRequestTasksCompliant(policyIds, requestTasks);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_MapsEligibleTaskAndSkipsDeletedElements()
        {
            WfReqTask task = CreateEligibleRequestTask(21);
            task.Elements.Add(new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                IpString = "10.0.0.2/32",
                Name = "deleted-src",
                RequestAction = nameof(RequestAction.delete)
            });
            task.Elements.Add(new WfReqElement
            {
                Field = ElemFieldType.service.ToString(),
                Port = 8443,
                PortEnd = 8450,
                ProtoId = 6,
                Name = "deleted-svc",
                RequestAction = nameof(RequestAction.delete)
            });

            List<Rule> rules = await BuildRulesFromRequestTasks(task);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(rules[0].MgmtId, Is.EqualTo(1));
                Assert.That(rules[0].Uid, Is.EqualTo("rule-21"));
                Assert.That(rules[0].Action, Is.EqualTo(RuleActions.Accept));
                Assert.That(rules[0].Froms, Has.Length.EqualTo(1));
                Assert.That(rules[0].Tos, Has.Length.EqualTo(1));
                Assert.That(rules[0].Services, Has.Length.EqualTo(1));
                Assert.That(rules[0].Froms[0].Object.IP, Is.EqualTo("10.0.0.1/32"));
                Assert.That(rules[0].Services[0].Content.DestinationPort, Is.EqualTo(443));
            });
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_MapsDeleteTaskToDropAndRangeToIpRange()
        {
            WfReqTask task = CreateEligibleRequestTask(22, nameof(WfTaskType.rule_delete));
            task.Elements[0].IpString = "10.0.0.1";
            task.Elements[0].IpEnd = "10.0.0.9";
            task.Elements[0].CidrEnd = new Cidr("10.0.0.9");

            List<Rule> rules = await BuildRulesFromRequestTasks(task);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(rules[0].Action, Is.EqualTo(RuleActions.Drop));
                Assert.That(rules[0].Froms[0].Object.Type.Name, Is.EqualTo(ObjectType.IPRange));
                Assert.That(rules[0].Froms[0].Object.IpEnd, Is.EqualTo("10.0.0.9/32"));
            });
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_SkipsTasksWithoutRequiredActiveElements()
        {
            WfReqTask task = CreateEligibleRequestTask(23);
            task.Elements.First(element => element.Field == ElemFieldType.service.ToString()).RequestAction = nameof(RequestAction.delete);

            List<Rule> rules = await BuildRulesFromRequestTasks(task);

            Assert.That(rules, Is.Empty);
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_ExpandsReferencedNetworkGroupMembers()
        {
            WfReqTask destinationGroupTask = new()
            {
                TaskType = WfTaskType.group_create.ToString(),
                AdditionalInfo = "{\"GrpName\":\"app-servers\"}",
                Elements =
                [
                    new WfReqElement
                    {
                        Field = ElemFieldType.source.ToString(),
                        IpString = "192.0.2.10",
                        Name = "app-server-1"
                    }
                ]
            };
            WfReqTask ruleTask = CreateEligibleRequestTask(24);
            WfReqElement destination = ruleTask.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.IpString = null;
            destination.GroupName = "app-servers";

            List<Rule> rules = await BuildRulesFromRequestTasks(ruleTask, destinationGroupTask);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].Tos, Has.Length.EqualTo(1));
            Assert.That(rules[0].Tos[0].Object.IP, Is.EqualTo("192.0.2.10/32"));
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_ExpandsModifiedNetworkGroupMembers()
        {
            WfReqTask destinationGroupTask = new()
            {
                TaskType = WfTaskType.group_modify.ToString(),
                AdditionalInfo = "{\"GrpName\":\"app-servers\"}",
                Elements =
                [
                    new WfReqElement
                    {
                        Field = ElemFieldType.source.ToString(),
                        IpString = "192.0.2.11",
                        Name = "app-server-2"
                    }
                ]
            };
            WfReqTask ruleTask = CreateEligibleRequestTask(26);
            WfReqElement destination = ruleTask.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.IpString = null;
            destination.GroupName = "app-servers";

            List<Rule> rules = await BuildRulesFromRequestTasks(ruleTask, destinationGroupTask);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].Tos, Has.Length.EqualTo(1));
            Assert.That(rules[0].Tos[0].Object.IP, Is.EqualTo("192.0.2.11/32"));
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_ResolvesExistingNetworkGroupFromFlowDb()
        {
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowNwGroup>>(FlowQueries.getFlowSyncNwGroups, Arg.Any<object>())
                .Returns(
                [
                    new FlowNwGroup
                    {
                        Id = 101,
                        Name = "existing-app-servers",
                        NwGroupMembers =
                        [
                            new FlowNwGroupMember { NwGroupId = 101, NwObjectId = 999 },
                            new FlowNwGroupMember { NwGroupId = 101, NwObjectId = 201 }
                        ]
                    }
                ]);
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowNwObject>>(FlowQueries.getFlowSyncNwObjects, Arg.Any<object>())
                .Returns(
                [
                    new FlowNwObject { Id = 201, Name = "app-server-1", IpStart = "192.0.2.10", IpEnd = "192.0.2.10" }
                ]);

            WfReqTask ruleTask = CreateEligibleRequestTask(25);
            WfReqElement destination = ruleTask.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.IpString = null;
            destination.GroupName = "existing-app-servers";
            destination.FlowNetworkGroupId = 101;

            List<Rule> rules = await BuildRulesFromRequestTasks(ruleTask);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].Tos, Has.Length.EqualTo(1));
            Assert.That(rules[0].Tos[0].Object.IP, Is.EqualTo("192.0.2.10/32"));
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_ResolvesExistingServiceGroupFromFlowDb()
        {
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowSvcGroup>>(FlowQueries.getFlowSyncSvcGroups, Arg.Any<object>())
                .Returns(
                [
                    new FlowSvcGroup
                    {
                        Id = 102,
                        Name = "web-services",
                        SvcGroupMembers = [new FlowSvcGroupMember { SvcGroupId = 102, SvcObjectId = 202 }]
                    }
                ]);
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowSvcObject>>(FlowQueries.getFlowSyncSvcObjects, Arg.Any<object>())
                .Returns(
                [
                    new FlowSvcObject { Id = 202, Name = "https", PortStart = 443, PortEnd = 443, ProtoId = 6 }
                ]);

            WfReqTask ruleTask = CreateEligibleRequestTask(27);
            NwServiceElement service = ruleTask.GetServiceElements().Single();
            service.Name = null;
            service.Port = null;
            service.PortEnd = null;
            service.ProtoId = 0;
            service.GroupName = "web-services";
            service.FlowServiceGroupId = 102;

            List<Rule> rules = await BuildRulesFromRequestTasks(ruleTask);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].Services, Has.Length.EqualTo(1));
            Assert.That(rules[0].Services[0].Content.DestinationPort, Is.EqualTo(443));
            Assert.That(rules[0].Services[0].Content.ProtoId, Is.EqualTo(6));
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_ReturnsNoRulesWhenGroupCannotBeResolved()
        {
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowNwGroup>>(FlowQueries.getFlowSyncNwGroups, Arg.Any<object>())
                .Returns([]);
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowNwObject>>(FlowQueries.getFlowSyncNwObjects, Arg.Any<object>())
                .Returns([]);
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowSvcGroup>>(FlowQueries.getFlowSyncSvcGroups, Arg.Any<object>())
                .Returns([]);
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowSvcObject>>(FlowQueries.getFlowSyncSvcObjects, Arg.Any<object>())
                .Returns([]);

            WfReqTask ruleTask = CreateEligibleRequestTask(28);
            WfReqElement destination = ruleTask.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.IpString = null;
            destination.GroupName = "missing-group";

            List<Rule> rules = await BuildRulesFromRequestTasks(ruleTask);

            Assert.That(rules, Is.Empty);
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_MergesDuplicateNetworkGroupTasks()
        {
            WfReqTask firstGroupTask = new()
            {
                TaskType = WfTaskType.group_modify.ToString(),
                AdditionalInfo = "{\"GrpName\":\"app-servers\"}",
                Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "192.0.2.10", Name = "app-server-1" }]
            };
            WfReqTask secondGroupTask = new()
            {
                TaskType = WfTaskType.group_modify.ToString(),
                AdditionalInfo = "{\"GrpName\":\"app-servers\"}",
                Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "192.0.2.11", Name = "app-server-2" }]
            };
            WfReqTask ruleTask = CreateEligibleRequestTask(29);
            WfReqElement destination = ruleTask.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.IpString = null;
            destination.GroupName = "app-servers";

            List<Rule> rules = await BuildRulesFromRequestTasks(ruleTask, firstGroupTask, secondGroupTask);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].Tos.Select(location => location.Object.IP), Is.EquivalentTo(["192.0.2.10/32", "192.0.2.11/32"]));
        }

        [Test]
        public void ComplianceRequestedRulePolicyCheckerFactory_CreatesChecker()
        {
            ComplianceRequestedRulePolicyCheckerFactory factory = new();

            IRequestedRulePolicyChecker result = factory.Create(UserConfig, ApiConnection);

            Assert.That(result, Is.TypeOf<ComplianceRequestedRulePolicyChecker>());
        }

        [Test]
        public void ComplianceRequestedRulePolicyCheckerFactory_CanBeResolvedFromServices()
        {
            using ServiceProvider services = new ServiceCollection()
                .AddSingleton<IRequestedRulePolicyCheckerFactory, ComplianceRequestedRulePolicyCheckerFactory>()
                .BuildServiceProvider();

            IRequestedRulePolicyCheckerFactory? factory = services.GetService<IRequestedRulePolicyCheckerFactory>();

            Assert.That(factory, Is.TypeOf<ComplianceRequestedRulePolicyCheckerFactory>());
        }

        private static WfReqTask CreateEligibleRequestTask(long id, string? taskType = null, string? requestAction = null)
        {
            return new WfReqTask
            {
                Id = id,
                ManagementId = 1,
                Title = $"Request {id}",
                TaskType = taskType ?? WfTaskType.access.ToString(),
                RequestAction = requestAction ?? nameof(RequestAction.create),
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1/32", Name = "src" },
                    new WfReqElement { Field = ElemFieldType.destination.ToString(), IpString = "10.0.1.1/32", Name = "dst" },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6, Name = "https" },
                    new WfReqElement { Field = ElemFieldType.rule.ToString(), RuleUid = $"rule-{id}" }
                ]
            };
        }

        private async Task<List<Rule>> BuildRulesFromRequestTasks(params WfReqTask[] requestTasks)
        {
            MethodInfo method = typeof(ComplianceRequestedRulePolicyChecker).GetMethod("BuildRulesFromRequestTasks", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new AssertionException("BuildRulesFromRequestTasks method not found.");
            object? result = method.Invoke(checker, [requestTasks.AsEnumerable()]);
            Task<List<Rule>> task = result as Task<List<Rule>> ?? throw new AssertionException("BuildRulesFromRequestTasks returned unexpected result.");
            return await task;
        }
    }
}
