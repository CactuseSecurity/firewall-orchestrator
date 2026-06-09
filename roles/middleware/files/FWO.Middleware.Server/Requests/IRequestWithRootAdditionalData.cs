using System.Text.Json;

namespace FWO.Middleware.Server.Requests;

public interface IRequestWithRootAdditionalData
{
    Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
