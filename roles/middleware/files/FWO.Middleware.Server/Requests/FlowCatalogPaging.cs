using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Defines the paging keys of the flow catalog list requests.
/// </summary>
public interface IPagedListRequest
{
    /// <summary>
    /// Gets or sets the maximum number of items returned; null returns a page of the maximum size.
    /// </summary>
    int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the number of items skipped before the first returned item; null skips no item.
    /// </summary>
    int? Offset { get; set; }
}

/// <summary>
/// Page size limits, key descriptions and validation of the flow catalog list requests.
/// </summary>
public static class FlowCatalogPaging
{
    /// <summary>Maximum and default page size of the object lists (address, service and time objects).</summary>
    public const int kMaxObjectLimit = 1000;

    /// <summary>Maximum and default page size of the group lists, which carry their members.</summary>
    public const int kMaxGroupLimit = 250;

    private const string kLimitKey = "limit";
    private const string kOffsetKey = "offset";

    /// <summary>
    /// Describes the 'limit' key for the given maximum page size.
    /// </summary>
    public static string DescribeLimit(int maxLimit) =>
        $"Maximum number of items returned, between 1 and {maxLimit}; omitted or null returns {maxLimit}. Items are ordered by name and id, " +
        $"so 'limit' and 'offset' page the result deterministically; the response header '{ListPaging.kHasMoreHeader}' tells whether further items follow.";

    /// <summary>
    /// Describes the 'offset' key.
    /// </summary>
    public const string kOffsetDescription = "Number of items skipped before the first returned item, at least 0; omitted or null skips no item.";

    /// <summary>
    /// Returns the root key definitions of the paging keys.
    /// </summary>
    public static List<RequestKeyDefinition> KeyDefinitions(int maxLimit) =>
    [
        new RequestKeyDefinition(kLimitKey, DescribeLimit(maxLimit)),
        new RequestKeyDefinition(kOffsetKey, kOffsetDescription)
    ];

    /// <summary>
    /// Returns the page size of a request; an omitted or null limit selects the maximum page size of the endpoint.
    /// </summary>
    /// <param name="request">Request carrying the paging keys.</param>
    /// <param name="maxLimit">Maximum page size of the endpoint.</param>
    public static int GetPageSize(IPagedListRequest request, int maxLimit)
    {
        return request.Limit ?? maxLimit;
    }

    /// <summary>
    /// Validates both paging keys and reports every invalid key together.
    /// </summary>
    /// <param name="request">Request carrying the paging keys.</param>
    /// <param name="maxLimit">Maximum page size of the endpoint.</param>
    /// <param name="errorResult">The bad request result naming every invalid key, or null if both are valid.</param>
    /// <returns>True if both paging keys are valid.</returns>
    public static bool TryValidate(IPagedListRequest request, int maxLimit, out ActionResult? errorResult)
    {
        List<string> errors = [];
        if (request.Limit is < 1 || request.Limit > maxLimit)
        {
            errors.Add($"'{kLimitKey}': {DescribeLimit(maxLimit)}");
        }
        if (request.Offset < 0)
        {
            errors.Add($"'{kOffsetKey}': {kOffsetDescription}");
        }

        errorResult = errors.Count == 0 ? null : new BadRequestObjectResult($"Invalid paging: {string.Join(" ", errors)}");
        return errors.Count == 0;
    }
}
