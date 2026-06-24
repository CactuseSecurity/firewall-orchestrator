namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the RequestRootValidationSchema record.
/// </summary>
public sealed record RequestRootValidationSchema(
    string EndpointName,
    IReadOnlyList<RequestKeyDefinition> AllowedKeys)
{
    /// <summary>
    /// Performs the ForVisibleInRequest operation.
    /// </summary>
    public static RequestRootValidationSchema ForVisibleInRequest(string endpointName) => new(
        endpointName,
        [
            new RequestKeyDefinition("filter", "Optional filter container for request-visible settings.")
        ]);
}
