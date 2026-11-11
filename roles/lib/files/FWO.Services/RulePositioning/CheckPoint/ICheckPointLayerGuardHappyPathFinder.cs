using System.Net;
using FWO.Data;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;

namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>Finds one production-validated LayerGuard zone-pair positioning candidate.</summary>
public interface ICheckPointLayerGuardHappyPathFinder
{
    /// <summary>Searches already-loaded positioning snapshots without performing I/O.</summary>
    Task<CheckPointLayerGuardHappyPathSearchResult> FindAsync(
        RulePositioningContext context,
        RulebaseGraphSnapshot graph,
        CheckPointLayerGuardSnapshot guards,
        IReadOnlyCollection<NetworkZone> networkZones,
        RulePositioningConfiguration configuration,
        CheckPointSectionNameTemplate sectionNameTemplate,
        CancellationToken cancellationToken);
}

/// <summary>Terminal state of one bounded happy-path search.</summary>
public enum CheckPointLayerGuardHappyPathSearchStatus
{
    Found,
    NoCandidate,
    Indeterminate,
    SearchLimitReached
}

/// <summary>Stable reason category accompanying a terminal search state.</summary>
public enum CheckPointLayerGuardHappyPathSearchReason
{
    CandidateValidated,
    Exhausted,
    InvalidSnapshot,
    GuardIndeterminate,
    RenderedZonePairLimit,
    RepresentativeAddressLimit,
    AddressPairLimit
}

/// <summary>One immutable diagnostic candidate containing only bounded identifying values.</summary>
public sealed record CheckPointLayerGuardHappyPathCandidate
{
    public IPAddress SourceAddress { get; }
    public IPAddress DestinationAddress { get; }
    public int SourceZoneId { get; }
    public string SourceZoneName { get; }
    public int DestinationZoneId { get; }
    public string DestinationZoneName { get; }
    public long GuardRuleId { get; }
    public long TargetLayerId { get; }
    public string TargetLayerName { get; }
    public string TargetLayerUid { get; }
    public long SectionId { get; }
    public string SectionName { get; }
    public string SectionUid { get; }
    public string RenderedSectionName { get; }
    public CheckPointRulePosition PredictedPosition { get; }

    /// <summary>Creates one fully identified, immutable candidate.</summary>
    public CheckPointLayerGuardHappyPathCandidate(
        IPAddress sourceAddress,
        IPAddress destinationAddress,
        int sourceZoneId,
        string sourceZoneName,
        int destinationZoneId,
        string destinationZoneName,
        long guardRuleId,
        long targetLayerId,
        string targetLayerName,
        string targetLayerUid,
        long sectionId,
        string sectionName,
        string sectionUid,
        string renderedSectionName,
        CheckPointRulePosition predictedPosition)
    {
        SourceAddress = sourceAddress ?? throw new ArgumentNullException(nameof(sourceAddress));
        DestinationAddress = destinationAddress ?? throw new ArgumentNullException(nameof(destinationAddress));
        SourceZoneId = RequirePositive(sourceZoneId, nameof(sourceZoneId));
        SourceZoneName = RequireText(sourceZoneName, nameof(sourceZoneName));
        DestinationZoneId = RequirePositive(destinationZoneId, nameof(destinationZoneId));
        DestinationZoneName = RequireText(destinationZoneName, nameof(destinationZoneName));
        GuardRuleId = RequirePositive(guardRuleId, nameof(guardRuleId));
        TargetLayerId = RequirePositive(targetLayerId, nameof(targetLayerId));
        TargetLayerName = RequireText(targetLayerName, nameof(targetLayerName));
        TargetLayerUid = RequireText(targetLayerUid, nameof(targetLayerUid));
        SectionId = RequirePositive(sectionId, nameof(sectionId));
        SectionName = RequireText(sectionName, nameof(sectionName));
        SectionUid = RequireText(sectionUid, nameof(sectionUid));
        RenderedSectionName = RequireText(renderedSectionName, nameof(renderedSectionName));
        PredictedPosition = predictedPosition ?? throw new ArgumentNullException(nameof(predictedPosition));
    }

    private static int RequirePositive(int value, string parameterName) => value > 0
        ? value
        : throw new ArgumentOutOfRangeException(parameterName);

    private static long RequirePositive(long value, string parameterName) => value > 0
        ? value
        : throw new ArgumentOutOfRangeException(parameterName);

    private static string RequireText(string value, string parameterName) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A value is required.", parameterName);
}

/// <summary>Bounded counters describing how far one search progressed.</summary>
public sealed record CheckPointLayerGuardHappyPathSearchStatistics
{
    public int RenderedZonePairCount { get; }
    public int StructuralMatchCount { get; }
    public int AddressPairValidationCount { get; }
    public int ZoneRejectionCount { get; }
    public int GuardRejectionCount { get; }
    public int SectionRejectionCount { get; }
    public int RecipeRejectionCount { get; }

    /// <summary>Rejects negative counters.</summary>
    public CheckPointLayerGuardHappyPathSearchStatistics(
        int renderedZonePairCount,
        int structuralMatchCount,
        int addressPairValidationCount,
        int zoneRejectionCount,
        int guardRejectionCount,
        int sectionRejectionCount,
        int recipeRejectionCount)
    {
        if (renderedZonePairCount < 0 || structuralMatchCount < 0 || addressPairValidationCount < 0 ||
            zoneRejectionCount < 0 || guardRejectionCount < 0 || sectionRejectionCount < 0 ||
            recipeRejectionCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderedZonePairCount));
        }
        RenderedZonePairCount = renderedZonePairCount;
        StructuralMatchCount = structuralMatchCount;
        AddressPairValidationCount = addressPairValidationCount;
        ZoneRejectionCount = zoneRejectionCount;
        GuardRejectionCount = guardRejectionCount;
        SectionRejectionCount = sectionRejectionCount;
        RecipeRejectionCount = recipeRejectionCount;
    }
}

/// <summary>Immutable terminal result of one candidate search.</summary>
public sealed record CheckPointLayerGuardHappyPathSearchResult
{
    public CheckPointLayerGuardHappyPathSearchStatus Status { get; }
    public CheckPointLayerGuardHappyPathSearchReason Reason { get; }
    public CheckPointLayerGuardHappyPathCandidate? Candidate { get; }
    public CheckPointLayerGuardHappyPathSearchStatistics Statistics { get; }

    /// <summary>Creates a result whose candidate presence agrees with its status.</summary>
    public CheckPointLayerGuardHappyPathSearchResult(
        CheckPointLayerGuardHappyPathSearchStatus status,
        CheckPointLayerGuardHappyPathSearchReason reason,
        CheckPointLayerGuardHappyPathCandidate? candidate,
        CheckPointLayerGuardHappyPathSearchStatistics statistics)
    {
        if (!Enum.IsDefined(status) || !Enum.IsDefined(reason) ||
            status == CheckPointLayerGuardHappyPathSearchStatus.Found != (candidate != null))
        {
            throw new ArgumentException("Search status and candidate are inconsistent.", nameof(candidate));
        }
        Status = status;
        Reason = reason;
        Candidate = candidate;
        Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }
}
