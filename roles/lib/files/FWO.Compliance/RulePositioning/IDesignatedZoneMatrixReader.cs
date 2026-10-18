using FWO.Data;
using System.Collections.ObjectModel;

namespace FWO.Compliance.RulePositioning;

/// <summary>Loads the configured designated zone matrix for positioning calculations.</summary>
public interface IDesignatedZoneMatrixReader
{
    /// <summary>Loads the configured matrix and its network zones.</summary>
    Task<DesignatedZoneMatrixReadResult> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>Base result for a designated-zone-matrix read.</summary>
public abstract class DesignatedZoneMatrixReadResult;

/// <summary>A successful designated-zone-matrix read.</summary>
public sealed class DesignatedZoneMatrixReadSuccess : DesignatedZoneMatrixReadResult
{
    /// <summary>Creates a successful result with a defensive collection copy.</summary>
    public DesignatedZoneMatrixReadSuccess(IReadOnlyCollection<ComplianceNetworkZone> networkZones)
    {
        ArgumentNullException.ThrowIfNull(networkZones);
        NetworkZones = new ReadOnlyCollection<ComplianceNetworkZone>([.. networkZones]);
    }

    /// <summary>Gets the network zones returned for the configured matrix.</summary>
    public IReadOnlyList<ComplianceNetworkZone> NetworkZones { get; }
}

/// <summary>A failed designated-zone-matrix read with a safe diagnostic.</summary>
public sealed class DesignatedZoneMatrixReadFailure : DesignatedZoneMatrixReadResult
{
    /// <summary>Creates a failed result.</summary>
    public DesignatedZoneMatrixReadFailure(DesignatedZoneMatrixReadFailureCategory category)
    {
        Category = category;
        Diagnostic = GetDiagnostic(category);
    }

    /// <summary>Gets the stable failure category.</summary>
    public DesignatedZoneMatrixReadFailureCategory Category { get; }

    /// <summary>Gets the fixed caller-safe diagnostic.</summary>
    public string Diagnostic { get; }

    /// <summary>Maps a failure category to its fixed safe diagnostic.</summary>
    private static string GetDiagnostic(DesignatedZoneMatrixReadFailureCategory category) => category switch
    {
        DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid =>
            "The designated zone-matrix configuration is invalid.",
        DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable =>
            "The designated zone matrix is unavailable.",
        _ => "The designated zone matrix could not be loaded."
    };
}

/// <summary>Stable failure categories for designated-zone-matrix reads.</summary>
public enum DesignatedZoneMatrixReadFailureCategory
{
    ConfigurationInvalid,
    MatrixUnavailable,
    QueryFailed
}
