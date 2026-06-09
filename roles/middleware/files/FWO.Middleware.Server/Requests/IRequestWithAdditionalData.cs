using System.Text.Json;

namespace FWO.Middleware.Server.Requests;

public interface IRequestWithAdditionalData
{
    Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
