namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the RequestRootValidationSchema record.
/// </summary>
public sealed record RequestRootValidationSchema(
    string EndpointName,
    IReadOnlyList<RequestKeyDefinition> AllowedKeys)
{
    private static readonly List<RequestKeyDefinition> kVisibleInRequestKeys =
    [
        new RequestKeyDefinition("filter", "Optional filter container for request-visible settings.")
    ];

    /// <summary>
    /// Performs the ForVisibleInRequest operation.
    /// </summary>
    public static RequestRootValidationSchema ForVisibleInRequest(string endpointName) => new(
        endpointName,
        kVisibleInRequestKeys);

    /// <summary>
    /// Builds the schema of a paged list request with the request-visible filter and the paging keys.
    /// </summary>
    /// <param name="endpointName">Name of the endpoint used in validation messages.</param>
    public static RequestRootValidationSchema ForPagedVisibleInRequest(string endpointName)
    {
        List<RequestKeyDefinition> allowedKeys = [.. kVisibleInRequestKeys, .. FlowCatalogPaging.KeyDefinitions()];
        return new RequestRootValidationSchema(endpointName, allowedKeys);
    }
}
