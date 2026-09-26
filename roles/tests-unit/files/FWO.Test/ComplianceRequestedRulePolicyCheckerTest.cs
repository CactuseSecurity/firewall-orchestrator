using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Compliance;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Services.Workflow;
using FWO.Test.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using System.Reflection;
using System.Net;
using RestSharp;

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
        public async Task AreRequestTasksCompliant_ReturnsFalseWhenAnEligibleTaskCannotBeMapped()
        {
            WfReqTask unmappableTask = CreateEligibleRequestTask(15);
            unmappableTask.Elements.Single(element => element.Field == ElemFieldType.source.ToString()).IpString = null;

            bool result = await checker.AreRequestTasksCompliant([5], [unmappableTask, CreateEligibleRequestTask(16)]);

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
                        ShowInRequestModule = true,
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
                    new FlowNwObject { Id = 201, Name = "app-server-1", IpStart = "192.0.2.10", IpEnd = "192.0.2.10", ShowInRequestModule = true }
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
                        ShowInRequestModule = true,
                        SvcGroupMembers =
                        [
                            new FlowSvcGroupMember { SvcGroupId = 102, SvcObjectId = 202 },
                            new FlowSvcGroupMember { SvcGroupId = 102, SvcObjectId = 203 }
                        ]
                    }
                ]);
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowSvcObject>>(FlowQueries.getFlowSyncSvcObjects, Arg.Any<object>())
                .Returns(
                [
                    new FlowSvcObject { Id = 202, Name = "https", PortStart = 443, PortEnd = 443, ProtoId = 6, ShowInRequestModule = true },
                    new FlowSvcObject { Id = 203, Name = "denied", PortStart = 444, PortEnd = 444, ProtoId = 6, ShowInRequestModule = true, State = FlowState.Denied }
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
        public async Task BuildRulesFromRequestTasks_ResolvesGroupsByUniqueNameAndSkipsRemovedFlowRecords()
        {
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowNwGroup>>(FlowQueries.getFlowSyncNwGroups, Arg.Any<object>())
                .Returns(
                [
                    new FlowNwGroup
                    {
                        Name = "named-network-group",
                        ShowInRequestModule = true,
                        NwGroupMembers =
                        [
                            new FlowNwGroupMember { NwObjectId = 401 },
                            new FlowNwGroupMember { NwObjectId = 402 }
                        ]
                    },
                    new FlowNwGroup { Name = "removed-network-group", State = FlowState.Removed },
                    new FlowNwGroup { Name = "denied-network-group", State = FlowState.Denied, ShowInRequestModule = true }
                ]);
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowNwObject>>(FlowQueries.getFlowSyncNwObjects, Arg.Any<object>())
                .Returns(
                [
                    new FlowNwObject { Id = 401, IpStart = "192.0.2.40", IpEnd = "192.0.2.40", ShowInRequestModule = true },
                    new FlowNwObject { Id = 402, IpStart = "192.0.2.41", IpEnd = "192.0.2.41", State = FlowState.Removed },
                    new FlowNwObject { Id = 403, IpStart = "192.0.2.43", IpEnd = "192.0.2.43", State = FlowState.Denied, ShowInRequestModule = true }
                ]);

            WfReqTask task = CreateEligibleRequestTask(32);
            WfReqElement destination = task.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.IpString = null;
            destination.GroupName = "named-network-group";

            List<Rule> rules = await BuildRulesFromRequestTasks(task);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].Tos.Select(location => location.Object.IP), Is.EqualTo(["192.0.2.40/32"]));
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_DoesNotResolveAmbiguousFlowGroupName()
        {
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowNwGroup>>(FlowQueries.getFlowSyncNwGroups, Arg.Any<object>())
                .Returns(
                [
                    new FlowNwGroup { Name = "ambiguous-group", ShowInRequestModule = true },
                    new FlowNwGroup { Name = "ambiguous-group", ShowInRequestModule = true }
                ]);
            ApiConnection.AsSub()
                .SendQueryAsync<List<FlowNwObject>>(FlowQueries.getFlowSyncNwObjects, Arg.Any<object>())
                .Returns([]);

            WfReqTask task = CreateEligibleRequestTask(33);
            WfReqElement destination = task.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.IpString = null;
            destination.GroupName = "ambiguous-group";

            List<Rule> rules = await BuildRulesFromRequestTasks(task);

            Assert.That(rules, Is.Empty);
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
        public async Task BuildRulesFromRequestTasks_ResolvesNetworkAndServiceGroupsThroughMiddleware()
        {
            TestFlowGroupMiddlewareClient middlewareClient = new()
            {
                Result = new FlowGroupResolutionResult
                {
                    NetworkGroups = [new FlowNetworkGroupResolution
                    {
                        Id = 501,
                        Name = "remote-destinations",
                        Members = [new FlowNetworkMemberResolution
                        {
                            Id = 301, Name = "remote-host", IpStart = "192.0.2.30", IpEnd = "192.0.2.30"
                        }]
                    }],
                    ServiceGroups = [new FlowServiceGroupResolution
                    {
                        Id = 502,
                        Name = "remote-services",
                        Members = [new FlowServiceMemberResolution
                        {
                            Id = 302, Name = "https", PortStart = 443, PortEnd = 443, ProtoId = 6
                        }]
                    }]
                }
            };
            ComplianceRequestedRulePolicyChecker middlewareChecker = new(UserConfig, ApiConnection, middlewareClient);
            WfReqTask task = CreateEligibleRequestTask(30);
            WfReqElement destination = task.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.IpString = null;
            destination.GroupName = "remote-destinations";
            destination.FlowNetworkGroupId = 501;
            WfReqElement service = task.Elements.Single(element => element.Field == ElemFieldType.service.ToString());
            service.Name = null;
            service.Port = null;
            service.ProtoId = 0;
            service.GroupName = "remote-services";
            service.FlowServiceGroupId = 502;

            List<Rule> rules = await InvokeBuildRules(middlewareChecker, task);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].Tos[0].Object.IP, Is.EqualTo("192.0.2.30/32"));
            Assert.That(rules[0].Services[0].Content.DestinationPort, Is.EqualTo(443));
            Assert.That(middlewareClient.Requests, Has.Count.EqualTo(2));
            Assert.That(middlewareClient.Requests.SelectMany(request => request.NetworkGroupIds), Does.Contain(501));
            Assert.That(middlewareClient.Requests.SelectMany(request => request.ServiceGroupIds), Does.Contain(502));
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_UsesRegisteredFlowGroupResolver()
        {
            TestFlowGroupResolver resolver = new()
            {
                Result = new FlowGroupResolutionResult
                {
                    NetworkGroups = [new FlowNetworkGroupResolution
                    {
                        Id = 601,
                        Name = "resolver-destinations",
                        Members = [new FlowNetworkMemberResolution
                        {
                            Id = 701, Name = "resolver-host", IpStart = "192.0.2.60", IpEnd = "192.0.2.60"
                        }]
                    }],
                    ServiceGroups = [new FlowServiceGroupResolution
                    {
                        Id = 602,
                        Name = "resolver-services",
                        Members = [new FlowServiceMemberResolution
                        {
                            Id = 702, Name = "resolver-https", PortStart = 443, PortEnd = 443, ProtoId = 6
                        }]
                    }]
                }
            };
            IServiceProvider? originalServices = FWO.Services.ServiceProvider.Services;
            FWO.Services.ServiceProvider.Services = new ServiceCollection()
                .AddSingleton<IFlowGroupResolver>(resolver)
                .BuildServiceProvider();

            try
            {
                ComplianceRequestedRulePolicyChecker resolverChecker = new(UserConfig, ApiConnection);
                WfReqTask task = CreateEligibleRequestTask(39);
                WfReqElement destination = task.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
                destination.IpString = null;
                destination.GroupName = "resolver-destinations";
                destination.FlowNetworkGroupId = 601;
                WfReqElement service = task.Elements.Single(element => element.Field == ElemFieldType.service.ToString());
                service.Name = null;
                service.Port = null;
                service.GroupName = "resolver-services";
                service.FlowServiceGroupId = 602;

                List<Rule> rules = await InvokeBuildRules(resolverChecker, task);

                Assert.Multiple(() =>
                {
                    Assert.That(rules, Has.Count.EqualTo(1));
                    Assert.That(rules[0].Tos[0].Object.IP, Is.EqualTo("192.0.2.60/32"));
                    Assert.That(rules[0].Services[0].Content.DestinationPort, Is.EqualTo(443));
                    Assert.That(resolver.Requests, Has.Count.EqualTo(2));
                    Assert.That(resolver.Requests.SelectMany(request => request.NetworkGroupIds), Is.EqualTo([601]));
                    Assert.That(resolver.Requests.SelectMany(request => request.ServiceGroupIds), Is.EqualTo([602]));
                });
            }
            finally
            {
                FWO.Services.ServiceProvider.Services = originalServices;
            }
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_ReturnsNoRulesWhenMappedRuleHasNoTechnicalValues()
        {
            WfReqTask task = CreateEligibleRequestTask(31);
            task.Elements.Single(element => element.Field == ElemFieldType.source.ToString()).IpString = null;

            List<Rule> rules = await BuildRulesFromRequestTasks(task);

            Assert.That(rules, Is.Empty);
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_SkipsUnmappableTaskButKeepsOtherRules()
        {
            WfReqTask unmappableTask = CreateEligibleRequestTask(35);
            unmappableTask.Elements.Single(element => element.Field == ElemFieldType.source.ToString()).IpString = null;

            WfReqTask validTask = CreateEligibleRequestTask(36);

            List<Rule> rules = await BuildRulesFromRequestTasks(unmappableTask, validTask);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].Uid, Is.EqualTo("rule-36"));
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_DoesNotMergeNameMatchIntoIdMatch()
        {
            TestFlowGroupMiddlewareClient middlewareClient = new()
            {
                Result = new FlowGroupResolutionResult
                {
                    NetworkGroups =
                    [
                        new FlowNetworkGroupResolution
                        {
                            Id = 501,
                            Name = "flow-name",
                            Members = [new FlowNetworkMemberResolution { Id = 1, IpStart = "192.0.2.1", IpEnd = "192.0.2.1" }]
                        },
                        new FlowNetworkGroupResolution
                        {
                            Id = 55,
                            Name = "requested-name",
                            Members = [new FlowNetworkMemberResolution { Id = 2, IpStart = "192.0.2.2", IpEnd = "192.0.2.2" }]
                        }
                    ]
                }
            };
            ComplianceRequestedRulePolicyChecker middlewareChecker = new(UserConfig, ApiConnection, middlewareClient);
            WfReqTask task = CreateEligibleRequestTask(37);
            WfReqElement destination = task.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.IpString = null;
            destination.GroupName = "requested-name";
            destination.FlowNetworkGroupId = 501;

            List<Rule> rules = await InvokeBuildRules(middlewareChecker, task);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].Tos.Select(location => location.Object.IP), Is.EqualTo(["192.0.2.1/32"]));
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_StoresIdResolvedGroupUnderAliasAndRealName()
        {
            TestFlowGroupMiddlewareClient middlewareClient = new()
            {
                Result = new FlowGroupResolutionResult
                {
                    NetworkGroups = [new FlowNetworkGroupResolution
                    {
                        Id = 501,
                        Name = "real-name",
                        Members = [new FlowNetworkMemberResolution
                        {
                            Id = 701, Name = "host", IpStart = "192.0.2.50", IpEnd = "192.0.2.50"
                        }]
                    }]
                }
            };
            ComplianceRequestedRulePolicyChecker middlewareChecker = new(UserConfig, ApiConnection, middlewareClient);
            WfReqTask aliasTask = CreateEligibleRequestTask(40);
            WfReqElement aliasDestination = aliasTask.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            aliasDestination.IpString = null;
            aliasDestination.GroupName = "alias-name";
            aliasDestination.FlowNetworkGroupId = 501;
            WfReqTask realNameTask = CreateEligibleRequestTask(41);
            WfReqElement realDestination = realNameTask.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            realDestination.IpString = null;
            realDestination.GroupName = "real-name";

            List<Rule> rules = await InvokeBuildRules(middlewareChecker, aliasTask, realNameTask);

            Assert.That(rules, Has.Count.EqualTo(2));
            Assert.That(rules.SelectMany(rule => rule.Tos).Select(location => location.Object.IP),
                Is.EqualTo(["192.0.2.50/32", "192.0.2.50/32"]));
        }

        [Test]
        public async Task BuildRulesFromRequestTasks_ChunksMiddlewareGroupResolutionSelectors()
        {
            TestFlowGroupMiddlewareClient middlewareClient = new();
            ComplianceRequestedRulePolicyChecker middlewareChecker = new(UserConfig, ApiConnection, middlewareClient);
            WfReqTask task = CreateEligibleRequestTask(38);
            WfReqElement destination = task.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.IpString = null;
            destination.GroupName = "network-group-0";
            destination.FlowNetworkGroupId = 1000;
            task.Elements.AddRange(Enumerable.Range(1, 100).Select(index => new WfReqElement
            {
                Field = ElemFieldType.destination.ToString(),
                GroupName = $"network-group-{index}",
                FlowNetworkGroupId = 1000 + index
            }));

            await InvokeBuildRules(middlewareChecker, task);

            Assert.That(middlewareClient.Requests, Has.Count.EqualTo(2));
            Assert.That(middlewareClient.Requests.All(request => request.NetworkGroupIds.Count <= 100), Is.True);
            Assert.That(middlewareClient.Requests.SelectMany(request => request.NetworkGroupIds).ToList(), Has.Count.EqualTo(101));
        }

        [Test]
        public void BuildRulesFromRequestTasks_ThrowsWhenMiddlewareGroupResolutionFails()
        {
            TestFlowGroupMiddlewareClient middlewareClient = new() { FailureStatus = HttpStatusCode.Forbidden };
            ComplianceRequestedRulePolicyChecker middlewareChecker = new(UserConfig, ApiConnection, middlewareClient);
            WfReqTask task = CreateEligibleRequestTask(34);
            WfReqElement destination = task.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.IpString = null;
            destination.GroupName = "forbidden-group";

            Assert.ThrowsAsync<InvalidOperationException>(async () => await InvokeBuildRules(middlewareChecker, task));
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
            return await InvokeBuildRules(checker, requestTasks);
        }

        private static async Task<List<Rule>> InvokeBuildRules(ComplianceRequestedRulePolicyChecker policyChecker, params WfReqTask[] requestTasks)
        {
            MethodInfo method = typeof(ComplianceRequestedRulePolicyChecker).GetMethod("BuildRuleAssessmentFromRequestTasks", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new AssertionException("BuildRuleAssessmentFromRequestTasks method not found.");
            object? result = method.Invoke(policyChecker, [requestTasks.AsEnumerable()]);
            Task task = result as Task ?? throw new AssertionException("BuildRuleAssessmentFromRequestTasks returned unexpected result.");
            await task;
            object assessment = task.GetType().GetProperty("Result")?.GetValue(task)
                ?? throw new AssertionException("Rule assessment result was missing.");
            return (List<Rule>)(assessment.GetType().GetProperty("Rules")?.GetValue(assessment)
                ?? throw new AssertionException("Rule assessment rules were missing."));
        }

        private sealed class TestFlowGroupMiddlewareClient : MiddlewareClient
        {
            public FlowGroupResolutionResult Result { get; set; } = new();
            public List<FlowGroupResolutionParameters> Requests { get; } = [];
            public HttpStatusCode? FailureStatus { get; set; }

            public TestFlowGroupMiddlewareClient() : base("https://middleware.example/")
            { }

            public override Task<RestResponse<FlowGroupResolutionResult>> ResolveFlowGroupMembers(FlowGroupResolutionParameters parameters)
            {
                Requests.Add(parameters);
                RestResponse<FlowGroupResolutionResult> response = new(new RestRequest())
                {
                    Data = Result,
                    StatusCode = HttpStatusCode.OK,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = true
                };
                if (FailureStatus.HasValue)
                {
                    response.StatusCode = FailureStatus.Value;
                    response.Data = null;
                    response.IsSuccessStatusCode = false;
                }
                return Task.FromResult(response);
            }
        }

        private sealed class TestFlowGroupResolver : IFlowGroupResolver
        {
            public FlowGroupResolutionResult Result { get; set; } = new();
            public List<FlowGroupResolutionParameters> Requests { get; } = [];

            public Task<FlowGroupResolutionResult> ResolveFlowGroupMembersAsync(FlowGroupResolutionParameters parameters)
            {
                Requests.Add(parameters);
                return Task.FromResult(Result);
            }
        }
    }
}
