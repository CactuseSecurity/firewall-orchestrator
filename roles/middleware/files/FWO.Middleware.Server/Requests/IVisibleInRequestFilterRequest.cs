using System.Text.Json;

namespace FWO.Middleware.Server.Requests;

public interface IVisibleInRequestFilterRequest : IRequestWithRootAdditionalData
{
    VisibleInRequestFilter? Filter { get; set; }
}
