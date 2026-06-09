namespace FWO.Middleware.Server.Requests;

internal sealed record RequestFilterValidationSchema(
    string EndpointName,
    IReadOnlyList<RequestFilterKeyDefinition> AllowedKeys)
{
    public static RequestFilterValidationSchema ForVisibleInRequest(string endpointName) => new(
        endpointName,
        [
            new RequestFilterKeyDefinition("visibleInRequest", "Include only objects visible in the current request.")
        ]);
}

internal sealed record RequestFilterKeyDefinition(string JsonName, string Description);
