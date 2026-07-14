namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the RequestFilterValidationSchema record.
/// </summary>
public sealed record RequestFilterValidationSchema(
    string EndpointName,
    IReadOnlyList<RequestKeyDefinition> AllowedKeys)
{
    /// <summary>
    /// Performs the ForVisibleInRequest operation.
    /// </summary>
    public static RequestFilterValidationSchema ForVisibleInRequest(string endpointName) => new(
        endpointName,
        [
            new RequestKeyDefinition("visibleInRequest", "Include only objects visible in the current request.")
        ]);
}
