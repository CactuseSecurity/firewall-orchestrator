namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetAddressObjectsRequest type.
/// </summary>
public sealed class GetAddressObjectsRequest : RequestDto<RequestOptionsDto<VisibleInRequestFilter>>, IVisibleInRequestFilterRequest
{
}
