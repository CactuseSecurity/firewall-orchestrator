namespace FWO.Data.RulePositioning;

/// <summary>
/// Immutable global settings used consistently throughout one positioning calculation.
/// </summary>
public sealed record RulePositioningConfiguration(
    int DesignatedZoneMatrixId,
    bool AutoCalculateInternetZone,
    string InternetLocalZoneName);
