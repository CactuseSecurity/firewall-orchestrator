namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetServiceObjectsRequest type.
/// </summary>
public sealed class GetServiceObjectsRequest : RequestDto<RequestOptionsDto<VisibleInRequestFilter>>, IVisibleInRequestFilterRequest
{
}
