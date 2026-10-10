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

    /// <summary>
    /// Builds the schema of a paged list request with the request-visible filter and the paging keys.
    /// </summary>
    /// <param name="endpointName">Name of the endpoint used in validation messages.</param>
    /// <param name="maxLimit">Maximum page size of the endpoint.</param>
    public static RequestRootValidationSchema ForPagedVisibleInRequest(string endpointName, int maxLimit) => new(
        endpointName,
        [
            new RequestKeyDefinition("filter", "Optional filter container for request-visible settings."),
            .. FlowCatalogPaging.KeyDefinitions(maxLimit)
        ]);
}
