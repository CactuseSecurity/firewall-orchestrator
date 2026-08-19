namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetAddressGroupsRequest type.
/// </summary>
public sealed class GetAddressGroupsRequest : RequestDto<RequestOptionsDto<VisibleInRequestFilter>>, IVisibleInRequestFilterRequest
{
}
