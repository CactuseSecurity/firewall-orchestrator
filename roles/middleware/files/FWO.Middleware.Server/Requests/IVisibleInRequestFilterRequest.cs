using System.Text.Json;

namespace FWO.Middleware.Server.Requests;

internal interface IVisibleInRequestFilterRequest
{
    VisibleInRequestFilter? Filter { get; set; }

    Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
