using FWO.Data;
using FWO.Data.Workflow;
using System.Collections.ObjectModel;

namespace FWO.Compliance.RulePositioning;

/// <summary>Resolves workflow network elements to positioning-zone input.</summary>
public interface ICreationRuleZoneResolver
{
    /// <summary>Resolves the source and destination zones for a new access rule.</summary>
    Task<CreationRuleZoneResolutionResult> ResolveAsync(
        WfReqTask requestTask,
        IReadOnlyCollection<ComplianceNetworkZone> networkZones,
        CancellationToken cancellationToken);
}

/// <summary>Base result for creation-rule zone resolution.</summary>
public abstract class CreationRuleZoneResolutionResult;

/// <summary>Successful creation-rule zone resolution.</summary>
public sealed class CreationRuleZoneResolutionSuccess : CreationRuleZoneResolutionResult
{
    /// <summary>Creates a successful result.</summary>
    public CreationRuleZoneResolutionSuccess(CreationRuleZoneInput input)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
    }

    /// <summary>Gets the immutable positioning input.</summary>
    public CreationRuleZoneInput Input { get; }
}

/// <summary>Failed creation-rule zone resolution with a safe diagnostic.</summary>
public sealed class CreationRuleZoneResolutionFailure : CreationRuleZoneResolutionResult
{
    /// <summary>Creates a failed result.</summary>
    public CreationRuleZoneResolutionFailure(CreationRuleZoneResolutionFailureCategory category)
    {
        Category = category;
        Diagnostic = GetDiagnostic(category);
    }

    /// <summary>Gets the failure category.</summary>
    public CreationRuleZoneResolutionFailureCategory Category { get; }

    /// <summary>Gets the fixed, non-sensitive failure diagnostic.</summary>
    public string Diagnostic { get; }

    /// <summary>Maps a category to its safe diagnostic.</summary>
    private static string GetDiagnostic(CreationRuleZoneResolutionFailureCategory category)
    {
        return category switch
        {
            CreationRuleZoneResolutionFailureCategory.InvalidInput => "The creation-rule zone input is invalid.",
            CreationRuleZoneResolutionFailureCategory.UnsupportedTask => "The request task is not a supported access-rule creation.",
            CreationRuleZoneResolutionFailureCategory.GroupUnresolved => "A referenced network group could not be resolved.",
            CreationRuleZoneResolutionFailureCategory.GroupQueryFailed => "Referenced network groups could not be loaded.",
            CreationRuleZoneResolutionFailureCategory.UnsupportedObject => "A network object has no supported address representation.",
            CreationRuleZoneResolutionFailureCategory.InvalidAddress => "A network address is invalid.",
            CreationRuleZoneResolutionFailureCategory.InvalidZoneData => "The designated zone data is incomplete or invalid.",
            _ => "The creation-rule zones could not be resolved."
        };
    }
}

/// <summary>Immutable positioning input for one creation rule.</summary>
public sealed class CreationRuleZoneInput
{
    /// <summary>Creates positioning input and defensively copies its zone collections.</summary>
    public CreationRuleZoneInput(
        int criterionId,
        IReadOnlyCollection<PositioningZoneReference> sourceZones,
        IReadOnlyCollection<PositioningZoneReference> destinationZones,
        ZoneCoverage sourceCoverage,
        ZoneCoverage destinationCoverage)
    {
        if (criterionId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(criterionId));
        }

        ArgumentNullException.ThrowIfNull(sourceZones);
        ArgumentNullException.ThrowIfNull(destinationZones);
        CriterionId = criterionId;
        SourceZones = new ReadOnlyCollection<PositioningZoneReference>([.. sourceZones]);
        DestinationZones = new ReadOnlyCollection<PositioningZoneReference>([.. destinationZones]);
        SourceCoverage = sourceCoverage;
        DestinationCoverage = destinationCoverage;
    }

    /// <summary>Gets the designated zone-matrix criterion ID.</summary>
    public int CriterionId { get; }

    /// <summary>Gets ordered source-zone references.</summary>
    public IReadOnlyList<PositioningZoneReference> SourceZones { get; }

    /// <summary>Gets ordered destination-zone references.</summary>
    public IReadOnlyList<PositioningZoneReference> DestinationZones { get; }

    /// <summary>Gets source range coverage.</summary>
    public ZoneCoverage SourceCoverage { get; }

    /// <summary>Gets destination range coverage.</summary>
    public ZoneCoverage DestinationCoverage { get; }
}

/// <summary>Immutable positioning-safe representation of a resolved zone.</summary>
public sealed class PositioningZoneReference
{
    /// <summary>Creates a zone reference.</summary>
    public PositioningZoneReference(
        int? id,
        string idString,
        string name,
        bool isAutoCalculatedInternetZone,
        bool isAutoCalculatedUndefinedInternalZone,
        PositioningZoneKind kind)
    {
        Id = id;
        IdString = idString ?? string.Empty;
        Name = name ?? string.Empty;
        IsAutoCalculatedInternetZone = isAutoCalculatedInternetZone;
        IsAutoCalculatedUndefinedInternalZone = isAutoCalculatedUndefinedInternalZone;
        Kind = kind;
    }

    /// <summary>Gets the persisted ID, or null for a synthetic zone.</summary>
    public int? Id { get; }

    /// <summary>Gets the external zone identifier.</summary>
    public string IdString { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets whether this is the persisted automatic Internet zone.</summary>
    public bool IsAutoCalculatedInternetZone { get; }

    /// <summary>Gets whether this is the persisted automatic undefined-internal zone.</summary>
    public bool IsAutoCalculatedUndefinedInternalZone { get; }

    /// <summary>Gets the stable positioning-zone kind.</summary>
    public PositioningZoneKind Kind { get; }
}

/// <summary>Stable kind of a resolved positioning zone.</summary>
public enum PositioningZoneKind
{
    Persisted,
    SyntheticInternetFallback
}

/// <summary>Coverage of normalized address ranges by resolved zones.</summary>
public enum ZoneCoverage
{
    Complete,
    Partial,
    None
}

/// <summary>Safe failure categories for creation-rule zone resolution.</summary>
public enum CreationRuleZoneResolutionFailureCategory
{
    InvalidInput,
    UnsupportedTask,
    GroupUnresolved,
    GroupQueryFailed,
    UnsupportedObject,
    InvalidAddress,
    InvalidZoneData,
    UnexpectedFailure
}
