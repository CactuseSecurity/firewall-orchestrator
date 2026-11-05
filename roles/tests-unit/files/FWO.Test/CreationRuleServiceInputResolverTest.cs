using FWO.Basics;
using FWO.Compliance.RulePositioning;
using FWO.Data.Middleware;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;
using CreationRuleServiceResolutionResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Data.RulePositioning.CreationRuleServiceInput,
    FWO.Compliance.RulePositioning.CreationRuleServiceResolutionFailureCategory>;

namespace FWO.Test;

[TestFixture]
public class CreationRuleServiceInputResolverTest
{
    [TestCase(CreationRuleServiceResolutionFailureCategory.InvalidInput)]
    [TestCase(CreationRuleServiceResolutionFailureCategory.UnsupportedTask)]
    [TestCase(CreationRuleServiceResolutionFailureCategory.ProtocolOrderInvalid)]
    [TestCase(CreationRuleServiceResolutionFailureCategory.ServiceUnresolved)]
    [TestCase(CreationRuleServiceResolutionFailureCategory.GroupUnresolved)]
    [TestCase(CreationRuleServiceResolutionFailureCategory.GroupAmbiguous)]
    [TestCase(CreationRuleServiceResolutionFailureCategory.GroupQueryFailed)]
    [TestCase(CreationRuleServiceResolutionFailureCategory.InvalidService)]
    [TestCase(CreationRuleServiceResolutionFailureCategory.UnexpectedFailure)]
    [TestCase((CreationRuleServiceResolutionFailureCategory)999)]
    public void FailureDiagnostics_AreFixedAndSafe(CreationRuleServiceResolutionFailureCategory category)
    {
        Assert.Multiple(() =>
        {
            Assert.That(category.GetDiagnostic(), Is.Not.Empty);
            Assert.That(category.GetDiagnostic(), Does.Not.Contain("customer-secret"));
        });
    }

    [Test]
    public async Task DirectServices_AreNormalizedAndSemanticallyDeduplicated()
    {
        WfReqTask access = Access(Service(6, 443, null), Service(6, 443, 443), Service(17, 53, 53));

        CreationRuleServiceInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.Services, Has.Count.EqualTo(2));
            Assert.That(input.Services.Single(service => service.ProtocolId == 6).DestinationPortEnd,
                Is.EqualTo(443));
        });
    }

    [Test]
    public async Task CanonicalAny_IsPreserved()
    {
        WfReqTask access = Access(Service(GlobalConst.kAnyIpProtocolId, null, null));

        CreationRuleServiceInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, [access], CancellationToken.None));

        Assert.That(input.Services.Single().ProtocolId, Is.EqualTo(GlobalConst.kAnyIpProtocolId));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CatalogGroup_ResolvesByIdOrName(bool useId)
    {
        WfReqElement reference = new()
        {
            Field = ElemFieldType.service.ToString(),
            FlowServiceGroupId = useId ? 7 : null,
            GroupName = useId ? null : "web"
        };
        WfReqTask access = Access(reference);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            ServiceGroups =
            [
                new FlowServiceGroupResolution
                {
                    Id = 7,
                    Name = "web",
                    Members = [new FlowServiceMemberResolution { Id = 8, ProtoId = 6, PortStart = 443, PortEnd = 443 }]
                }
            ]
        });

        CreationRuleServiceInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.Services.Single().DestinationPortStart, Is.EqualTo(443));
            Assert.That(groups.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ExplicitGroupMarkers_TakePrecedenceWhenWorkflowAlsoPopulatesName()
    {
        WfReqElement reference = GroupReference("web");
        reference.Name = "web";
        reference.FlowServiceGroupId = 7;
        WfReqTask access = Access(reference);
        FakeFlowGroupResolver groups = CatalogGroup(7, "web", 8, 6, 443);

        CreationRuleServiceInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.Services.Single().DestinationPortStart, Is.EqualTo(443));
            Assert.That(groups.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task NamedServiceObjectReference_DoesNotTriggerGroupLookup()
    {
        WfReqElement reference = new()
        {
            Field = ElemFieldType.service.ToString(),
            Name = "named-service",
            FlowServiceObjectId = 9
        };
        WfReqTask access = Access(reference);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult());

        CreationRuleServiceResolutionFailureCategory failure = AssertFailure(
            await Resolver(groups).ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(failure, Is.EqualTo(CreationRuleServiceResolutionFailureCategory.ServiceUnresolved));
            Assert.That(groups.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task TicketCreatedGroup_ExpandsNestedGroupWithoutCatalogLookup()
    {
        WfReqTask nested = GroupTask(WfTaskType.group_create, 1, 1, "nested", Service(6, 443, 443));
        WfReqTask outer = GroupTask(WfTaskType.group_create, 2, 2, "outer", GroupReference("nested"));
        WfReqTask access = Access(3, 3, GroupReference("outer"));
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult());

        CreationRuleServiceInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, [nested, outer, access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.Services.Single().DestinationPortStart, Is.EqualTo(443));
            Assert.That(groups.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task PriorServiceObjectModify_ReplacesReferencedLeaf()
    {
        WfReqElement reference = Service(6, 80, 80);
        reference.FlowServiceObjectId = 9;
        WfReqTask modify = ObjectModify(1, 1, 9, Service(6, 80, 80), Service(6, 443, 443));
        WfReqTask access = Access(2, 2, reference);

        CreationRuleServiceInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, [modify, access], CancellationToken.None));

        Assert.That(input.Services.Single().DestinationPortStart, Is.EqualTo(443));
    }

    [Test]
    public async Task GroupModify_FoldsCatalogBaselineAndMemberActions()
    {
        WfReqElement replacement = Service(6, 8443, 8443, RequestAction.modify);
        replacement.FlowServiceObjectId = 8;
        WfReqTask modify = GroupTask(WfTaskType.group_modify, 1, 1, "web", replacement);
        modify.SetAddInfo(AdditionalInfoKeys.SvcGrpId, "7");
        WfReqTask access = Access(2, 2, new WfReqElement
        {
            Field = ElemFieldType.service.ToString(),
            FlowServiceGroupId = 7,
            GroupName = "web"
        });
        FakeFlowGroupResolver groups = CatalogGroup(7, "web", 8, 6, 443);

        CreationRuleServiceInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, [modify, access], CancellationToken.None));

        Assert.That(input.Services.Single().DestinationPortStart, Is.EqualTo(8443));
    }

    [Test]
    public async Task DeletedCyclicAndLaterTicketState_FailsClosed()
    {
        WfReqTask created = GroupTask(WfTaskType.group_create, 1, 1, "gone", Service(6, 443, 443));
        WfReqTask deleted = GroupTask(WfTaskType.group_delete, 2, 2, "gone");
        created.SetAddInfo(AdditionalInfoKeys.SvcGrpId, "77");
        deleted.SetAddInfo(AdditionalInfoKeys.SvcGrpId, "77");
        WfReqElement deletedReference = GroupReference("gone");
        deletedReference.FlowServiceGroupId = 77;
        WfReqTask deletedAccess = Access(3, 3, deletedReference);
        WfReqTask cycleA = GroupTask(WfTaskType.group_create, 4, 1, "a", GroupReference("b"));
        WfReqTask cycleB = GroupTask(WfTaskType.group_create, 5, 2, "b", GroupReference("a"));
        WfReqTask cycleAccess = Access(6, 3, GroupReference("a"));
        WfReqTask laterObject = ObjectModify(8, 2, 9, Service(6, 80, 80), Service(6, 443, 443));
        WfReqElement laterReference = Service(6, 80, 80);
        laterReference.FlowServiceObjectId = 9;
        WfReqTask laterAccess = Access(7, 1, laterReference);

        CreationRuleServiceResolutionFailureCategory deletedFailure = AssertFailure(
            await Resolver().ResolveAsync(deletedAccess, [created, deleted, deletedAccess], CancellationToken.None));
        CreationRuleServiceResolutionFailureCategory cycleFailure = AssertFailure(
            await Resolver().ResolveAsync(cycleAccess, [cycleA, cycleB, cycleAccess], CancellationToken.None));
        CreationRuleServiceResolutionFailureCategory laterFailure = AssertFailure(
            await Resolver().ResolveAsync(laterAccess, [laterAccess, laterObject], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(deletedFailure, Is.EqualTo(CreationRuleServiceResolutionFailureCategory.GroupUnresolved));
            Assert.That(cycleFailure, Is.EqualTo(CreationRuleServiceResolutionFailureCategory.GroupUnresolved));
            Assert.That(laterFailure, Is.EqualTo(CreationRuleServiceResolutionFailureCategory.ProtocolOrderInvalid));
        });
    }

    [TestCase(null, 443, 443)]
    [TestCase(6, null, 443)]
    [TestCase(1, 443, 443)]
    [TestCase(-2, null, null)]
    public async Task MalformedTechnicalService_ReturnsInvalidService(int? protocol, int? start, int? end)
    {
        WfReqTask access = Access(Service(protocol, start, end));

        CreationRuleServiceResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, [access], CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleServiceResolutionFailureCategory.InvalidService));
    }

    [Test]
    public async Task MissingAmbiguousAndQueryFailureCatalogGroups_AreDistinguished()
    {
        WfReqTask access = Access(GroupReference("secret-group"));
        FakeFlowGroupResolver duplicate = new(new FlowGroupResolutionResult
        {
            ServiceGroups =
            [
                new FlowServiceGroupResolution { Id = 1, Name = "secret-group" },
                new FlowServiceGroupResolution { Id = 2, Name = "SECRET-GROUP" }
            ]
        });
        FakeFlowGroupResolver throwing = new(new FlowGroupResolutionResult())
        {
            Exception = new InvalidOperationException("customer secret")
        };

        CreationRuleServiceResolutionFailureCategory missing = AssertFailure(
            await Resolver().ResolveAsync(access, [access], CancellationToken.None));
        CreationRuleServiceResolutionFailureCategory ambiguous = AssertFailure(
            await Resolver(duplicate).ResolveAsync(access, [access], CancellationToken.None));
        CreationRuleServiceResolutionFailureCategory query = AssertFailure(
            await Resolver(throwing).ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.EqualTo(CreationRuleServiceResolutionFailureCategory.GroupUnresolved));
            Assert.That(ambiguous, Is.EqualTo(CreationRuleServiceResolutionFailureCategory.GroupAmbiguous));
            Assert.That(query, Is.EqualTo(CreationRuleServiceResolutionFailureCategory.GroupQueryFailed));
            Assert.That(query.GetDiagnostic(), Does.Not.Contain("customer secret"));
        });
    }

    [Test]
    public void Cancellation_IsPropagatedBeforeCatalogLookup()
    {
        WfReqTask access = Access(GroupReference("web"));
        FakeFlowGroupResolver groups = CatalogGroup(7, "web", 8, 6, 443);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            Resolver(groups).ResolveAsync(access, [access], cancellation.Token));
        Assert.That(groups.CallCount, Is.Zero);
    }

    private static CreationRuleServiceInputResolver Resolver(IFlowGroupResolver? groups = null) =>
        new(groups ?? new FakeFlowGroupResolver(new FlowGroupResolutionResult()));

    private static WfReqTask Access(params WfReqElement[] services) => Access(10, 10, services);

    private static WfReqTask Access(long id, int taskNumber, params WfReqElement[] services) => new()
    {
        Id = id,
        TicketId = 100,
        TaskNumber = taskNumber,
        ManagementId = 10,
        TaskType = WfTaskType.access.ToString(),
        RequestAction = RequestAction.create.ToString(),
        Elements = [.. services]
    };

    private static WfReqTask GroupTask(
        WfTaskType type,
        long id,
        int taskNumber,
        string name,
        params WfReqElement[] members)
    {
        WfReqTask task = new()
        {
            Id = id,
            TicketId = 100,
            TaskNumber = taskNumber,
            ManagementId = 10,
            TaskType = type.ToString(),
            Elements = [.. members]
        };
        task.SetAddInfo(AdditionalInfoKeys.GrpName, name);
        return task;
    }

    private static WfReqTask ObjectModify(
        long id,
        int taskNumber,
        long objectId,
        WfReqElement original,
        WfReqElement requested)
    {
        original.FlowServiceObjectId = objectId;
        original.RequestAction = RequestAction.unchanged.ToString();
        requested.FlowServiceObjectId = objectId;
        requested.RequestAction = RequestAction.modify.ToString();
        return new WfReqTask
        {
            Id = id,
            TicketId = 100,
            TaskNumber = taskNumber,
            ManagementId = 10,
            TaskType = WfTaskType.object_modify.ToString(),
            Elements = [original, requested]
        };
    }

    private static WfReqElement Service(
        int? protocol,
        int? start,
        int? end,
        RequestAction action = RequestAction.create) => new()
        {
            Field = ElemFieldType.service.ToString(),
            ProtoId = protocol,
            Port = start,
            PortEnd = end,
            RequestAction = action.ToString()
        };

    private static WfReqElement GroupReference(string name) => new()
    {
        Field = ElemFieldType.service.ToString(),
        GroupName = name,
        RequestAction = RequestAction.create.ToString()
    };

    private static FakeFlowGroupResolver CatalogGroup(
        long groupId,
        string name,
        long memberId,
        int protocol,
        int port) => new(new FlowGroupResolutionResult
        {
            ServiceGroups =
            [
                new FlowServiceGroupResolution
                {
                    Id = groupId,
                    Name = name,
                    Members = [new FlowServiceMemberResolution
                    {
                        Id = memberId,
                        ProtoId = protocol,
                        PortStart = port,
                        PortEnd = port
                    }]
                }
            ]
        });

    private static CreationRuleServiceInput AssertSuccess(CreationRuleServiceResolutionResult result)
    {
        Assert.That(result.TryGetValue(out CreationRuleServiceInput? input), Is.True);
        return input!;
    }

    private static CreationRuleServiceResolutionFailureCategory AssertFailure(
        CreationRuleServiceResolutionResult result)
    {
        Assert.That(result.TryGetFailure(out CreationRuleServiceResolutionFailureCategory failure), Is.True);
        return failure;
    }

    private sealed class FakeFlowGroupResolver(FlowGroupResolutionResult result) : IFlowGroupResolver
    {
        internal Exception? Exception { get; init; }
        internal int CallCount { get; private set; }

        public Task<FlowGroupResolutionResult> ResolveFlowGroupMembersAsync(
            FlowGroupResolutionParameters parameters,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Exception != null)
            {
                throw Exception;
            }
            return Task.FromResult(result);
        }
    }
}
