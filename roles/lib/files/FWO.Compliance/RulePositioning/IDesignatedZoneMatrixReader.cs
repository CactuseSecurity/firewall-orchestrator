using FWO.Data;
using FWO.Data.RulePositioning;
using System.Collections.ObjectModel;

namespace FWO.Compliance.RulePositioning;

/// <summary>Loads the configured designated zone matrix for positioning calculations.</summary>
public interface IDesignatedZoneMatrixReader
{
    /// <summary>Loads the selected matrix and its network zones.</summary>
    Task<PositioningResult<DesignatedZoneMatrixReadValue, DesignatedZoneMatrixReadFailureCategory>> ReadAsync(
        int criterionId,
        CancellationToken cancellationToken);
}

/// <summary>Immutable value returned by a successful designated-zone-matrix read.</summary>
public sealed class DesignatedZoneMatrixReadValue
{
    /// <summary>Creates an immutable copy of the selected designated-zone matrix rows.</summary>
    public DesignatedZoneMatrixReadValue(IReadOnlyCollection<NetworkZone> networkZones)
    {
        ArgumentNullException.ThrowIfNull(networkZones);
        NetworkZones = new ReadOnlyCollection<NetworkZone>(networkZones.ToList());
    }

    /// <summary>Gets the network zones returned for the configured matrix.</summary>
    public IReadOnlyList<NetworkZone> NetworkZones { get; }
}

/// <summary>Maps designated-zone-matrix failures to fixed caller-safe diagnostics.</summary>
public static class DesignatedZoneMatrixReadFailureDiagnostics
{
    /// <summary>Maps a failure category to its fixed safe diagnostic.</summary>
    public static string GetDiagnostic(this DesignatedZoneMatrixReadFailureCategory category) => category switch
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
