using FWO.Compliance.RulePositioning;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NetTools;
using NUnit.Framework;
using System.Net;

namespace FWO.Test;

[TestFixture]
internal class CreationRuleZoneResolverTest
{
    private const int kCriterionId = 7;
    private static readonly long[] kExpectedSingleGroupId = [11];
    private static readonly string[] kExpectedSingleGroupName = ["NAMED-GROUP"];
    private static readonly int[] kExpectedChunkSizes = [100, 1];

    [Test]
    public async Task ResolveAsync_ResolvesIndependentOrderedZoneSets()
    {
        TestFlowGroupResolver groupResolver = new();
        CreationRuleZoneResolver resolver = CreateResolver(groupResolver);
        WfReqTask task = CreateTask(
            Element(ElemFieldType.source, "10.0.0.20"),
            Element(ElemFieldType.source, "10.0.0.60"),
            Element(ElemFieldType.destination, "192.0.2.10"));
        List<ComplianceNetworkZone> zones =
        [
            Zone(30, "Source broad", "10.0.0.0", "10.0.0.255"),
            Zone(10, "Source narrow", "10.0.0.0", "10.0.0.31"),
            Zone(20, "Destination", "192.0.2.0", "192.0.2.255")
        ];

        CreationRuleZoneInput input = Success(await resolver.ResolveAsync(task, zones, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.CriterionId, Is.EqualTo(kCriterionId));
            Assert.That(input.SourceZones.Select(zone => zone.Id), Is.EqualTo(new int?[] { 10, 30 }));
            Assert.That(input.DestinationZones.Select(zone => zone.Id), Is.EqualTo(new int?[] { 20 }));
            Assert.That(input.SourceCoverage, Is.EqualTo(ZoneCoverage.Complete));
            Assert.That(input.DestinationCoverage, Is.EqualTo(ZoneCoverage.Complete));
            Assert.That(groupResolver.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task ResolveAsync_UsesActiveActionsAndIgnoresDeletedAndIrrelevantElements()
    {
        CreationRuleZoneResolver resolver = CreateResolver(new());
        WfReqTask task = CreateTask(
            Element(ElemFieldType.source, "10.0.0.1", RequestAction.create),
            Element(ElemFieldType.source, "10.0.0.2", RequestAction.unchanged),
            Element(ElemFieldType.source, "10.0.0.3", RequestAction.modify),
            Element(ElemFieldType.source, "10.0.0.4", RequestAction.addAfterCreation),
            Element(ElemFieldType.source, "203.0.113.1", RequestAction.delete),
            Element(ElemFieldType.destination, "192.0.2.1"),
            Element(ElemFieldType.service, "203.0.113.2"));

        CreationRuleZoneInput input = Success(await resolver.ResolveAsync(task,
            [Zone(1, "Source", "10.0.0.0", "10.0.0.255"), Zone(2, "Destination", "192.0.2.0", "192.0.2.255")],
            CancellationToken.None));

        Assert.That(input.SourceZones.Select(zone => zone.Id), Is.EqualTo(new int?[] { 1 }));
        Assert.That(input.DestinationZones.Select(zone => zone.Id), Is.EqualTo(new int?[] { 2 }));
    }

    [TestCase(null, null, CreationRuleZoneResolutionFailureCategory.InvalidInput)]
    [TestCase("generic", "create", CreationRuleZoneResolutionFailureCategory.UnsupportedTask)]
    [TestCase("access", "modify", CreationRuleZoneResolutionFailureCategory.UnsupportedTask)]
    public async Task ResolveAsync_RejectsMissingOrUnsupportedTask(
        string? taskType,
        string? requestAction,
        CreationRuleZoneResolutionFailureCategory expected)
    {
        TestFlowGroupResolver groupResolver = new();
        CreationRuleZoneResolver resolver = CreateResolver(groupResolver);
        WfReqTask? task = taskType == null ? null : CreateTask(
            Element(ElemFieldType.source, "10.0.0.1"), Element(ElemFieldType.destination, "192.0.2.1"));
        if (task != null)
        {
            task.TaskType = taskType!;
            task.RequestAction = requestAction!;
        }

        CreationRuleZoneResolutionFailure failure = Failure(await resolver.ResolveAsync(
            task!, [Zone(1, "Zone", "10.0.0.0", "255.255.255.255")], CancellationToken.None));

        Assert.That(failure.Category, Is.EqualTo(expected));
        Assert.That(groupResolver.Requests, Is.Empty);
    }

    [Test]
    public async Task ResolveAsync_RejectsMissingSidesAndUnknownParticipatingAction()
    {
        CreationRuleZoneResolver resolver = CreateResolver(new());
        List<ComplianceNetworkZone> zones = [Zone(1, "Zone", "10.0.0.0", "255.255.255.255")];
        WfReqTask missingDestination = CreateTask(Element(ElemFieldType.source, "10.0.0.1"));
        WfReqTask deletedSource = CreateTask(
            Element(ElemFieldType.source, "10.0.0.1", RequestAction.delete), Element(ElemFieldType.destination, "10.0.0.2"));
        WfReqTask unknownAction = CreateTask(
            Element(ElemFieldType.source, "10.0.0.1"), Element(ElemFieldType.destination, "10.0.0.2"));
        unknownAction.Elements[0].RequestAction = "renamed-action";

        List<CreationRuleZoneResolutionFailureCategory> categories = [];
        categories.Add(Failure(await resolver.ResolveAsync(missingDestination, zones, CancellationToken.None)).Category);
        categories.Add(Failure(await resolver.ResolveAsync(deletedSource, zones, CancellationToken.None)).Category);
        categories.Add(Failure(await resolver.ResolveAsync(unknownAction, zones, CancellationToken.None)).Category);

        Assert.That(categories, Is.All.EqualTo(CreationRuleZoneResolutionFailureCategory.InvalidInput));
    }

    [Test]
    public async Task ResolveAsync_NormalizesIpv4Ipv6CidrRangeAndHonorsCidrPrecedence()
    {
        CreationRuleZoneResolver resolver = CreateResolver(new());
        WfReqElement cidrElement = Element(ElemFieldType.source, "203.0.113.100");
        cidrElement.Cidr = new Cidr("10.1.0.0/24");
        cidrElement.IpString = "203.0.113.100";
        WfReqTask task = CreateTask(
            cidrElement,
            Element(ElemFieldType.source, "2001:db8::1"),
            Element(ElemFieldType.destination, "192.0.2.10", ipEnd: "192.0.2.20"));

        CreationRuleZoneInput input = Success(await resolver.ResolveAsync(task,
            [
                Zone(1, "CIDR", "10.1.0.0", "10.1.0.255"),
                Zone(2, "IPv6", "2001:db8::", "2001:db8::ffff"),
                Zone(3, "Range", "192.0.2.0", "192.0.2.255")
            ], CancellationToken.None));

        Assert.That(input.SourceZones.Select(zone => zone.Id), Is.EqualTo(new int?[] { 1, 2 }));
        Assert.That(input.DestinationZones.Single().Id, Is.EqualTo(3));
    }

    [TestCase("bad-address", null)]
    [TestCase("192.0.2.20", "192.0.2.10")]
    [TestCase("192.0.2.10", "2001:db8::1")]
    public async Task ResolveAsync_ReturnsInvalidAddressForMalformedRanges(string ipStart, string? ipEnd)
    {
        CreationRuleZoneResolver resolver = CreateResolver(new());
        WfReqTask task = CreateTask(
            Element(ElemFieldType.source, ipStart, ipEnd: ipEnd), Element(ElemFieldType.destination, "10.0.0.1"));

        CreationRuleZoneResolutionFailure failure = Failure(await resolver.ResolveAsync(task,
            [Zone(1, "Zone", "10.0.0.0", "255.255.255.255")], CancellationToken.None));

        Assert.That(failure.Category, Is.EqualTo(CreationRuleZoneResolutionFailureCategory.InvalidAddress));
    }

    [Test]
    public async Task ResolveAsync_ReturnsUnsupportedObjectWithoutNumericRepresentation()
    {
        CreationRuleZoneResolver resolver = CreateResolver(new());
        WfReqTask task = CreateTask(
            Element(ElemFieldType.source, null), Element(ElemFieldType.destination, "10.0.0.1"));

        CreationRuleZoneResolutionFailure failure = Failure(await resolver.ResolveAsync(task,
            [Zone(1, "Zone", "10.0.0.0", "255.255.255.255")], CancellationToken.None));

        Assert.That(failure.Category, Is.EqualTo(CreationRuleZoneResolutionFailureCategory.UnsupportedObject));
    }

    [Test]
    public async Task ResolveAsync_ExpandsGroupsByIdAndNameWithIdPrecedence()
    {
        TestFlowGroupResolver groupResolver = new()
        {
            Handler = parameters => new FlowGroupResolutionResult
            {
                NetworkGroups = parameters.NetworkGroupIds.Count > 0
                    ? [Group(11, "actual-id-name", Member(101, "10.0.0.1"))]
                    : [Group(12, "named-group", Member(102, "192.0.2.1"))]
            }
        };
        CreationRuleZoneResolver resolver = CreateResolver(groupResolver);
        WfReqElement source = Element(ElemFieldType.source, null);
        source.FlowNetworkGroupId = 11;
        source.GroupName = "ignored-name";
        WfReqElement destination = Element(ElemFieldType.destination, null);
        destination.GroupName = "NAMED-GROUP";

        CreationRuleZoneInput input = Success(await resolver.ResolveAsync(CreateTask(source, destination),
            [Zone(1, "Source", "10.0.0.0", "10.0.0.255"), Zone(2, "Destination", "192.0.2.0", "192.0.2.255")],
            CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceZones.Single().Id, Is.EqualTo(1));
            Assert.That(input.DestinationZones.Single().Id, Is.EqualTo(2));
            Assert.That(groupResolver.Requests, Has.Count.EqualTo(2));
            Assert.That(groupResolver.Requests[0].NetworkGroupIds, Is.EqualTo(kExpectedSingleGroupId));
            Assert.That(groupResolver.Requests[1].NetworkGroupNames, Is.EqualTo(kExpectedSingleGroupName));
        });
    }

    [Test]
    public async Task ResolveAsync_DeduplicatesAndChunksMoreThanOneHundredSelectors()
    {
        TestFlowGroupResolver groupResolver = new()
        {
            Handler = parameters => new FlowGroupResolutionResult
            {
                NetworkGroups = parameters.NetworkGroupIds.Select(id =>
                    Group(id, $"group-{id}", Member(id * 10, $"10.0.0.{(id % 250) + 1}"))).ToList()
            }
        };
        CreationRuleZoneResolver resolver = CreateResolver(groupResolver);
        List<WfReqElement> elements = [];
        for (int id = 1; id <= 101; id++)
        {
            WfReqElement source = Element(ElemFieldType.source, null);
            source.FlowNetworkGroupId = id;
            elements.Add(source);
        }
        WfReqElement duplicate = Element(ElemFieldType.source, null);
        duplicate.FlowNetworkGroupId = 1;
        elements.Add(duplicate);
        elements.Add(Element(ElemFieldType.destination, "192.0.2.1"));

        CreationRuleZoneInput input = Success(await resolver.ResolveAsync(CreateTask([.. elements]),
            [Zone(1, "Source", "10.0.0.0", "10.0.0.255"), Zone(2, "Destination", "192.0.2.0", "192.0.2.255")],
            CancellationToken.None));

        Assert.That(input.SourceZones.Single().Id, Is.EqualTo(1));
        Assert.That(groupResolver.Requests.Select(request => request.NetworkGroupIds.Count), Is.EqualTo(kExpectedChunkSizes));
    }

    [TestCase(GroupFailure.Missing)]
    [TestCase(GroupFailure.InvalidIdentity)]
    [TestCase(GroupFailure.Empty)]
    [TestCase(GroupFailure.DuplicateIdentity)]
    public async Task ResolveAsync_ReturnsGroupUnresolvedForInvalidGroupResults(GroupFailure groupFailure)
    {
        TestFlowGroupResolver groupResolver = new()
        {
            Handler = _ => groupFailure switch
            {
                GroupFailure.Missing => new(),
                GroupFailure.InvalidIdentity => new() { NetworkGroups = [Group(0, "group", Member(1, "10.0.0.1"))] },
                GroupFailure.Empty => new() { NetworkGroups = [Group(9, "group")] },
                _ => new()
                {
                    NetworkGroups =
                    [Group(9, "first", Member(1, "10.0.0.1")), Group(9, "second", Member(2, "10.0.0.2"))]
                }
            }
        };
        WfReqElement source = Element(ElemFieldType.source, null);
        source.FlowNetworkGroupId = 9;
        WfReqElement destination = Element(ElemFieldType.destination, "192.0.2.1");
        if (groupFailure == GroupFailure.DuplicateIdentity)
        {
            WfReqElement second = Element(ElemFieldType.source, null);
            second.FlowNetworkGroupId = 10;
            CreationRuleZoneResolutionFailure duplicateFailure = Failure(await CreateResolver(groupResolver).ResolveAsync(
                CreateTask(source, second, destination), [Zone(1, "Zone", "0.0.0.0", "255.255.255.255")], CancellationToken.None));
            Assert.That(duplicateFailure.Category, Is.EqualTo(CreationRuleZoneResolutionFailureCategory.GroupUnresolved));
            return;
        }

        CreationRuleZoneResolutionFailure failure = Failure(await CreateResolver(groupResolver).ResolveAsync(
            CreateTask(source, destination), [Zone(1, "Zone", "0.0.0.0", "255.255.255.255")], CancellationToken.None));

        Assert.That(failure.Category, Is.EqualTo(CreationRuleZoneResolutionFailureCategory.GroupUnresolved));
    }

    [TestCase(null, null, CreationRuleZoneResolutionFailureCategory.UnsupportedObject)]
    [TestCase("not-ip", null, CreationRuleZoneResolutionFailureCategory.InvalidAddress)]
    public async Task ResolveAsync_DoesNotDropInvalidGroupMembers(
        string? ipStart,
        string? ipEnd,
        CreationRuleZoneResolutionFailureCategory expected)
    {
        TestFlowGroupResolver groupResolver = new()
        {
            Handler = _ => new() { NetworkGroups = [Group(9, "group", Member(1, ipStart, ipEnd))] }
        };
        WfReqElement source = Element(ElemFieldType.source, null);
        source.FlowNetworkGroupId = 9;

        CreationRuleZoneResolutionFailure failure = Failure(await CreateResolver(groupResolver).ResolveAsync(
            CreateTask(source, Element(ElemFieldType.destination, "192.0.2.1")),
            [Zone(1, "Zone", "0.0.0.0", "255.255.255.255")], CancellationToken.None));

        Assert.That(failure.Category, Is.EqualTo(expected));
    }

    [Test]
    public async Task ResolveAsync_MapsGroupQueryFailureWithoutLeakingExceptionText()
    {
        TestFlowGroupResolver groupResolver = new() { Exception = new InvalidOperationException("sensitive-sentinel") };
        WfReqElement source = Element(ElemFieldType.source, null);
        source.FlowNetworkGroupId = 9;

        CreationRuleZoneResolutionFailure failure = Failure(await CreateResolver(groupResolver).ResolveAsync(
            CreateTask(source, Element(ElemFieldType.destination, "192.0.2.1")),
            [Zone(1, "Zone", "0.0.0.0", "255.255.255.255")], CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(failure.Category, Is.EqualTo(CreationRuleZoneResolutionFailureCategory.GroupQueryFailed));
            Assert.That(failure.Diagnostic, Does.Not.Contain("sensitive-sentinel"));
        });
    }

    [Test]
    public async Task ResolveAsync_IdentifiesSyntheticFallbackAndIncompleteIpv6Coverage()
    {
        RulePositioningConfiguration configuration = Config(
            autoCalculateInternetZone: false,
            internetZoneName: "Localized external");
        CreationRuleZoneResolver resolver = new(new TestFlowGroupResolver());
        WfReqTask fallbackTask = CreateTask(
            Element(ElemFieldType.source, "203.0.113.1"), Element(ElemFieldType.destination, "10.0.0.1"));
        CreationRuleZoneInput fallbackInput = Success(await resolver.ResolveAsync(fallbackTask,
            [Zone(1, "Internal", "10.0.0.0", "10.0.0.255")], configuration, CancellationToken.None));

        WfReqTask ipv6Task = CreateTask(
            Element(ElemFieldType.source, "2001:db8::1"), Element(ElemFieldType.source, "10.0.0.1"),
            Element(ElemFieldType.destination, "2001:db8:ffff::1"));
        CreationRuleZoneInput ipv6Input = Success(await resolver.ResolveAsync(ipv6Task,
            [Zone(1, "Internal", "10.0.0.0", "10.0.0.255")], configuration, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(fallbackInput.SourceZones.Single().Id, Is.Null);
            Assert.That(fallbackInput.SourceZones.Single().Kind, Is.EqualTo(PositioningZoneKind.SyntheticInternetFallback));
            Assert.That(fallbackInput.SourceZones.Single().Name, Is.EqualTo("Localized external"));
            Assert.That(ipv6Input.SourceCoverage, Is.EqualTo(ZoneCoverage.Partial));
            Assert.That(ipv6Input.DestinationCoverage, Is.EqualTo(ZoneCoverage.None));
        });
    }

    [Test]
    public async Task ResolveAsync_UsesPersistedAutomaticInternetZone()
    {
        CreationRuleZoneResolver resolver = new(new TestFlowGroupResolver());
        RulePositioningConfiguration configuration = Config(autoCalculateInternetZone: true);
        ComplianceNetworkZone internet = Zone(5, "Internet", "0.0.0.0", "255.255.255.255");
        internet.IsAutoCalculatedInternetZone = true;

        CreationRuleZoneInput input = Success(await resolver.ResolveAsync(CreateTask(
            Element(ElemFieldType.source, "203.0.113.1"), Element(ElemFieldType.destination, "198.51.100.1")),
            [internet], configuration, CancellationToken.None));

        Assert.That(input.SourceZones.Single().Kind, Is.EqualTo(PositioningZoneKind.Persisted));
        Assert.That(input.SourceZones.Single().IsAutoCalculatedInternetZone, Is.True);
    }

    [Test]
    public async Task ResolveAsync_RejectsInvalidZoneCollections()
    {
        WfReqTask task = CreateTask(Element(ElemFieldType.source, "10.0.0.1"), Element(ElemFieldType.destination, "10.0.0.2"));
        ComplianceNetworkZone malformedZone = Zone(1, "Reversed", "10.0.0.1", "10.0.0.2");
        malformedZone.IPRanges[0].Begin = IPAddress.Parse("10.0.0.3");
        List<IReadOnlyCollection<ComplianceNetworkZone>> invalidCollections =
        [
            [],
            [Zone(1, "Foreign", "10.0.0.0", "10.0.0.255", kCriterionId + 1)],
            [Zone(1, "First", "10.0.0.0", "10.0.0.127"), Zone(1, "Duplicate", "10.0.0.128", "10.0.0.255")],
            [malformedZone]
        ];

        foreach (IReadOnlyCollection<ComplianceNetworkZone> zones in invalidCollections)
        {
            CreationRuleZoneResolutionFailure failure = Failure(await CreateResolver(new()).ResolveAsync(task, zones, CancellationToken.None));
            Assert.That(failure.Category, Is.EqualTo(CreationRuleZoneResolutionFailureCategory.InvalidZoneData));
        }
    }

    [Test]
    public async Task ResolveAsync_RequiresExactlyOneValidAutomaticInternetZone()
    {
        CreationRuleZoneResolver resolver = new(new TestFlowGroupResolver());
        RulePositioningConfiguration configuration = Config(autoCalculateInternetZone: true);
        WfReqTask task = CreateTask(Element(ElemFieldType.source, "10.0.0.1"), Element(ElemFieldType.destination, "10.0.0.2"));
        ComplianceNetworkZone first = Zone(1, "Internet one", "0.0.0.0", "255.255.255.255");
        first.IsAutoCalculatedInternetZone = true;
        ComplianceNetworkZone second = Zone(2, "Internet two", "192.0.2.0", "192.0.2.255");
        second.IsAutoCalculatedInternetZone = true;
        ComplianceNetworkZone ipv6Only = Zone(3, "IPv6 Internet", "2001:db8::", "2001:db8::ffff");
        ipv6Only.IsAutoCalculatedInternetZone = true;

        CreationRuleZoneResolutionFailure missing = Failure(await resolver.ResolveAsync(task,
            [Zone(4, "Internal", "10.0.0.0", "10.0.0.255")], configuration, CancellationToken.None));
        CreationRuleZoneResolutionFailure duplicate = Failure(await resolver.ResolveAsync(
            task, [first, second], configuration, CancellationToken.None));
        CreationRuleZoneResolutionFailure malformed = Failure(await resolver.ResolveAsync(
            task, [ipv6Only], configuration, CancellationToken.None));

        Assert.That(new[] { missing.Category, duplicate.Category, malformed.Category },
            Is.All.EqualTo(CreationRuleZoneResolutionFailureCategory.InvalidZoneData));
    }

    [Test]
    public async Task ResolveAsync_ReturnsInputUnaffectedByCallerMutation()
    {
        CreationRuleZoneResolver resolver = CreateResolver(new());
        WfReqTask task = CreateTask(Element(ElemFieldType.source, "10.0.0.1"), Element(ElemFieldType.destination, "192.0.2.1"));
        List<ComplianceNetworkZone> zones =
        [Zone(1, "Source", "10.0.0.0", "10.0.0.255"), Zone(2, "Destination", "192.0.2.0", "192.0.2.255")];

        CreationRuleZoneInput input = Success(await resolver.ResolveAsync(task, zones, CancellationToken.None));
        task.Elements[0].IpString = "203.0.113.1";
        zones[0].Name = "Mutated";
        zones.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceZones.Single().Name, Is.EqualTo("Source"));
            Assert.That(input.SourceZones, Is.Not.InstanceOf<List<PositioningZoneReference>>());
            Assert.That(input.DestinationZones.Single().Name, Is.EqualTo("Destination"));
        });
    }

    [Test]
    public void ResolveAsync_PropagatesPreCancellation()
    {
        CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await CreateResolver(new()).ResolveAsync(
            CreateTask(Element(ElemFieldType.source, "10.0.0.1"), Element(ElemFieldType.destination, "10.0.0.2")),
            [Zone(1, "Zone", "10.0.0.0", "10.0.0.255")], cancellation.Token));
    }

    [Test]
    public async Task ResolveAsync_CancellationWhileGroupQueryAwaits_Propagates()
    {
        TaskCompletionSource<bool> queryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestFlowGroupResolver groupResolver = new()
        {
            AsyncHandler = async (_, cancellationToken) =>
            {
                queryStarted.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new FlowGroupResolutionResult();
            }
        };
        WfReqElement source = Element(ElemFieldType.source, null);
        source.FlowNetworkGroupId = 9;
        CancellationTokenSource cancellation = new();

        Task<CreationRuleZoneResolutionResult> resolution = CreateResolver(groupResolver).ResolveAsync(
            CreateTask(source, Element(ElemFieldType.destination, "10.0.0.1")),
            [Zone(1, "Zone", "10.0.0.0", "10.0.0.255")], cancellation.Token);
        await queryStarted.Task;
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await resolution);
    }

    [Test]
    public void ResolveAsync_CancellationDuringZoneEnumeration_Propagates()
    {
        CancellationTokenSource cancellation = new();
        CancellingZoneCollection zones = new(
            [Zone(1, "First", "10.0.0.0", "10.0.0.127"), Zone(2, "Second", "10.0.0.128", "10.0.0.255")],
            cancellation);

        Assert.CatchAsync<OperationCanceledException>(async () => await CreateResolver(new()).ResolveAsync(
            CreateTask(Element(ElemFieldType.source, "10.0.0.1"), Element(ElemFieldType.destination, "10.0.0.2")),
            zones,
            cancellation.Token));
    }

    [Test]
    public void ResultContracts_DefensivelyCopyAndUseSafeUnknownDiagnostic()
    {
        List<PositioningZoneReference> source = [new(1, "one", "One", false, false, PositioningZoneKind.Persisted)];
        CreationRuleZoneInput input = new(kCriterionId, source, [], ZoneCoverage.Complete, ZoneCoverage.None);
        source.Clear();
        CreationRuleZoneResolutionFailure failure = new((CreationRuleZoneResolutionFailureCategory)999);

        Assert.Multiple(() =>
        {
            Assert.That(input.SourceZones, Has.Count.EqualTo(1));
            Assert.That(failure.Diagnostic, Is.EqualTo("The creation-rule zones could not be resolved."));
            Assert.Throws<ArgumentNullException>(() => new CreationRuleZoneResolutionSuccess(null!));
        });
    }

    [Test]
    public async Task NormalizedInputOverload_ReusesExactRangesWithoutGroupLookup()
    {
        TestFlowGroupResolver groupResolver = new();
        CreationRuleNetworkInput networkInput = new(
            [new IPAddressRange(IPAddress.Parse("10.0.0.10"), IPAddress.Parse("10.0.0.20"))],
            [new IPAddressRange(IPAddress.Parse("192.0.2.10"), IPAddress.Parse("192.0.2.20"))]);

        CreationRuleZoneInput result = Success(await CreateResolver(groupResolver).ResolveAsync(
            networkInput,
            [Zone(1, "Source", "10.0.0.0", "10.0.0.255"), Zone(2, "Destination", "192.0.2.0", "192.0.2.255")],
            CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(result.SourceZones.Single().Id, Is.EqualTo(1));
            Assert.That(result.DestinationZones.Single().Id, Is.EqualTo(2));
            Assert.That(result.SourceCoverage, Is.EqualTo(ZoneCoverage.Complete));
            Assert.That(result.DestinationCoverage, Is.EqualTo(ZoneCoverage.Complete));
            Assert.That(groupResolver.Requests, Is.Empty);
        });
    }

    public enum GroupFailure
    {
        Missing,
        InvalidIdentity,
        Empty,
        DuplicateIdentity
    }

    private static CreationRuleZoneResolver CreateResolver(TestFlowGroupResolver groupResolver)
    {
        return new CreationRuleZoneResolver(groupResolver);
    }

    private static RulePositioningConfiguration Config(
        bool autoCalculateInternetZone,
        string internetZoneName = "Internet/Local")
    {
        return new RulePositioningConfiguration(kCriterionId, autoCalculateInternetZone, internetZoneName);
    }

    private static WfReqTask CreateTask(params WfReqElement[] elements)
    {
        return new WfReqTask
        {
            TaskType = WfTaskType.access.ToString(),
            RequestAction = RequestAction.create.ToString(),
            Elements = [.. elements]
        };
    }

    private static WfReqElement Element(
        ElemFieldType field,
        string? ipStart,
        RequestAction action = RequestAction.create,
        string? ipEnd = null)
    {
        return new WfReqElement
        {
            Field = field.ToString(),
            IpString = ipStart,
            IpEnd = ipEnd,
            RequestAction = action.ToString()
        };
    }

    private static ComplianceNetworkZone Zone(
        int id,
        string name,
        string start,
        string end,
        int criterionId = kCriterionId)
    {
        return new ComplianceNetworkZone
        {
            Id = id,
            IdString = $"zone-{id}",
            Name = name,
            CriterionId = criterionId,
            IPRanges = [new IPAddressRange(IPAddress.Parse(start), IPAddress.Parse(end))]
        };
    }

    private static FlowNetworkGroupResolution Group(long id, string name, params FlowNetworkMemberResolution[] members)
    {
        return new FlowNetworkGroupResolution { Id = id, Name = name, Members = [.. members] };
    }

    private static FlowNetworkMemberResolution Member(long id, string? start, string? end = null)
    {
        return new FlowNetworkMemberResolution { Id = id, IpStart = start ?? string.Empty, IpEnd = end ?? string.Empty };
    }

    private static CreationRuleZoneInput Success(CreationRuleZoneResolutionResult result)
    {
        Assert.That(result, Is.TypeOf<CreationRuleZoneResolutionSuccess>());
        return ((CreationRuleZoneResolutionSuccess)result).Input;
    }

    private static CreationRuleZoneResolutionFailure Failure(CreationRuleZoneResolutionResult result)
    {
        Assert.That(result, Is.TypeOf<CreationRuleZoneResolutionFailure>());
        return (CreationRuleZoneResolutionFailure)result;
    }

    private sealed class TestFlowGroupResolver : IFlowGroupResolver
    {
        public Func<FlowGroupResolutionParameters, FlowGroupResolutionResult>? Handler { get; init; }
        public Func<FlowGroupResolutionParameters, CancellationToken, Task<FlowGroupResolutionResult>>? AsyncHandler { get; init; }
        public Exception? Exception { get; init; }
        public List<FlowGroupResolutionParameters> Requests { get; } = [];

        public Task<FlowGroupResolutionResult> ResolveFlowGroupMembersAsync(
            FlowGroupResolutionParameters parameters,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(parameters);
            if (Exception != null)
            {
                throw Exception;
            }

            return AsyncHandler?.Invoke(parameters, cancellationToken)
                ?? Task.FromResult(Handler?.Invoke(parameters) ?? new FlowGroupResolutionResult());
        }
    }

    private sealed class CancellingZoneCollection(
        IReadOnlyList<ComplianceNetworkZone> zones,
        CancellationTokenSource cancellation) : IReadOnlyCollection<ComplianceNetworkZone>
    {
        public int Count => zones.Count;

        public IEnumerator<ComplianceNetworkZone> GetEnumerator()
        {
            for (int index = 0; index < zones.Count; index++)
            {
                if (index == 1)
                {
                    cancellation.Cancel();
                }

                yield return zones[index];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

internal static class CreationRuleZoneResolverTestExtensions
{
    private static readonly RulePositioningConfiguration kDefaultConfiguration = new(7, false, "Internet/Local");

    internal static Task<CreationRuleZoneResolutionResult> ResolveAsync(
        this CreationRuleZoneResolver resolver,
        WfReqTask requestTask,
        IReadOnlyCollection<ComplianceNetworkZone> networkZones,
        CancellationToken cancellationToken) =>
        resolver.ResolveAsync(requestTask, networkZones, kDefaultConfiguration, cancellationToken);

    internal static Task<CreationRuleZoneResolutionResult> ResolveAsync(
        this CreationRuleZoneResolver resolver,
        CreationRuleNetworkInput networkInput,
        IReadOnlyCollection<ComplianceNetworkZone> networkZones,
        CancellationToken cancellationToken) =>
        resolver.ResolveAsync(networkInput, networkZones, kDefaultConfiguration, cancellationToken);
}
