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
/// Key descriptions and validation of the paging keys of the flow catalog list requests. The maximum page size is the
/// configured <see cref="ListPaging.kMaxObjectLimitSetting"/>, see <see cref="ListPaging.GetMaxObjectLimit"/>.
/// </summary>
public static class FlowCatalogPaging
{
    private const string kLimitKey = "limit";
    private const string kOffsetKey = "offset";

    /// <summary>
    /// Describes the 'limit' key.
    /// </summary>
    public static readonly string kLimitDescription =
        $"{ListPaging.kLimitDescription} Items are ordered by name and id, so 'limit' and 'offset' page the result deterministically.";

    /// <summary>
    /// Returns the root key definitions of the paging keys.
    /// </summary>
    public static List<RequestKeyDefinition> KeyDefinitions() =>
    [
        new RequestKeyDefinition(kLimitKey, kLimitDescription),
        new RequestKeyDefinition(kOffsetKey, ListPaging.kOffsetDescription)
    ];

    /// <summary>
    /// Returns the page size of a request; an omitted or null limit selects the maximum page size.
    /// </summary>
    /// <param name="request">Request carrying the paging keys.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    public static int GetPageSize(IPagedListRequest request, int maxLimit)
    {
        return ListPaging.GetPageSize(request.Limit, maxLimit);
    }

    /// <summary>
    /// Validates both paging keys and reports every invalid key together.
    /// </summary>
    /// <param name="request">Request carrying the paging keys.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    /// <param name="errorResult">The bad request result naming every invalid key, or null if both are valid.</param>
    /// <returns>True if both paging keys are valid.</returns>
    public static bool TryValidate(IPagedListRequest request, int maxLimit, out ActionResult? errorResult)
    {
        List<string> errors = [];
        if (!ListPaging.IsValidLimit(request.Limit, maxLimit))
        {
            errors.Add(ListPaging.BuildLimitError(kLimitKey, maxLimit));
        }
        if (!ListPaging.IsValidOffset(request.Offset))
        {
            errors.Add(ListPaging.BuildOffsetError(kOffsetKey));
        }

        errorResult = errors.Count == 0 ? null : new BadRequestObjectResult($"Invalid paging: {string.Join(" ", errors)}");
        return errors.Count == 0;
    }
}
