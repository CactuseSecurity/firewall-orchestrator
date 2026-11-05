using FWO.Basics;

namespace FWO.Data.RulePositioning;

/// <summary>Normalized protocol and destination-port coverage used during rule positioning.</summary>
public sealed class PositioningService
{
    /// <summary>Gets the IP protocol identifier, or <see cref="GlobalConst.kAnyIpProtocolId"/> for any protocol.</summary>
    public int ProtocolId { get; }

    /// <summary>Gets the inclusive first destination port for protocols that support ports.</summary>
    public int? DestinationPortStart { get; }

    /// <summary>Gets the inclusive last destination port for protocols that support ports.</summary>
    public int? DestinationPortEnd { get; }

    /// <summary>Creates and validates normalized service coverage.</summary>
    public PositioningService(int protocolId, int? destinationPortStart, int? destinationPortEnd)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(protocolId, GlobalConst.kAnyIpProtocolId);
        if (protocolId == GlobalConst.kAnyIpProtocolId)
        {
            EnsureNoPorts(destinationPortStart, destinationPortEnd, nameof(protocolId));
            ProtocolId = protocolId;
            return;
        }
        if (!IpProtocol.HasPorts(protocolId))
        {
            EnsureNoPorts(destinationPortStart, destinationPortEnd, nameof(protocolId));
            ProtocolId = protocolId;
            return;
        }

        ValidatePorts(destinationPortStart, destinationPortEnd);
        ProtocolId = protocolId;
        DestinationPortStart = destinationPortStart;
        DestinationPortEnd = destinationPortStart.HasValue
            ? destinationPortEnd ?? destinationPortStart
            : null;
    }

    /// <summary>Rejects port information for protocols without destination ports.</summary>
    private static void EnsureNoPorts(int? start, int? end, string parameterName)
    {
        if (start.HasValue || end.HasValue)
        {
            throw new ArgumentException("Destination ports are only valid for TCP and UDP.", parameterName);
        }
    }

    /// <summary>Validates optional inclusive destination-port bounds.</summary>
    private static void ValidatePorts(int? start, int? end)
    {
        if (!start.HasValue && end.HasValue)
        {
            throw new ArgumentException("A destination port end requires a start.", nameof(end));
        }
        ValidatePort(start, nameof(start));
        ValidatePort(end, nameof(end));
        if (start.HasValue && end.HasValue && start > end)
        {
            throw new ArgumentException("Destination port start must not exceed the end.", nameof(end));
        }
    }

    /// <summary>Validates one destination-port bound when present.</summary>
    private static void ValidatePort(int? port, string parameterName)
    {
        if (port is < 0 or > GlobalConst.kMaxPortNumber)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
