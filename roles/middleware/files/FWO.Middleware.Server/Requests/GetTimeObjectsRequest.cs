namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetTimeObjectsRequest type.
/// </summary>
public sealed class GetTimeObjectsRequest : RequestDto<RequestOptionsDto<VisibleInRequestFilter>>, IVisibleInRequestFilterRequest
{
}
