namespace FWO.Middleware.Server.Requests;

public sealed record RequestFilterValidationSchema(
    string EndpointName,
    IReadOnlyList<RequestKeyDefinition> AllowedKeys)
{
    public static RequestFilterValidationSchema ForVisibleInRequest(string endpointName) => new(
        endpointName,
        [
            new RequestKeyDefinition("visibleInRequest", "Include only objects visible in the current request.")
        ]);
}
