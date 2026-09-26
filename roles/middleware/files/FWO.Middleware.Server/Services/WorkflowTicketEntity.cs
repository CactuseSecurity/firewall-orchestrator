using System.Globalization;
using FWO.Basics;
using FWO.Data.Flow;
using FWO.Logging;
using FWO.Middleware.Server.Requests;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Identifies the kind of request-local entity represented by an internal id.
/// </summary>
internal enum WorkflowTicketEntityKind
{
    AddressObject,
    AddressGroup,
    ServiceObject,
    ServiceGroup,
    TimeObject
}

/// <summary>
/// Represents a request-local entity while a workflow ticket is being built.
/// </summary>
internal sealed record WorkflowTicketEntity(
    long Id,
    WorkflowTicketEntityKind Kind,
    string DisplayName,
    string? IpStart = null,
    string? IpEnd = null,
    int? ProtocolId = null,
    int? PortStart = null,
    int? PortEnd = null,
    DateTime? TimeStart = null,
    DateTime? TimeEnd = null)
{
    public static WorkflowTicketEntity FromAddressObject(long id, CreateTicketRequest.CreateAddressObjectRequest request)
    {
        return new WorkflowTicketEntity(id, WorkflowTicketEntityKind.AddressObject, request.Name, request.IpStart, request.IpEnd);
    }

    public static WorkflowTicketEntity FromAddressGroup(CreateTicketRequest.CreateAddressGroupRequest request)
    {
        return new WorkflowTicketEntity(request.Id, WorkflowTicketEntityKind.AddressGroup, request.Name);
    }

    public static WorkflowTicketEntity FromServiceObject(long id, CreateTicketRequest.CreateServiceObjectRequest request, Dictionary<string, int> protocolIds)
    {
        int protocolId = ResolveProtocolId(request.Protocol, protocolIds, request.PortStart, request.PortEnd);
        return new WorkflowTicketEntity(id, WorkflowTicketEntityKind.ServiceObject, request.Name,
            ProtocolId: protocolId, PortStart: request.PortStart, PortEnd: request.PortEnd);
    }

    public static WorkflowTicketEntity FromServiceGroup(CreateTicketRequest.CreateServiceGroupRequest request)
    {
        return new WorkflowTicketEntity(request.Id, WorkflowTicketEntityKind.ServiceGroup, request.Name);
    }

    public static WorkflowTicketEntity FromTimeObject(long id, CreateTicketRequest.CreateTimeObjectRequest request)
    {
        DateTime? startTime = ParseDateTime(request.StartTime, "startTime");
        DateTime? endTime = ParseDateTime(request.EndTime, "endTime");
        return new WorkflowTicketEntity(id, WorkflowTicketEntityKind.TimeObject, request.Name, TimeStart: startTime, TimeEnd: endTime);
    }

    public static WorkflowTicketEntity FromFlowTimeObject(FlowTimeObject flowObject)
    {
        return new WorkflowTicketEntity(flowObject.Id, WorkflowTicketEntityKind.TimeObject, flowObject.Name,
            TimeStart: flowObject.StartTime, TimeEnd: flowObject.EndTime);
    }

    private static DateTime? ParseDateTime(string value, string fieldName)
    {
        try
        {
            DateTime parsedDateTime = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (parsedDateTime.Kind == DateTimeKind.Unspecified)
            {
                TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(parsedDateTime);
                Log.WriteWarning("Flow Request", $"Time object {fieldName} '{value}' has no timezone offset. Interpreting it as middleware local time with UTC offset {utcOffset}.");
            }

            return parsedDateTime;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException($"The time object {fieldName} '{value}' must be a valid date/time value.", fieldName, exception);
        }
    }

    private static int ResolveProtocolId(string protocol, Dictionary<string, int> protocolIds, int? portStart, int? portEnd)
    {
        if (int.TryParse(protocol, out int protocolId))
        {
            return ValidateResolvedProtocolId(protocol, protocolId, protocolIds.ContainsValue(protocolId), portStart, portEnd);
        }

        if (protocolIds.TryGetValue(protocol, out protocolId))
        {
            return ValidateResolvedProtocolId(protocol, protocolId, true, portStart, portEnd);
        }

        throw new ArgumentException($"The service object protocol '{protocol}' must match a configured STM protocol name or id.");
    }

    private static int ValidateResolvedProtocolId(string protocol, int protocolId, bool isConfigured, int? portStart, int? portEnd)
    {
        if (isConfigured && protocolId >= 0)
        {
            ValidatePortRange(protocol, portStart, portEnd);
            return protocolId;
        }

        bool isCanonicalAnyIpProtocol = isConfigured && protocolId == GlobalConst.kAnyIpProtocolId && portStart is null && portEnd is null;
        if (isCanonicalAnyIpProtocol)
        {
            return protocolId;
        }

        throw new ArgumentException($"The service object protocol '{protocol}' must match a non-negative configured STM protocol name or id, or be the canonical any-IP-protocol service without ports.");
    }

    private static void ValidatePortRange(string protocol, int? portStart, int? portEnd)
    {
        if (portStart is null && portEnd is not null)
        {
            throw new ArgumentException($"The service object protocol '{protocol}' has a 'portEnd' value without a 'portStart' value.");
        }
    }
}
