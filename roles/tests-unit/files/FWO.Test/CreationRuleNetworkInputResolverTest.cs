using FWO.Compliance.RulePositioning;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class CreationRuleNetworkInputResolverTest
{
    [Test]
    public async Task DirectRanges_AreNormalizedDeduplicatedAndImmutable()
    {
        WfReqTask access = Access(
            [Leaf(ElemFieldType.source, "192.0.2.1", "192.0.2.1"), Leaf(ElemFieldType.source, "192.0.2.1", "192.0.2.1")],
            [Leaf(ElemFieldType.destination, "2001:db8::/126", null)]);
        List<WfReqTask> tasks = [access];

        CreationRuleNetworkResolutionSuccess success = (CreationRuleNetworkResolutionSuccess)
            await Resolver().ResolveAsync(access, tasks, CancellationToken.None);
        tasks.Clear();
        access.Elements.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(success.Input.SourceRanges, Has.Count.EqualTo(1));
            Assert.That(success.Input.SourceRanges[0].Begin.ToString(), Is.EqualTo("192.0.2.1"));
            Assert.That(success.Input.DestinationRanges[0].Begin.ToString(), Is.EqualTo("2001:db8::"));
            Assert.That(success.Input.DestinationRanges[0].End.ToString(), Is.EqualTo("2001:db8::3"));
            Assert.That(success.Input.SourceRanges, Is.Not.InstanceOf<List<NetTools.IPAddressRange>>());
        });
    }

    [Test]
    public async Task ValidCidr_TakesPrecedenceOverLegacyIpStrings()
    {
        WfReqElement source = Leaf(ElemFieldType.source, "192.0.2.99", "192.0.2.100");
        source.Cidr = new Cidr("10.10.10.0/30");
        WfReqTask access = Access(
            [source],
            [Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")]);

        CreationRuleNetworkResolutionSuccess success = (CreationRuleNetworkResolutionSuccess)
            await Resolver().ResolveAsync(access, [access], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(success.Input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.10.10.0"));
            Assert.That(success.Input.SourceRanges.Single().End.ToString(), Is.EqualTo("10.10.10.3"));
        });
    }

    [TestCase(null, "192.0.2.1", CreationRuleNetworkResolutionFailureCategory.InvalidInput)]
    [TestCase("not-an-ip", "192.0.2.1", CreationRuleNetworkResolutionFailureCategory.InvalidAddress)]
    public async Task InvalidInput_ReturnsTypedFailure(
        string? source,
        string destination,
        CreationRuleNetworkResolutionFailureCategory category)
    {
        WfReqTask access = Access(
            source == null ? [] : [Leaf(ElemFieldType.source, source, source)],
            [Leaf(ElemFieldType.destination, destination, destination)]);

        CreationRuleNetworkResolutionFailure failure = (CreationRuleNetworkResolutionFailure)
            await Resolver().ResolveAsync(access, [access], CancellationToken.None);

        Assert.That(failure.Category, Is.EqualTo(category));
        Assert.That(failure.Diagnostic, Does.Not.Contain(source ?? "ticket-secret"));
    }

    [Test]
    public async Task CatalogGroup_ResolvesByIdAndExpandsAllLeaves()
    {
        WfReqTask access = Access(
            [new WfReqElement { Field = "source", FlowNetworkGroupId = 7 }],
            [Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")]);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups =
            [
                new FlowNetworkGroupResolution
                {
                    Id = 7,
                    Name = "catalog",
                    Members =
                    [
                        new FlowNetworkMemberResolution { Id = 1, IpStart = "10.0.0.1", IpEnd = "10.0.0.1" },
                        new FlowNetworkMemberResolution { Id = 2, IpStart = "10.0.0.2", IpEnd = "10.0.0.2" }
                    ]
                }
            ]
        });

        CreationRuleNetworkResolutionSuccess success = (CreationRuleNetworkResolutionSuccess)
            await Resolver(groups).ResolveAsync(access, [access], CancellationToken.None);

        Assert.That(success.Input.SourceRanges.Select(range => range.Begin.ToString()),
            Is.EqualTo(new[] { "10.0.0.1", "10.0.0.2" }));
        Assert.That(groups.Parameters!.NetworkGroupIds, Is.EqualTo(new long[] { 7 }));
    }

    [Test]
    public async Task CatalogGroups_AreLoadedInBoundedChunks()
    {
        WfReqTask access = Access(
            Enumerable.Range(1, 101).Select(id => new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                FlowNetworkGroupId = id
            }),
            [Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")]);
        FakeFlowGroupResolver groups = new(parameters => new FlowGroupResolutionResult
        {
            NetworkGroups = [.. parameters.NetworkGroupIds.Select(id => new FlowNetworkGroupResolution
            {
                Id = id,
                Name = $"group-{id}",
                Members = [new FlowNetworkMemberResolution
                {
                    Id = id,
                    IpStart = $"10.0.{(id - 1) / 254}.{(id - 1) % 254 + 1}",
                    IpEnd = $"10.0.{(id - 1) / 254}.{(id - 1) % 254 + 1}"
                }]
            })]
        });

        CreationRuleNetworkResolutionSuccess success = (CreationRuleNetworkResolutionSuccess)
            await Resolver(groups).ResolveAsync(access, [access], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(success.Input.SourceRanges, Has.Count.EqualTo(101));
            Assert.That(groups.CallCount, Is.EqualTo(2));
            Assert.That(groups.AllParameters.SelectMany(item => item.NetworkGroupIds),
                Is.EquivalentTo(Enumerable.Range(1, 101).Select(id => (long)id)));
        });
    }

    [Test]
    public async Task TicketGroup_OverridesCatalogAndExpandsNestedGroup()
    {
        WfReqTask nested = GroupCreate(1, 1, "nested",
            [Leaf(ElemFieldType.source, "10.1.0.1", "10.1.0.1")]);
        WfReqTask outer = GroupCreate(2, 2, "outer",
            [new WfReqElement { Field = "source", GroupName = "nested", RequestAction = "create" }]);
        WfReqTask access = Access(
            [new WfReqElement { Field = "source", GroupName = "outer" }],
            [Leaf(ElemFieldType.destination, "203.0.113.5", "203.0.113.5")], 3, 3);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult());

        CreationRuleNetworkResolutionSuccess success = (CreationRuleNetworkResolutionSuccess)
            await Resolver(groups).ResolveAsync(access, [access, outer, nested], CancellationToken.None);

        Assert.That(success.Input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.1.0.1"));
        Assert.That(groups.CallCount, Is.Zero);
    }

    [Test]
    public async Task RelevantLaterObjectChange_IsProtocolOrderInvalid()
    {
        WfReqElement source = Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1");
        source.FlowNetworkObjectId = 9;
        WfReqTask access = Access([source], [Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")], 1, 1);
        WfReqTask later = new()
        {
            Id = 2,
            TicketId = 100,
            TaskNumber = 2,
            ManagementId = 10,
            TaskType = WfTaskType.object_modify.ToString(),
            Elements =
            [
                new WfReqElement { Field = "source", FlowNetworkObjectId = 9, IpString = "10.0.0.1", IpEnd = "10.0.0.1", RequestAction = "unchanged" },
                new WfReqElement { Field = "source", FlowNetworkObjectId = 9, IpString = "10.0.0.3", IpEnd = "10.0.0.3", RequestAction = "modify" }
            ]
        };

        CreationRuleNetworkResolutionFailure failure = (CreationRuleNetworkResolutionFailure)
            await Resolver().ResolveAsync(access, [later, access], CancellationToken.None);

        Assert.That(failure.Category, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.ProtocolOrderInvalid));
    }

    [Test]
    public void Cancellation_IsPropagatedBeforeWork()
    {
        WfReqTask access = Access(
            [Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1")],
            [Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")]);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            Resolver().ResolveAsync(access, [access], cancellation.Token));
    }

    [Test]
    public async Task UnsupportedTaskUnknownActionAndInvalidTicketScope_AreTyped()
    {
        WfReqTask unsupported = Access(
            [Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1")],
            [Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")]);
        unsupported.RequestAction = RequestAction.modify.ToString();
        WfReqTask unknownAction = Access(
            [Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1")],
            [Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")]);
        unknownAction.Elements[0].RequestAction = "unknown";
        WfReqTask invalidIdentity = Access(
            [Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1")],
            [Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")], 0);

        CreationRuleNetworkResolutionFailure unsupportedFailure = (CreationRuleNetworkResolutionFailure)
            await Resolver().ResolveAsync(unsupported, [unsupported], CancellationToken.None);
        CreationRuleNetworkResolutionFailure actionFailure = (CreationRuleNetworkResolutionFailure)
            await Resolver().ResolveAsync(unknownAction, [unknownAction], CancellationToken.None);
        CreationRuleNetworkResolutionFailure identityFailure = (CreationRuleNetworkResolutionFailure)
            await Resolver().ResolveAsync(invalidIdentity, [invalidIdentity], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(unsupportedFailure.Category,
                Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.UnsupportedTask));
            Assert.That(actionFailure.Category,
                Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.InvalidInput));
            Assert.That(identityFailure.Category,
                Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.InvalidInput));
        });
    }

    [Test]
    public async Task CatalogFailures_AreDistinguishedAndSanitized()
    {
        WfReqTask access = Access(
            [new WfReqElement { Field = "source", GroupName = "catalog" }],
            [Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")]);
        FakeFlowGroupResolver throwing = new(new FlowGroupResolutionResult())
        {
            Exception = new InvalidOperationException("secret")
        };
        FakeFlowGroupResolver missing = new(new FlowGroupResolutionResult());
        FakeFlowGroupResolver duplicate = new(new FlowGroupResolutionResult
        {
            NetworkGroups =
            [
                new FlowNetworkGroupResolution { Id = 1, Name = "catalog" },
                new FlowNetworkGroupResolution { Id = 2, Name = "CATALOG" }
            ]
        });

        CreationRuleNetworkResolutionFailure queryFailure = (CreationRuleNetworkResolutionFailure)
            await Resolver(throwing).ResolveAsync(access, [access], CancellationToken.None);
        CreationRuleNetworkResolutionFailure missingFailure = (CreationRuleNetworkResolutionFailure)
            await Resolver(missing).ResolveAsync(access, [access], CancellationToken.None);
        CreationRuleNetworkResolutionFailure duplicateFailure = (CreationRuleNetworkResolutionFailure)
            await Resolver(duplicate).ResolveAsync(access, [access], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(queryFailure.Category, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupQueryFailed));
            Assert.That(queryFailure.Diagnostic, Does.Not.Contain("secret"));
            Assert.That(missingFailure.Category, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
            Assert.That(duplicateFailure.Category, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupAmbiguous));
        });
    }

    [Test]
    public async Task PriorObjectModify_ReplacesDirectLeaf()
    {
        WfReqElement source = Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1");
        source.FlowNetworkObjectId = 9;
        WfReqTask modify = new()
        {
            Id = 1,
            TicketId = 100,
            TaskNumber = 1,
            ManagementId = 10,
            TaskType = WfTaskType.object_modify.ToString(),
            Elements =
            [
                new WfReqElement { Field = "source", FlowNetworkObjectId = 9, IpString = "10.0.0.1", IpEnd = "10.0.0.1", RequestAction = "unchanged" },
                new WfReqElement { Field = "source", FlowNetworkObjectId = 9, IpString = "10.0.0.9", IpEnd = "10.0.0.9", RequestAction = "modify" }
            ]
        };
        WfReqTask access = Access([source], [Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")], 2, 2);

        CreationRuleNetworkResolutionSuccess success = (CreationRuleNetworkResolutionSuccess)
            await Resolver().ResolveAsync(access, [access, modify], CancellationToken.None);

        Assert.That(success.Input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task GroupModify_FoldsCatalogBaselineActions()
    {
        WfReqTask modify = new()
        {
            Id = 1,
            TicketId = 100,
            TaskNumber = 1,
            ManagementId = 10,
            TaskType = WfTaskType.group_modify.ToString(),
            Elements =
            [
                Member(1, "10.0.0.1", RequestAction.unchanged),
                Member(1, "10.0.0.9", RequestAction.modify),
                Member(2, "10.0.0.2", RequestAction.addAfterCreation),
                Member(2, "10.0.0.2", RequestAction.delete)
            ]
        };
        modify.SetAddInfo(AdditionalInfoKeys.GroupId, "7");
        modify.SetAddInfo(AdditionalInfoKeys.GrpName, "catalog");
        WfReqTask access = Access(
            [new WfReqElement { Field = "source", FlowNetworkGroupId = 7, GroupName = "catalog" }],
            [Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")], 2, 2);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups =
            [
                new FlowNetworkGroupResolution
                {
                    Id = 7,
                    Name = "catalog",
                    Members = [new FlowNetworkMemberResolution { Id = 1, Name = "one", IpStart = "10.0.0.1", IpEnd = "10.0.0.1" }]
                }
            ]
        });

        CreationRuleNetworkResolutionSuccess success = (CreationRuleNetworkResolutionSuccess)
            await Resolver(groups).ResolveAsync(access, [access, modify], CancellationToken.None);

        Assert.That(success.Input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task DeletedCyclicAndLaterGroups_FailClosed()
    {
        WfReqTask created = GroupCreate(1, 1, "deleted",
            [Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1")]);
        WfReqTask deleted = new()
        {
            Id = 2,
            TicketId = 100,
            TaskNumber = 2,
            ManagementId = 10,
            TaskType = WfTaskType.group_delete.ToString()
        };
        deleted.SetAddInfo(AdditionalInfoKeys.GrpName, "deleted");
        WfReqTask deletedAccess = Access(
            [new WfReqElement { Field = "source", GroupName = "deleted" }],
            [Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")], 3, 3);

        WfReqTask cycleA = GroupCreate(4, 1, "a",
            [new WfReqElement { Field = "source", GroupName = "b", RequestAction = "create" }]);
        WfReqTask cycleB = GroupCreate(5, 2, "b",
            [new WfReqElement { Field = "source", GroupName = "a", RequestAction = "create" }]);
        WfReqTask cycleAccess = Access(
            [new WfReqElement { Field = "source", GroupName = "a" }],
            [Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")], 6, 3);

        WfReqTask later = GroupCreate(8, 2, "later",
            [Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8")]);
        WfReqTask laterAccess = Access(
            [new WfReqElement { Field = "source", GroupName = "later" }],
            [Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")], 7, 1);

        CreationRuleNetworkResolutionFailure deletedFailure = (CreationRuleNetworkResolutionFailure)
            await Resolver().ResolveAsync(deletedAccess, [deletedAccess, created, deleted], CancellationToken.None);
        CreationRuleNetworkResolutionFailure cycleFailure = (CreationRuleNetworkResolutionFailure)
            await Resolver().ResolveAsync(cycleAccess, [cycleAccess, cycleA, cycleB], CancellationToken.None);
        CreationRuleNetworkResolutionFailure laterFailure = (CreationRuleNetworkResolutionFailure)
            await Resolver().ResolveAsync(laterAccess, [laterAccess, later], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(deletedFailure.Category,
                Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
            Assert.That(cycleFailure.Category,
                Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
            Assert.That(laterFailure.Category,
                Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.ProtocolOrderInvalid));
        });
    }

    private static CreationRuleNetworkInputResolver Resolver(IFlowGroupResolver? groups = null) =>
        new(groups ?? new FakeFlowGroupResolver(new FlowGroupResolutionResult()));

    private static WfReqTask Access(
        IEnumerable<WfReqElement> source,
        IEnumerable<WfReqElement> destination,
        long id = 10,
        int taskNumber = 10) => new()
        {
            Id = id,
            TicketId = 100,
            TaskNumber = taskNumber,
            ManagementId = 10,
            TaskType = WfTaskType.access.ToString(),
            RequestAction = RequestAction.create.ToString(),
            Elements = [.. source, .. destination]
        };

    private static WfReqTask GroupCreate(long id, int taskNumber, string name, IEnumerable<WfReqElement> members)
    {
        WfReqTask task = new()
        {
            Id = id,
            TicketId = 100,
            TaskNumber = taskNumber,
            ManagementId = 10,
            TaskType = WfTaskType.group_create.ToString(),
            Elements = [.. members]
        };
        task.SetAddInfo(AdditionalInfoKeys.GrpName, name);
        return task;
    }

    private static WfReqElement Leaf(ElemFieldType field, string ip, string? end) => new()
    {
        Field = field.ToString(),
        IpString = ip,
        IpEnd = end,
        RequestAction = RequestAction.create.ToString()
    };

    private static WfReqElement Member(long id, string ip, RequestAction action) => new()
    {
        Field = ElemFieldType.source.ToString(),
        FlowNetworkObjectId = id,
        Name = id.ToString(),
        IpString = ip,
        IpEnd = ip,
        RequestAction = action.ToString()
    };

    private sealed class FakeFlowGroupResolver : IFlowGroupResolver
    {
        private readonly Func<FlowGroupResolutionParameters, FlowGroupResolutionResult> resultFactory;

        internal FakeFlowGroupResolver(FlowGroupResolutionResult result)
            : this(_ => result)
        { }

        internal FakeFlowGroupResolver(Func<FlowGroupResolutionParameters, FlowGroupResolutionResult> resultFactory)
        {
            this.resultFactory = resultFactory;
        }

        internal Exception? Exception { get; init; }
        internal int CallCount { get; private set; }
        internal FlowGroupResolutionParameters? Parameters { get; private set; }
        internal List<FlowGroupResolutionParameters> AllParameters { get; } = [];

        public Task<FlowGroupResolutionResult> ResolveFlowGroupMembersAsync(
            FlowGroupResolutionParameters parameters,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Parameters = parameters;
            AllParameters.Add(parameters);
            if (Exception != null)
            {
                throw Exception;
            }
            return Task.FromResult(resultFactory(parameters));
        }
    }
}
