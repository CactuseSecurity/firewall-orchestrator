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
    public async Task ReferencedGroupCreate_UsesPersistedSnapshotsAndIgnoresSiblingTasks()
    {
        WfReqElement fantasyMember = Leaf(ElemFieldType.source, "10.1.0.2", "10.1.0.2");
        fantasyMember.Name = "fantasy";
        fantasyMember.ObjectTaskEntityId = -7;
        WfReqTask referenced = GroupCreate(2, 2, "shared",
            Items(Leaf(ElemFieldType.source, "10.1.0.1", "10.1.0.1"), fantasyMember));
        WfReqTask siblingObject = ObjectCreate(1, 1, "fantasy", "10.9.9.9");
        WfReqTask siblingGroup = GroupCreate(4, 4, "shared",
            Items(Leaf(ElemFieldType.source, "10.8.8.8", "10.8.8.8")));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            GroupName = "shared",
            ObjectTaskId = referenced.Id
        };
        WfReqTask access = Access(
            Items(source),
            Items(Leaf(ElemFieldType.destination, "203.0.113.5", "203.0.113.5")), 3, 3);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult());

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(
                access,
                Items(siblingGroup, access, referenced, siblingObject),
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceRanges.Select(range => range.Begin.ToString()),
                Is.EqualTo(new List<string> { "10.1.0.1", "10.1.0.2" }));
            Assert.That(groups.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task UnreferencedGroup_UsesCatalogDespiteIdentityMatchingTicketTask()
    {
        WfReqTask sibling = GroupCreate(1, 1, "catalog",
            Items(Leaf(ElemFieldType.source, "10.9.9.9", "10.9.9.9")));
        WfReqTask access = Access(
            Items(new WfReqElement { Field = "source", FlowNetworkGroupId = 7, GroupName = "catalog" }),
            Items(Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")), 2, 2);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = [CatalogGroup(7, "catalog")]
        });

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, Items(access, sibling), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.1"));
        Assert.That(groups.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task BrokenExplicitGroupReference_DoesNotFallBackToCatalog()
    {
        WfReqTask access = Access(
            Items(new WfReqElement
            {
                Field = "source",
                FlowNetworkGroupId = 7,
                GroupName = "catalog",
                ObjectTaskId = 999
            }),
            Items(Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")), 2, 2);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = [CatalogGroup(7, "catalog")]
        });

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver(groups).ResolveAsync(access, Items(access), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
        Assert.That(groups.CallCount, Is.Zero);
    }

    [Test]
    public async Task RelevantLaterObjectChange_IsProtocolOrderInvalid()
    {
        WfReqElement source = Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1");
        source.FlowNetworkObjectId = 9;
        WfReqTask access = Access([source], [Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")], 1, 1);
        WfReqTask later = ObjectModify(2, 2, 9, "shared", "10.0.0.1", "10.0.0.3");
        source.ObjectTaskId = later.Id;

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
        WfReqTask modify = ObjectModify(1, 1, 9, "shared", "10.0.0.1", "10.0.0.9");
        source.ObjectTaskId = modify.Id;
        WfReqTask access = Access([source], [Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")], 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, [access, modify], CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task ReferencedObjectCreate_ReplacesPersistedAccessSnapshot()
    {
        WfReqTask create = ObjectCreate(1, 1, "shared", "10.0.0.8");
        WfReqElement source = Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1");
        source.ObjectTaskId = create.Id;
        source.FlowNetworkObjectId = 9;
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, create), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [Test]
    public async Task ReferencedObjectCreate_UsesExactTaskForNamedAddresslessElement()
    {
        WfReqTask create = ObjectCreate(1, 1, "shared", "10.0.0.8");
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            Name = "stale-group-like-name",
            ObjectTaskId = create.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, create), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [Test]
    public async Task ReferencedGroupCreate_AddressMemberReferenceUsesOnlySelectedMember()
    {
        WfReqTask group = GroupCreate(1, 1, "shared",
            Items(Member(1, "10.0.0.8", RequestAction.create),
                Member(2, "10.0.0.9", RequestAction.create)));
        WfReqElement source = Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8");
        source.FlowNetworkObjectId = 1;
        source.Name = "1";
        source.ObjectTaskId = group.Id;
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, group), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [Test]
    public async Task ReferencedGroupCreate_IdOnlyReferenceExpandsWholeGroup()
    {
        WfReqTask group = GroupCreate(1, 1, "shared",
            Items(Member(1, "10.0.0.8", RequestAction.create),
                Member(2, "10.0.0.9", RequestAction.create)));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = group.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, group), CancellationToken.None));

        Assert.That(input.SourceRanges.Select(range => range.Begin.ToString()),
            Is.EqualTo(new List<string> { "10.0.0.8", "10.0.0.9" }));
    }

    [Test]
    public async Task ReferencedGroupCreate_AmbiguousMemberReferenceFailsClosed()
    {
        WfReqTask group = GroupCreate(1, 1, "shared",
            Items(Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8"),
                Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8"),
                Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8")));
        WfReqElement source = Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8");
        source.ObjectTaskId = group.Id;
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, Items(access, group), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
    }

    [Test]
    public async Task ReferencedGroupCreate_NestedCatalogGroupExpandsMembers()
    {
        WfReqElement nested = new()
        {
            Field = ElemFieldType.source.ToString(),
            FlowNetworkGroupId = 7,
            GroupName = "catalog",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask outer = GroupCreate(1, 1, "outer", Items(nested));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = outer.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = Items(CatalogGroup(7, "catalog"))
        });

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, Items(access, outer), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.1"));
    }

    [Test]
    public async Task ReferencedGroupCreate_SelectedNestedCatalogGroupExcludesSiblings()
    {
        WfReqElement nested = new()
        {
            Field = ElemFieldType.source.ToString(),
            FlowNetworkGroupId = 7,
            GroupName = "catalog",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask outer = GroupCreate(1, 1, "outer",
            Items(nested, Member(2, "10.0.0.9", RequestAction.create)));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = outer.Id,
            FlowNetworkGroupId = 7,
            GroupName = "catalog"
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = Items(CatalogGroup(7, "catalog"))
        });

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, Items(access, outer), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.1"));
    }

    [Test]
    public async Task ReferencedGroupCreate_NestedTicketGroupExpandsMembers()
    {
        WfReqTask inner = GroupCreate(1, 1, "inner",
            Items(Member(1, "10.0.0.8", RequestAction.create)));
        WfReqElement nested = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = inner.Id,
            GroupName = "inner",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask outer = GroupCreate(2, 2, "outer", Items(nested));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = outer.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 3, 3);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, outer, inner), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [Test]
    public async Task ReferencedGroupCreate_CyclicTicketGroupsFailClosed()
    {
        WfReqElement referencesSecond = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = 2,
            GroupName = "second",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqElement referencesFirst = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = 1,
            GroupName = "first",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask first = GroupCreate(1, 1, "first", Items(referencesSecond));
        WfReqTask second = GroupCreate(2, 2, "second", Items(referencesFirst));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = first.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 3, 3);

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, Items(access, first, second), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
    }

    [Test]
    public async Task ReferencedGroupCreate_ExcessiveTicketGroupDepthFailsClosed()
    {
        List<WfReqTask> groups = [];
        for (int id = 1; id <= 101; id++)
        {
            WfReqElement member = id == 101
                ? Member(id, "10.0.0.8", RequestAction.create)
                : new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    ObjectTaskId = id + 1,
                    GroupName = $"group-{id + 1}",
                    RequestAction = RequestAction.create.ToString()
                };
            groups.Add(GroupCreate(id, id, $"group-{id}", Items(member)));
        }
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = groups[0].Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 1000, 102);
        List<WfReqTask> ticketTasks = [access, .. groups];

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, ticketTasks, CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
    }

    [Test]
    public async Task ReferencedGroupCreate_AddressSnapshotIgnoresNestedObjectTaskReference()
    {
        WfReqElement snapshot = Member(1, "10.0.0.8", RequestAction.create);
        snapshot.ObjectTaskId = 999;
        WfReqTask group = GroupCreate(1, 1, "shared", Items(snapshot));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = group.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, group), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [Test]
    public async Task ReferencedGroupCreate_SelectedLeafIgnoresBrokenNestedSibling()
    {
        WfReqElement selected = Member(1, "10.0.0.8", RequestAction.create);
        selected.GroupName = "outer";
        WfReqElement brokenNested = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = 999,
            GroupName = "broken",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask outer = GroupCreate(1, 1, "outer", Items(selected, brokenNested));
        WfReqElement source = new(selected)
        {
            ObjectTaskId = outer.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, outer), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [Test]
    public async Task ReferencedGroupCreate_ParentGroupNameDoesNotOverrideExactMemberId()
    {
        WfReqElement selected = Member(1, "10.0.0.8", RequestAction.create);
        selected.GroupName = "outer";
        WfReqElement sibling = Member(2, "10.0.0.9", RequestAction.create);
        sibling.GroupName = "outer";
        WfReqTask outer = GroupCreate(1, 1, "outer", Items(selected, sibling));
        WfReqElement source = new(selected)
        {
            ObjectTaskId = outer.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, outer), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [Test]
    public async Task ReferencedGroupCreate_AddresslessObjectCannotBecomeLoadedCatalogGroup()
    {
        WfReqElement addresslessObject = new()
        {
            Field = ElemFieldType.source.ToString(),
            FlowNetworkObjectId = 9,
            Name = "catalog",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqElement nestedGroup = new()
        {
            Field = ElemFieldType.source.ToString(),
            FlowNetworkGroupId = 7,
            GroupName = "catalog",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask outer = GroupCreate(1, 1, "outer", Items(addresslessObject, nestedGroup));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = outer.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = Items(CatalogGroup(7, "catalog"))
        });

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver(groups).ResolveAsync(access, Items(access, outer), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
    }

    [Test]
    public async Task ReferencedGroupModify_NameOnlyReferenceExpandsWholeGroup()
    {
        WfReqTask modify = GroupModify(1, Member(1, "10.0.0.9", RequestAction.modify));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = modify.Id,
            GroupName = "catalog"
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);
        FlowNetworkGroupResolution catalog = CatalogGroup(7, "catalog");
        catalog.Members[0].Id = 1;
        catalog.Members[0].Name = "1";
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = Items(catalog)
        });

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, Items(access, modify), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task ReferencedGroupCreate_RemovedNestedGroupIsNotLoaded()
    {
        WfReqElement nestedCreate = new()
        {
            Field = ElemFieldType.source.ToString(),
            FlowNetworkGroupId = 7,
            GroupName = "catalog",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqElement nestedDelete = new(nestedCreate)
        {
            RequestAction = RequestAction.delete.ToString()
        };
        WfReqTask outer = GroupCreate(1, 1, "outer",
            Items(nestedCreate, nestedDelete, Member(1, "10.0.0.8", RequestAction.create)));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = outer.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult())
        {
            Exception = new InvalidOperationException("removed dependency was queried")
        };

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, Items(access, outer), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
        Assert.That(groups.CallCount, Is.Zero);
    }

    [Test]
    public async Task ReferencedGroupCreate_SharedSubgroupGraphIsDeduplicated()
    {
        List<WfReqTask> groups =
        [
            GroupCreate(1, 1, "group-1",
                Items(Member(1, "10.0.0.8", RequestAction.create)))
        ];
        for (int id = 2; id <= 22; id++)
        {
            WfReqElement firstReference = new()
            {
                Field = ElemFieldType.source.ToString(),
                ObjectTaskId = id - 1,
                GroupName = $"group-{id - 1}",
                RequestAction = RequestAction.create.ToString()
            };
            WfReqElement secondReference = new(firstReference);
            groups.Add(GroupCreate(id, id, $"group-{id}",
                Items(firstReference, secondReference)));
        }
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = groups[^1].Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 1000, 23);
        List<WfReqTask> ticketTasks = [access, .. groups];

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, ticketTasks, CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [Test]
    public async Task ReferencedGroupCreate_SharedSubgroupAcrossParentsIsDeduplicated()
    {
        List<WfReqTask> groups =
        [
            GroupCreate(1, 1, "shared-1",
                Items(Member(1, "10.0.0.8", RequestAction.create)))
        ];
        long sharedTaskId = 1;
        int nextId = 2;
        for (int level = 1; level <= 18; level++)
        {
            WfReqElement leftMember = NestedGroupReference(sharedTaskId, $"shared-{level}");
            WfReqTask left = GroupCreate(nextId, nextId, $"left-{level}", Items(leftMember));
            nextId++;
            WfReqElement rightMember = NestedGroupReference(sharedTaskId, $"shared-{level}");
            WfReqTask right = GroupCreate(nextId, nextId, $"right-{level}", Items(rightMember));
            nextId++;
            WfReqElement leftReference = NestedGroupReference(left.Id, $"left-{level}");
            WfReqElement rightReference = NestedGroupReference(right.Id, $"right-{level}");
            WfReqTask merged = GroupCreate(nextId, nextId, $"shared-{level + 1}",
                Items(leftReference, rightReference));
            nextId++;
            groups.AddRange(Items(left, right, merged));
            sharedTaskId = merged.Id;
        }
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = sharedTaskId
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 1000, nextId);
        List<WfReqTask> ticketTasks = [access, .. groups];

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, ticketTasks, CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [Test]
    public async Task ReferencedGroupCreate_DuplicateSnapshotsExpandDistinctRange()
    {
        WfReqTask group = GroupCreate(1, 1, "shared",
            Items(Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8"),
                Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8"),
                Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8")));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = group.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, group), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [Test]
    public async Task CatalogGroup_IdAndNestedNameSelectorsShareOneSnapshot()
    {
        WfReqElement nestedCatalog = new()
        {
            Field = ElemFieldType.source.ToString(),
            GroupName = "catalog",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask outer = GroupCreate(1, 1, "outer", Items(nestedCatalog));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            FlowNetworkGroupId = 7,
            GroupName = "catalog"
        };
        WfReqElement destination = new()
        {
            Field = ElemFieldType.destination.ToString(),
            ObjectTaskId = outer.Id
        };
        WfReqTask access = Access(Items(source), Items(destination), 2, 2);
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = Items(CatalogGroup(7, "catalog"))
        });

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, Items(access, outer), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.1"));
            Assert.That(input.DestinationRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.1"));
            Assert.That(groups.CallCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task CatalogGroup_MixedSelectorsPreserveDuplicateNameResult()
    {
        WfReqElement nestedCatalog = new()
        {
            Field = ElemFieldType.source.ToString(),
            GroupName = "catalog",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask outer = GroupCreate(1, 1, "outer", Items(nestedCatalog));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            FlowNetworkGroupId = 7,
            GroupName = "catalog"
        };
        WfReqElement destination = new()
        {
            Field = ElemFieldType.destination.ToString(),
            ObjectTaskId = outer.Id
        };
        WfReqTask access = Access(Items(source), Items(destination), 2, 2);
        FakeFlowGroupResolver groups = new(parameters => new FlowGroupResolutionResult
        {
            NetworkGroups = parameters.NetworkGroupIds.Count > 0
                ? Items(CatalogGroup(7, "catalog"))
                : Items(CatalogGroup(7, "catalog"), CatalogGroup(7, "catalog"))
        });

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver(groups).ResolveAsync(access, Items(access, outer), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupAmbiguous));
    }

    [Test]
    public async Task ReferencedGroupCreate_MemberSelectionsDoNotPoisonWholeGroupCache()
    {
        WfReqElement first = Member(1, "10.0.0.8", RequestAction.create);
        first.GroupName = "outer";
        WfReqElement second = Member(2, "10.0.0.9", RequestAction.create);
        second.GroupName = "outer";
        WfReqTask group = GroupCreate(1, 1, "outer", Items(first, second));
        WfReqElement firstReference = new(first)
        {
            ObjectTaskId = group.Id
        };
        WfReqElement secondReference = new(second)
        {
            ObjectTaskId = group.Id
        };
        WfReqElement wholeGroupReference = new()
        {
            Field = ElemFieldType.destination.ToString(),
            ObjectTaskId = group.Id
        };
        WfReqTask access = Access(Items(firstReference, secondReference),
            Items(wholeGroupReference), 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, group), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceRanges.Select(range => range.Begin.ToString()),
                Is.EqualTo(new List<string> { "10.0.0.8", "10.0.0.9" }));
            Assert.That(input.DestinationRanges.Select(range => range.Begin.ToString()),
                Is.EqualTo(new List<string> { "10.0.0.8", "10.0.0.9" }));
        });
    }

    [Test]
    public async Task ReferencedGroupCreate_ShallowCacheCannotBypassDeepPathLimit()
    {
        WfReqElement leafReference = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = 2,
            GroupName = "leaf",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask shared = GroupCreate(1, 1, "shared", Items(leafReference));
        WfReqTask leaf = GroupCreate(2, 2, "leaf",
            Items(Member(1, "10.0.0.8", RequestAction.create)));
        List<WfReqTask> groups = Items(shared, leaf);
        long nestedTaskId = shared.Id;
        for (int id = 3; id <= 101; id++)
        {
            WfReqElement nested = new()
            {
                Field = ElemFieldType.source.ToString(),
                ObjectTaskId = nestedTaskId,
                GroupName = nestedTaskId == shared.Id ? "shared" : $"wrapper-{id - 1}",
                RequestAction = RequestAction.create.ToString()
            };
            groups.Add(GroupCreate(id, id, $"wrapper-{id}", Items(nested)));
            nestedTaskId = id;
        }
        WfReqElement shallowReference = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = shared.Id,
            GroupName = "shared"
        };
        WfReqElement deepReference = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = nestedTaskId,
            GroupName = "wrapper-101"
        };
        WfReqTask access = Access(Items(shallowReference, deepReference),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 1000, 102);
        List<WfReqTask> ticketTasks = [access, .. groups];

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, ticketTasks, CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
    }

    [Test]
    public async Task ReferencedGroupModify_UnchangedStaleTaskReferenceIsNotFollowed()
    {
        WfReqElement unchanged = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = 999,
            RequestAction = RequestAction.unchanged.ToString()
        };
        WfReqTask modify = GroupModify(1, unchanged);
        modify.Elements.Add(Member(1, "10.0.0.9", RequestAction.modify));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = modify.Id,
            GroupName = "catalog"
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);
        FlowNetworkGroupResolution catalog = CatalogGroup(7, "catalog");
        catalog.Members[0].Id = 1;
        catalog.Members[0].Name = "1";
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = Items(catalog)
        });

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, Items(access, modify), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task CatalogGroup_DirectNameOnlyReferenceStillLoadsGroup()
    {
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            Name = "catalog"
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")));
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = Items(CatalogGroup(7, "catalog"))
        });

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, Items(access), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.1"));
        Assert.That(groups.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ReferencedGroupCreate_TaskOnlyNestedMemberCanBeRemoved()
    {
        WfReqTask inner = GroupCreate(1, 1, "inner",
            Items(Member(1, "10.0.0.9", RequestAction.create)));
        WfReqElement nestedCreate = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = inner.Id,
            RequestAction = RequestAction.create.ToString()
        };
        WfReqElement nestedDelete = new(nestedCreate)
        {
            RequestAction = RequestAction.delete.ToString()
        };
        WfReqTask outer = GroupCreate(2, 2, "outer",
            Items(nestedCreate, nestedDelete, Member(2, "10.0.0.8", RequestAction.create)));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = outer.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 3, 3);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, outer, inner), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [Test]
    public async Task ReferencedGroupCreate_ConcreteDeleteIgnoresParentGroupName()
    {
        WfReqElement first = Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8");
        first.GroupName = "outer";
        WfReqElement second = Leaf(ElemFieldType.source, "10.0.0.9", "10.0.0.9");
        second.GroupName = "outer";
        WfReqElement deleteFirst = new(first)
        {
            RequestAction = RequestAction.delete.ToString()
        };
        WfReqTask group = GroupCreate(1, 1, "outer", Items(first, second, deleteFirst));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = group.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, group), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task ReferencedGroupCreate_NameMemberAndWholeGroupCachesStaySeparate()
    {
        WfReqElement first = Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8");
        first.Name = "leaf-a";
        first.GroupName = "outer";
        WfReqElement second = Leaf(ElemFieldType.source, "10.0.0.9", "10.0.0.9");
        second.Name = "leaf-b";
        second.GroupName = "outer";
        WfReqTask group = GroupCreate(1, 1, "outer", Items(first, second));
        WfReqElement memberSource = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = group.Id,
            GroupName = "outer",
            Name = "leaf-a"
        };
        WfReqElement wholeDestination = new()
        {
            Field = ElemFieldType.destination.ToString(),
            ObjectTaskId = group.Id
        };
        WfReqTask memberFirst = Access(Items(memberSource), Items(wholeDestination), 2, 2);

        CreationRuleNetworkInput memberFirstInput = AssertSuccess(
            await Resolver().ResolveAsync(memberFirst, Items(memberFirst, group), CancellationToken.None));

        WfReqElement wholeSource = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = group.Id
        };
        WfReqElement memberDestination = new(memberSource)
        {
            Field = ElemFieldType.destination.ToString()
        };
        WfReqTask wholeFirst = Access(Items(wholeSource), Items(memberDestination), 3, 3);
        CreationRuleNetworkInput wholeFirstInput = AssertSuccess(
            await Resolver().ResolveAsync(wholeFirst, Items(wholeFirst, group), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(memberFirstInput.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
            Assert.That(memberFirstInput.DestinationRanges, Has.Count.EqualTo(2));
            Assert.That(wholeFirstInput.SourceRanges, Has.Count.EqualTo(2));
            Assert.That(wholeFirstInput.DestinationRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
        });
    }

    [Test]
    public async Task ReferencedGroupCreate_CachedDescendantCannotConcealAncestorCycle()
    {
        WfReqElement leaf = Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8");
        leaf.Name = "leaf";
        WfReqElement wholeG = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = 2,
            GroupName = "G",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask h = GroupCreate(1, 1, "H", Items(leaf, wholeG));
        WfReqElement selectedLeafFromH = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = h.Id,
            GroupName = "H",
            Name = "leaf",
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask g = GroupCreate(2, 2, "G", Items(selectedLeafFromH));
        WfReqElement primeG = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = g.Id,
            GroupName = "G"
        };
        WfReqElement resolveH = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = h.Id,
            GroupName = "H"
        };
        WfReqTask access = Access(Items(primeG, resolveH),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 3, 3);

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, Items(access, g, h), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
    }

    [Test]
    public async Task ReferencedGroupModify_AddressSnapshotMatchesBeforeObjectTaskId()
    {
        WfReqElement change = Member(1, "10.0.0.9", RequestAction.modify);
        change.ObjectTaskId = 999;
        WfReqTask modify = GroupModify(1, change);
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = modify.Id,
            GroupName = "catalog"
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);
        FlowNetworkGroupResolution catalog = CatalogGroup(7, "catalog");
        catalog.Members[0].Id = 1;
        catalog.Members[0].Name = "1";
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = Items(catalog)
        });

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, Items(access, modify), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task ReferencedGroupModify_AmbiguousEffectiveNameDoesNotTraverseNestedTask()
    {
        WfReqTask later = GroupCreate(3, 3, "later",
            Items(Member(1, "10.0.0.9", RequestAction.create)));
        WfReqElement nested = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = later.Id,
            GroupName = "later",
            Name = "shared",
            RequestAction = RequestAction.addAfterCreation.ToString()
        };
        WfReqTask modify = GroupModify(1, nested);
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = modify.Id,
            GroupName = "catalog",
            Name = "shared"
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);
        FlowNetworkGroupResolution catalog = CatalogGroup(7, "catalog");
        catalog.Members[0].Id = 1;
        catalog.Members[0].Name = "shared";
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = Items(catalog)
        });

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver(groups).ResolveAsync(access, Items(access, modify, later), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
    }

    [Test]
    public async Task ReferencedGroupModify_ExactGroupIdOutranksStaleName()
    {
        WfReqTask modify = GroupModify(1, Member(1, "10.0.0.9", RequestAction.modify));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = modify.Id,
            FlowNetworkGroupId = 7,
            GroupName = "catalog",
            Name = "stale-label"
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);
        FlowNetworkGroupResolution catalog = CatalogGroup(7, "catalog");
        catalog.Members[0].Id = 1;
        catalog.Members[0].Name = "1";
        FakeFlowGroupResolver groups = new(new FlowGroupResolutionResult
        {
            NetworkGroups = Items(catalog)
        });

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver(groups).ResolveAsync(access, Items(access, modify), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task ReferencedGroupCreate_SameTaskMemberDeleteUsesMemberName()
    {
        WfReqElement innerA = Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8");
        innerA.Name = "a";
        WfReqElement innerB = Leaf(ElemFieldType.source, "10.0.0.9", "10.0.0.9");
        innerB.Name = "b";
        WfReqTask inner = GroupCreate(1, 1, "inner", Items(innerA, innerB));
        WfReqElement selectA = NestedGroupReference(inner.Id, "inner");
        selectA.Name = "a";
        WfReqElement selectB = NestedGroupReference(inner.Id, "inner");
        selectB.Name = "b";
        WfReqElement deleteA = new(selectA)
        {
            RequestAction = RequestAction.delete.ToString()
        };
        WfReqTask outer = GroupCreate(2, 2, "outer", Items(selectA, selectB, deleteA));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = outer.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 3, 3);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, outer, inner), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task ReferencedGroupCreate_IdlessModifyCanChangeAddressByName()
    {
        WfReqElement original = Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8");
        original.Name = "shared";
        WfReqElement modified = Leaf(ElemFieldType.source, "10.0.0.9", "10.0.0.9");
        modified.Name = "shared";
        modified.RequestAction = RequestAction.modify.ToString();
        WfReqTask group = GroupCreate(1, 1, "outer", Items(original, modified));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = group.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, group), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task ReferencedGroupCreate_ModifyWithMismatchedStableIdDoesNotFallbackToName()
    {
        WfReqElement original = Member(1, "10.0.0.8", RequestAction.create);
        original.Name = "shared";
        WfReqElement modified = Member(2, "10.0.0.9", RequestAction.modify);
        modified.Name = "shared";
        WfReqTask group = GroupCreate(1, 1, "outer", Items(original, modified));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = group.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, Items(access, group), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
    }

    [Test]
    public async Task ReferencedGroupCreate_MissingTaskOnlyNestedMemberIsGroupUnresolved()
    {
        WfReqElement missing = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = 999,
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask group = GroupCreate(1, 1, "outer", Items(missing));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            ObjectTaskId = group.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, Items(access, group), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
    }

    [Test]
    public async Task UnreferencedObjectTask_CannotReplaceDirectSnapshotByIdentity()
    {
        WfReqTask modify = ObjectModify(1, 1, 9, "shared", "10.0.0.1", "10.0.0.9");
        WfReqElement source = Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1");
        source.FlowNetworkObjectId = 9;
        source.Name = "shared";
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 3, 3);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access, Items(access, modify), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.1"));
    }

    [Test]
    public async Task ExactObjectReference_IgnoresIdentityMatchingAndMalformedSiblings()
    {
        WfReqTask referenced = ObjectCreate(1, 1, "shared", "10.0.0.8");
        WfReqTask matching = ObjectCreate(2, 2, "shared", "10.0.0.9");
        matching.Elements[0].FlowNetworkObjectId = 9;
        WfReqTask malformed = ObjectCreate(3, 99, "shared", "10.0.0.10");
        malformed.ManagementId = 999;
        malformed.Elements = null!;
        WfReqElement source = Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1");
        source.ObjectTaskId = referenced.Id;
        source.FlowNetworkObjectId = 9;
        source.Name = "shared";
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 10, 10);

        CreationRuleNetworkInput input = AssertSuccess(
            await Resolver().ResolveAsync(access,
                Items(malformed, access, matching, referenced), CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.8"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(999)]
    public async Task InvalidExplicitObjectReference_FailsWithoutSnapshotFallback(long objectTaskId)
    {
        WfReqElement source = Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1");
        source.ObjectTaskId = objectTaskId;
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, Items(access), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved));
    }

    [Test]
    public async Task CrossTicketObjectReference_FailsWithoutIdentityFallback()
    {
        WfReqTask referenced = ObjectCreate(1, 1, "shared", "10.0.0.8");
        referenced.TicketId = 101;
        WfReqElement source = Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1");
        source.ObjectTaskId = referenced.Id;
        source.Name = "shared";
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 2, 2);

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, Items(access, referenced), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved));
    }

    [Test]
    public async Task WrongTypeAndInvalidObjectStructures_AreObjectUnresolved()
    {
        WfReqTask wrongType = ObjectCreate(1, 1, "shared", "10.0.0.8");
        wrongType.TaskType = WfTaskType.access.ToString();
        WfReqTask invalidCreate = ObjectCreate(2, 1, "shared", "10.0.0.8");
        invalidCreate.Elements.Add(Leaf(ElemFieldType.source, "10.0.0.9", "10.0.0.9"));
        WfReqTask invalidModify = ObjectModify(3, 1, 9, "shared", "10.0.0.1", "10.0.0.8");
        invalidModify.Elements[1].NetworkId = 10;
        WfReqTask service = ObjectCreate(4, 1, "service", "10.0.0.8");
        service.Elements[0].Field = ElemFieldType.service.ToString();
        WfReqTask time = ObjectCreate(5, 1, "time", "10.0.0.8");
        time.Elements[0].Field = ElemFieldType.time.ToString();

        CreationRuleNetworkResolutionFailureCategory wrongTypeFailure = await ResolveObjectFailure(wrongType);
        CreationRuleNetworkResolutionFailureCategory invalidCreateFailure = await ResolveObjectFailure(invalidCreate);
        CreationRuleNetworkResolutionFailureCategory invalidModifyFailure = await ResolveObjectFailure(invalidModify);
        CreationRuleNetworkResolutionFailureCategory serviceFailure = await ResolveObjectFailure(service);
        CreationRuleNetworkResolutionFailureCategory timeFailure = await ResolveObjectFailure(time);

        Assert.Multiple(() =>
        {
            Assert.That(wrongTypeFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved));
            Assert.That(invalidCreateFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved));
            Assert.That(invalidModifyFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved));
            Assert.That(serviceFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved));
            Assert.That(timeFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved));
        });
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
        WfReqTask unrelatedDelete = new()
        {
            Id = 3,
            TicketId = 100,
            TaskNumber = 3,
            ManagementId = 999,
            TaskType = WfTaskType.group_delete.ToString(),
            Elements = []
        };
        unrelatedDelete.SetAddInfo(AdditionalInfoKeys.GroupId, "7");
        unrelatedDelete.SetAddInfo(AdditionalInfoKeys.GrpName, "catalog");
        WfReqTask access = Access(
            Items(new WfReqElement
            {
                Field = "source",
                FlowNetworkGroupId = 7,
                GroupName = "catalog",
                ObjectTaskId = modify.Id
            }),
            Items(Leaf(ElemFieldType.destination, "203.0.113.1", "203.0.113.1")), 2, 2);
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
            await Resolver(groups).ResolveAsync(
                access,
                Items(unrelatedDelete, access, modify),
                CancellationToken.None));

        Assert.That(input.SourceRanges.Single().Begin.ToString(), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task ReferencedGroupWithMissingAddress_FailsDespiteMatchingObjectSibling()
    {
        WfReqElement missing = new()
        {
            Field = ElemFieldType.source.ToString(),
            Name = "fantasy",
            ObjectTaskEntityId = -7,
            RequestAction = RequestAction.create.ToString()
        };
        WfReqTask group = GroupCreate(2, 2, "shared", Items(missing));
        WfReqTask sibling = ObjectCreate(1, 1, "fantasy", "10.9.9.9");

        CreationRuleNetworkResolutionFailureCategory failure = await ResolveGroupFailure(
            group,
            Items(sibling));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
    }

    [Test]
    public async Task EmptyDeletedCrossTicketAndUnsupportedGroupReferences_FailClosed()
    {
        WfReqTask empty = GroupCreate(1, 1, "empty", []);
        WfReqTask deleted = new()
        {
            Id = 2,
            TicketId = 100,
            TaskNumber = 1,
            ManagementId = 10,
            TaskType = WfTaskType.group_delete.ToString(),
            Elements = []
        };
        WfReqTask crossTicket = GroupCreate(3, 1, "cross",
            Items(Leaf(ElemFieldType.source, "10.0.0.3", "10.0.0.3")));
        crossTicket.TicketId = 101;
        WfReqTask wrongType = GroupCreate(4, 1, "wrong",
            Items(Leaf(ElemFieldType.source, "10.0.0.4", "10.0.0.4")));
        wrongType.TaskType = WfTaskType.access.ToString();

        CreationRuleNetworkResolutionFailureCategory emptyFailure = await ResolveGroupFailure(empty);
        CreationRuleNetworkResolutionFailureCategory deletedFailure = await ResolveGroupFailure(deleted);
        CreationRuleNetworkResolutionFailureCategory crossTicketFailure = await ResolveGroupFailure(crossTicket);
        CreationRuleNetworkResolutionFailureCategory wrongTypeFailure = await ResolveGroupFailure(wrongType);

        Assert.Multiple(() =>
        {
            Assert.That(emptyFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
            Assert.That(deletedFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
            Assert.That(crossTicketFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
            Assert.That(wrongTypeFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
        });
    }

    [Test]
    public async Task LaterReferencedGroup_IsProtocolOrderInvalid()
    {
        WfReqTask later = GroupCreate(2, 2, "later",
            Items(Leaf(ElemFieldType.source, "10.0.0.8", "10.0.0.8")));
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            GroupName = "later",
            ObjectTaskId = later.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 1, 1);

        CreationRuleNetworkResolutionFailureCategory failure = AssertFailure(
            await Resolver().ResolveAsync(access, Items(access, later), CancellationToken.None));

        Assert.That(failure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.ProtocolOrderInvalid));
    }

    [Test]
    public async Task GroupModify_InvalidOrEmptyResultIsGroupUnresolved()
    {
        WfReqTask invalid = GroupModify(1, Member(1, "10.0.0.9", RequestAction.modify));
        invalid.Elements[0].IpString = null;
        invalid.Elements[0].IpEnd = null;
        WfReqTask empty = GroupModify(2, Member(1, "10.0.0.1", RequestAction.delete));
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
                            Name = "one",
                            IpStart = "10.0.0.1",
                            IpEnd = "10.0.0.1"
                        }
                    ]
                }
            ]
        });

        CreationRuleNetworkResolutionFailureCategory invalidFailure = await ResolveGroupFailure(
            invalid, [], groups);
        CreationRuleNetworkResolutionFailureCategory emptyFailure = await ResolveGroupFailure(
            empty, [], groups);

        Assert.Multiple(() =>
        {
            Assert.That(invalidFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
            Assert.That(emptyFailure, Is.EqualTo(CreationRuleNetworkResolutionFailureCategory.GroupUnresolved));
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

    private static WfReqTask ObjectCreate(long id, int taskNumber, string name, string ip) => new()
    {
        Id = id,
        TicketId = 100,
        TaskNumber = taskNumber,
        ManagementId = 10,
        TaskType = WfTaskType.object_create.ToString(),
        Elements =
        [
            new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                Name = name,
                IpString = ip,
                IpEnd = ip,
                RequestAction = RequestAction.create.ToString()
            }
        ]
    };

    private static WfReqTask ObjectModify(
        long id,
        int taskNumber,
        long networkId,
        string name,
        string originalIp,
        string requestedIp) => new()
        {
            Id = id,
            TicketId = 100,
            TaskNumber = taskNumber,
            ManagementId = 10,
            TaskType = WfTaskType.object_modify.ToString(),
            Elements =
            [
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    NetworkId = networkId,
                    Name = name,
                    IpString = originalIp,
                    IpEnd = originalIp,
                    RequestAction = RequestAction.unchanged.ToString()
                },
                new WfReqElement
                {
                    Field = ElemFieldType.source.ToString(),
                    NetworkId = networkId,
                    Name = name,
                    IpString = requestedIp,
                    IpEnd = requestedIp,
                    RequestAction = RequestAction.modify.ToString()
                }
            ]
        };

    private static WfReqTask GroupModify(long id, WfReqElement change)
    {
        WfReqTask task = new()
        {
            Id = id,
            TicketId = 100,
            TaskNumber = 1,
            ManagementId = 10,
            TaskType = WfTaskType.group_modify.ToString(),
            Elements = [change]
        };
        task.SetAddInfo(AdditionalInfoKeys.GroupId, "7");
        task.SetAddInfo(AdditionalInfoKeys.GrpName, "catalog");
        return task;
    }

    private static async Task<CreationRuleNetworkResolutionFailureCategory> ResolveObjectFailure(
        WfReqTask referencedTask)
    {
        WfReqElement source = Leaf(ElemFieldType.source, "10.0.0.1", "10.0.0.1");
        source.ObjectTaskId = referencedTask.Id;
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 10, 10);
        return AssertFailure(await Resolver().ResolveAsync(
            access,
            Items(access, referencedTask),
            CancellationToken.None));
    }

    private static async Task<CreationRuleNetworkResolutionFailureCategory> ResolveGroupFailure(
        WfReqTask referencedTask,
        IEnumerable<WfReqTask>? siblings = null,
        IFlowGroupResolver? groups = null)
    {
        WfReqElement source = new()
        {
            Field = ElemFieldType.source.ToString(),
            FlowNetworkGroupId = 7,
            GroupName = "catalog",
            ObjectTaskId = referencedTask.Id
        };
        WfReqTask access = Access(Items(source),
            Items(Leaf(ElemFieldType.destination, "10.0.0.2", "10.0.0.2")), 10, 10);
        List<WfReqTask> ticketTasks = [access, referencedTask, .. siblings ?? []];
        return AssertFailure(await Resolver(groups).ResolveAsync(
            access,
            ticketTasks,
            CancellationToken.None));
    }

    /// <summary>Creates one explicitly list-targeted test collection.</summary>
    private static List<T> Items<T>(T first) => [first];

    /// <summary>Creates one explicitly list-targeted test collection.</summary>
    private static List<T> Items<T>(T first, T second) => [first, second];

    /// <summary>Creates one explicitly list-targeted test collection.</summary>
    private static List<T> Items<T>(T first, T second, T third) => [first, second, third];

    /// <summary>Creates one explicitly list-targeted test collection.</summary>
    private static List<T> Items<T>(T first, T second, T third, T fourth) =>
        [first, second, third, fourth];

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

    /// <summary>Creates a ticket-local nested group member reference.</summary>
    private static WfReqElement NestedGroupReference(long taskId, string groupName) => new()
    {
        Field = ElemFieldType.source.ToString(),
        ObjectTaskId = taskId,
        GroupName = groupName,
        RequestAction = RequestAction.create.ToString()
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
