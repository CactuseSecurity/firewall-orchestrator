using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;

namespace FWO.Services.RulePositioning;

/// <summary>
/// Calculates rule positions for one exact device-type and positioning-algorithm combination.
/// </summary>
public interface IRulePositioningStrategy
{
    /// <summary>
    /// Determines whether this strategy supports the exact target device type and algorithm.
    /// </summary>
    bool CanHandle(
        DeviceType deviceType,
        ProvisioningPositioningAlgorithm positioningAlgorithm);

    /// <summary>
    /// Calculates a position from an already validated, caller-supplied context.
    /// </summary>
    Task<RulePositioningResult> CalculateAsync(
        RulePositioningContext context,
        CancellationToken cancellationToken);
}
