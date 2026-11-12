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

    /// <summary>
    /// Creates the validation schema used by Flow Catalog group endpoints.
    /// </summary>
    public static RequestFilterValidationSchema ForFlowCatalogGroups(string endpointName) => new(
        endpointName,
        [
            new RequestKeyDefinition("visibleInRequest", "Include only objects visible in the current request."),
            new RequestKeyDefinition(
                "includeTechnicalDetails",
                "Include the standalone object fields for every group member.")
        ]);
}
