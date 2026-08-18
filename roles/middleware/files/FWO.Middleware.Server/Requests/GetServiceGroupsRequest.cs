namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetServiceGroupsRequest type.
/// </summary>
public sealed class GetServiceGroupsRequest : RequestDto<RequestOptionsDto<VisibleInRequestFilter>>, IVisibleInRequestFilterRequest
{
}
