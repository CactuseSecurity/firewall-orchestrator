namespace FWO.Middleware.Server.Requests;

public sealed record RequestRootValidationSchema(
    string EndpointName,
    IReadOnlyList<RequestKeyDefinition> AllowedKeys)
{
    public static RequestRootValidationSchema ForVisibleInRequest(string endpointName) => new(
        endpointName,
        [
            new RequestKeyDefinition("filter", "Optional filter container for request-visible settings.")
        ]);
}
