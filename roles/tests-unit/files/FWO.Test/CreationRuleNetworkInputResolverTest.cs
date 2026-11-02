using FWO.Compliance.RulePositioning;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;
using CreationRuleNetworkResolutionResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Data.RulePositioning.CreationRuleNetworkInput,
    FWO.Compliance.RulePositioning.CreationRuleNetworkResolutionFailureCategory>;

namespace FWO.Test;

[TestFixture]
public class CreationRuleNetworkInputResolverTest
{
    private static readonly string[] kExpectedResolvedSourceAddresses = ["10.0.0.1", "10.0.0.2"];
    private static readonly long[] kExpectedRequestedGroupIds = [7];

    [Test]
    public async Task DirectRanges_AreNormalizedDeduplicatedAndImmutable()
    {
        WfReqTask access = Access(
            [Leaf(ElemFieldType.source, "192.0.2.1", "192.0.2.1"), Leaf(ElemFieldType.source, "192.0.2.1", "192.0.2.1")],
            [Leaf(ElemFieldType.destination, "2001:db8::/126", null)]);
        List<WfReqTask> tasks = [access];

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, tasks, CancellationToken.None));
        tasks.Clear();
        access.Elements.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceRanges, Has.Count.EqualTo(1));
            Assert.That(input.SourceRanges[0].Begin.ToString(), Is.EqualTo("192.0.2.1"));
            Assert.That(input.DestinationRanges[0].Begin.ToString(), Is.EqualTo("2001:db8::"));
            Assert.That(input.DestinationRanges[0].End.ToString(), Is.EqualTo("2001:db8::3"));
            Assert.That(input.SourceRanges, Is.Not.InstanceOf<List<NetTools.IPAddressRange>>());
        });
    }

    [Test]
    public async Task ValidCidr_TakesPrecedenceOverLegacyIpStrings()
    {
        WfReqElement source = Leaf(ElemFieldType.source, "not-an-ip", "also-not-an-ip");
        source.Cidr = new Cidr("10.10.10.0/30");
        WfReqTask access = Access(
            [source],
            [Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")]);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.10.10.0"));
            Assert.That(input.SourceRanges.Single().End.ToString(), Is.EqualTo("10.10.10.3"));
        });
    }

    [Test]
    public async Task LegacyIpString_WithDottedNetmaskRemainsSupported()
    {
        WfReqTask access = Access(
            [Leaf(ElemFieldType.source, "10.10.10.0/255.255.255.0", null)],
            [Leaf(ElemFieldType.destination, "203.0.113.1", null)]);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.10.10.0"));
            Assert.That(input.SourceRanges.Single().End.ToString(), Is.EqualTo("10.10.10.255"));
        });
    }

    [Test]
    public async Task CatalogMemberEndpointCidrs_AreTreatedAsExplicitEndpoints()
    {
        WfReqTask access = Access(
            [new WfReqElement { Field = "source", FlowNetworkGroupId = 7 }],
            [Leaf(ElemFieldType.destination, "203.0.113.1", null)]);
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
                        new FlowNetworkMemberResolution
                        {
                            Id = 1,
                            IpStart = "10.0.0.1/24",
                            IpEnd = "10.0.0.2/24"
                        }
                    ]
                }
            ]
        });

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.1"));
            Assert.That(input.SourceRanges.Single().End.ToString(), Is.EqualTo("10.0.0.2"));
        });
    }

    [TestCase("10.0.0.9", "10.0.0.1")]
    [TestCase("10.0.0.1", "2001:db8::1")]
    public async Task ExplicitEndpoints_RejectReversedOrMixedAddressFamilies(string start, string end)
    {
        WfReqTask access = Access(
            [Leaf(ElemFieldType.source, start, end)],
            [Leaf(ElemFieldType.destination, "203.0.113.1", null)]);

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, [access], CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.InvalidAddress));
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

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, [access], CancellationToken.None));

        Assert.That(failure, Is.EqualTo(category));
        Assert.That(failure.GetDiagnostic(), Does.Not.Contain(source ?? "ticket-secret"));
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

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, [access], CancellationToken.None));

        Assert.That(input.SourceRanges.Select(range => range.Begin.ToString()),
            Is.EqualTo(kExpectedResolvedSourceAddresses));
        Assert.That(groups.Parameters!.NetworkGroupIds, Is.EqualTo(kExpectedRequestedGroupIds));
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

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceRanges, Has.Count.EqualTo(101));
            Assert.That(groups.CallCount, Is.EqualTo(2));
            Assert.That(groups.AllParameters.SelectMany(item => item.NetworkGroupIds),
                Is.EquivalentTo(Enumerable.Range(1, 101).Select(id => (long)id)));
        });
    }

    [Test]
    public async Task CatalogGroups_MixedSelectorsAboveOneCallLimitAreLoadedSeparately()
    {
        List<WfReqElement> selectors =
        [
            .. Enumerable.Range(1, 60).Select(id => new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                FlowNetworkGroupId = id
            }),
            .. Enumerable.Range(1, 50).Select(id => new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                GroupName = $"named-{id}"
            })
        ];
        WfReqTask access = Access(selectors,
            [Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")]);
        FakeFlowGroupResolver groups = new(parameters => new FlowGroupResolutionResult
        {
            NetworkGroups =
            [
                .. parameters.NetworkGroupIds.Select(id => CatalogGroup(id, $"group-{id}")),
                .. parameters.NetworkGroupNames.Select((name, index) => CatalogGroup(1000 + index, name))
            ]
        });

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceRanges, Is.Not.Empty);
            Assert.That(groups.CallCount, Is.EqualTo(2));
            Assert.That(groups.AllParameters.Single(parameters => parameters.NetworkGroupIds.Count > 0)
                .NetworkGroupIds, Has.Count.EqualTo(60));
            Assert.That(groups.AllParameters.Single(parameters => parameters.NetworkGroupNames.Count > 0)
                .NetworkGroupNames, Has.Count.EqualTo(50));
        });
    }

    [TestCase(false, CreationRuleNetworkResolutionFailureCategory.GroupUnresolved)]
    [TestCase(true, CreationRuleNetworkResolutionFailureCategory.GroupAmbiguous)]
    public async Task CatalogGroups_InvalidResultInLaterChunkFailsClosed(
        bool duplicateLastGroup,
        CreationRuleNetworkResolutionFailureCategory expectedCategory)
    {
        WfReqTask access = Access(
            Enumerable.Range(1, 101).Select(id => new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                FlowNetworkGroupId = id
            }),
            [Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")]);
        FakeFlowGroupResolver groups = new(parameters =>
        {
            List<FlowNetworkGroupResolution> resolved = parameters.NetworkGroupIds
                .Where(id => id != 101 || duplicateLastGroup)
                .Select(id => CatalogGroup(id, $"group-{id}"))
                .ToList();
            if (duplicateLastGroup && parameters.NetworkGroupIds.Contains(101))
            {
                resolved.Add(CatalogGroup(101, "duplicate-101"));
            }
            return new FlowGroupResolutionResult { NetworkGroups = resolved };
        });

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver(groups).ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(failure, Is.EqualTo(expectedCategory));
            Assert.That(groups.CallCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void CatalogGroups_CancellationBetweenChunksStopsFurtherCalls()
    {
        WfReqTask access = Access(
            Enumerable.Range(1, 101).Select(id => new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                FlowNetworkGroupId = id
            }),
            [Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")]);
        using CancellationTokenSource cancellation = new();
        FakeFlowGroupResolver groups = new(parameters =>
        {
            cancellation.Cancel();
            return new FlowGroupResolutionResult
            {
                NetworkGroups = [.. parameters.NetworkGroupIds.Select(id => CatalogGroup(id, $"group-{id}"))]
            };
        });

        Assert.That(async () => await Resolver(groups).ResolveAsync(access, [access], cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(groups.CallCount, Is.EqualTo(1));
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

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, [access, outer, nested], CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.1.0.1"));
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

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, [later, access], CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.ProtocolOrderInvalid));
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

        CreationRuleNetworkResolutionFailureCategory unsupportedFailure = AssertFailure(
            await Resolver().ResolveAsync(unsupported, [unsupported], CancellationToken.None));
        CreationRuleNetworkResolutionFailureCategory actionFailure = AssertFailure(
            await Resolver().ResolveAsync(unknownAction, [unknownAction], CancellationToken.None));
        CreationRuleNetworkResolutionFailureCategory identityFailure = AssertFailure(
            await Resolver().ResolveAsync(invalidIdentity, [invalidIdentity], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(unsupportedFailure,
                Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.UnsupportedTask));
            Assert.That(actionFailure,
                Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.InvalidInput));
            Assert.That(identityFailure,
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

        CreationRuleNetworkResolutionFailureCategory queryFailure = AssertFailure(
            await Resolver(throwing).ResolveAsync(access, [access], CancellationToken.None));
        CreationRuleNetworkResolutionFailureCategory missingFailure = AssertFailure(
            await Resolver(missing).ResolveAsync(access, [access], CancellationToken.None));
        CreationRuleNetworkResolutionFailureCategory duplicateFailure = AssertFailure(
            await Resolver(duplicate).ResolveAsync(access, [access], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(queryFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupQueryFailed));
            Assert.That(queryFailure.GetDiagnostic(), Does.Not.Contain("secret"));
            Assert.That(missingFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
            Assert.That(duplicateFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupAmbiguous));
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

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, [access, modify], CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
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

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, [access, modify], CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
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

        CreationRuleNetworkResolutionFailureCategory deletedFailure = AssertFailure(
            await Resolver().ResolveAsync(deletedAccess, [deletedAccess, created, deleted], CancellationToken.None));
        CreationRuleNetworkResolutionFailureCategory cycleFailure = AssertFailure(
            await Resolver().ResolveAsync(cycleAccess, [cycleAccess, cycleA, cycleB], CancellationToken.None));
        CreationRuleNetworkResolutionFailureCategory laterFailure = AssertFailure(
            await Resolver().ResolveAsync(laterAccess, [laterAccess, later], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(deletedFailure,
                Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
            Assert.That(cycleFailure,
                Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
            Assert.That(laterFailure,
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

    private static FlowNetworkGroupResolution CatalogGroup(long id, string name) => new()
    {
        Id = id,
        Name = name,
        Members =
        [
            new FlowNetworkMemberResolution
            {
                Id = id,
                IpStart = "10.0.0.1",
                IpEnd = "10.0.0.1"
            }
        ]
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

    private static CreationRuleNetworkInput AssertSuccess(CreationRuleNetworkResolutionResult result)
    {
        Assert.That(result.TryGetValue(out CreationRuleNetworkInput? input), Is.True);
        return input!;
    }

    private static CreationRuleNetworkResolutionFailureCategory AssertFailure(
        CreationRuleNetworkResolutionResult result)
    {
        Assert.That(result.TryGetFailure(out CreationRuleNetworkResolutionFailureCategory failure), Is.True);
        return failure;
    }
}
