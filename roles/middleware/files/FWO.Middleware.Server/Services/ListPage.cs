using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// One page of a list result together with the information whether further items follow.
/// </summary>
/// <typeparam name="T">Type of the listed items.</typeparam>
/// <param name="Items">Items of the page, at most the requested page size.</param>
/// <param name="HasMore">True if at least one further item follows the page.</param>
public sealed record ListPage<T>(List<T> Items, bool HasMore)
{
    /// <summary>
    /// Builds a page from items that were fetched with a limit of one item more than the page size,
    /// so the existence of further items is known without counting the whole result.
    /// </summary>
    /// <param name="fetchedItems">Items fetched with a limit of <paramref name="pageSize"/> + 1.</param>
    /// <param name="pageSize">Requested page size.</param>
    /// <returns>The page, trimmed to the page size.</returns>
    public static ListPage<T> FromLookahead(List<T> fetchedItems, int pageSize)
    {
        return fetchedItems.Count > pageSize
            ? new ListPage<T>(fetchedItems.Take(pageSize).ToList(), true)
            : new ListPage<T>(fetchedItems, false);
    }

    /// <summary>
    /// Cuts one page out of a result that is only available in memory, for example from LDAP.
    /// </summary>
    /// <param name="orderedItems">All items in a deterministic order.</param>
    /// <param name="pageSize">Requested page size.</param>
    /// <param name="offset">Number of items to skip; null skips no item.</param>
    /// <returns>The page and whether further items follow it.</returns>
    public static ListPage<T> FromOrderedItems(IEnumerable<T> orderedItems, int pageSize, int? offset)
    {
        List<T> pageWithLookahead = [.. orderedItems.Skip(offset ?? 0).Take(pageSize + 1)];
        return FromLookahead(pageWithLookahead, pageSize);
    }
}

/// <summary>
/// Shared paging conventions of the list endpoints.
/// </summary>
public static class ListPaging
{
    /// <summary>
    /// Response header telling whether further items follow the returned page (<c>true</c> or <c>false</c>).
    /// </summary>
    public const string kHasMoreHeader = "X-Has-More";

    /// <summary>
    /// Name of the setting holding the maximum and default page size of every REST list endpoint.
    /// </summary>
    public const string kMaxObjectLimitSetting = "maxRestResponseObjectLimit";

    /// <summary>
    /// Describes the <c>limit</c> key shared by every REST list endpoint.
    /// </summary>
    public static readonly string kLimitDescription =
        $"Maximum number of items returned, between 1 and the configured '{kMaxObjectLimitSetting}' " +
        $"(default {GlobalConst.kDefaultMaxRestResponseObjectLimit}); omitted or null returns a page of that size. " +
        $"The response header '{kHasMoreHeader}' tells whether further items follow.";

    /// <summary>
    /// Describes the <c>offset</c> key shared by every REST list endpoint.
    /// </summary>
    public const string kOffsetDescription = "Number of items skipped before the first returned item, at least 0; omitted or null skips no item.";

    /// <summary>
    /// Returns the maximum and default page size of the REST list endpoints as currently configured, see
    /// <see cref="ConfigData.GetUsableMaxRestResponseObjectLimit"/>.
    /// </summary>
    /// <param name="globalConfig">Global configuration holding the setting.</param>
    public static int GetMaxObjectLimit(GlobalConfig globalConfig)
    {
        return ConfigData.GetUsableMaxRestResponseObjectLimit(globalConfig.MaxRestResponseObjectLimit);
    }

    /// <summary>
    /// Returns the page size of a request; an omitted or null limit selects the maximum page size.
    /// </summary>
    /// <param name="limit">Requested page size, already validated against the maximum.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    public static int GetPageSize(int? limit, int maxLimit)
    {
        return limit ?? maxLimit;
    }

    /// <summary>
    /// Checks a requested page size against the maximum page size.
    /// </summary>
    /// <param name="limit">Requested page size; null is valid and selects the maximum.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    public static bool IsValidLimit(int? limit, int maxLimit)
    {
        return limit is null || (limit >= 1 && limit <= maxLimit);
    }

    /// <summary>
    /// Checks a requested offset.
    /// </summary>
    /// <param name="offset">Requested offset; null is valid and skips no item.</param>
    public static bool IsValidOffset(int? offset)
    {
        return offset is null or >= 0;
    }

    /// <summary>
    /// Builds the error message of an invalid <c>limit</c>.
    /// </summary>
    /// <param name="limitPath">Request path of the limit key.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    public static string BuildLimitError(string limitPath, int maxLimit)
    {
        return $"'{limitPath}' must be between 1 and {maxLimit} when supplied. {kLimitDescription}";
    }

    /// <summary>
    /// Builds the error message of an invalid <c>offset</c>.
    /// </summary>
    /// <param name="offsetPath">Request path of the offset key.</param>
    public static string BuildOffsetError(string offsetPath)
    {
        return $"'{offsetPath}' must not be negative when supplied. {kOffsetDescription}";
    }

    /// <summary>
    /// Validates both paging keys and collects every invalid key in the ASP.NET validation problem format.
    /// </summary>
    /// <param name="limit">Requested page size.</param>
    /// <param name="offset">Requested offset.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    /// <param name="limitPath">Request path of the limit key.</param>
    /// <param name="offsetPath">Request path of the offset key.</param>
    /// <returns>The errors keyed by request path; empty when both keys are valid.</returns>
    public static Dictionary<string, string[]> ValidatePaging(int? limit, int? offset, int maxLimit, string limitPath = "limit", string offsetPath = "offset")
    {
        Dictionary<string, string[]> errors = [];
        if (!IsValidLimit(limit, maxLimit))
        {
            AddError(errors, limitPath, BuildLimitError(limitPath, maxLimit));
        }
        if (!IsValidOffset(offset))
        {
            AddError(errors, offsetPath, BuildOffsetError(offsetPath));
        }
        return errors;
    }

    private static void AddError(Dictionary<string, string[]> errors, string path, string error)
    {
        string[] errorValues = new string[1];
        errorValues[0] = error;
        errors[path] = errorValues;
    }

    /// <summary>
    /// Sets the <see cref="kHasMoreHeader"/> response header.
    /// </summary>
    /// <param name="httpContext">Context of the current request; nothing is set without one.</param>
    /// <param name="hasMore">True if further items follow the returned page.</param>
    public static void SetHasMoreHeader(HttpContext? httpContext, bool hasMore)
    {
        if (httpContext != null)
        {
            httpContext.Response.Headers[kHasMoreHeader] = hasMore ? "true" : "false";
        }
    }

    /// <summary>
    /// Cuts one page out of a result that is only available in memory and tells through the
    /// <see cref="kHasMoreHeader"/> response header whether further items follow.
    /// </summary>
    /// <param name="httpContext">Context of the current request; no header is set without one.</param>
    /// <param name="orderedItems">All items in a deterministic order.</param>
    /// <param name="paging">Requested page, already validated.</param>
    /// <param name="maxLimit">Maximum page size, used when no limit was requested.</param>
    /// <returns>The items of the page.</returns>
    public static List<T> TakePage<T>(HttpContext? httpContext, IEnumerable<T> orderedItems, PagingParameters paging, int maxLimit)
    {
        ListPage<T> page = ListPage<T>.FromOrderedItems(orderedItems, GetPageSize(paging.Limit, maxLimit), paging.Offset);
        SetHasMoreHeader(httpContext, page.HasMore);
        return page.Items;
    }

    /// <summary>
    /// Validates the paging keys of a request in the ASP.NET validation problem format.
    /// </summary>
    /// <param name="paging">Requested page.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    /// <param name="errorResult">The bad request result naming every invalid key, or null if both keys are valid.</param>
    /// <returns>True if both paging keys are valid.</returns>
    public static bool TryValidate(PagingParameters paging, int maxLimit, out ActionResult? errorResult)
    {
        Dictionary<string, string[]> errors = ValidatePaging(paging.Limit, paging.Offset, maxLimit);
        errorResult = errors.Count == 0
            ? null
            : new BadRequestObjectResult(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest });
        return errors.Count == 0;
    }

    /// <summary>
    /// Builds the GraphQL paging variables for a page, fetching one item more than the page size to detect further items.
    /// </summary>
    /// <param name="variables">Query variables to extend.</param>
    /// <param name="pageSize">Requested page size.</param>
    /// <param name="offset">Number of items to skip; null skips no item.</param>
    public static void AddLookaheadPagingVariables(Dictionary<string, object> variables, int pageSize, int? offset)
    {
        variables["limit"] = pageSize + 1;
        variables["offset"] = offset ?? 0;
    }
}
