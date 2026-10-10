using Microsoft.AspNetCore.Http;

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
    /// Builds the GraphQL paging variables for a page, fetching one item more than the page size to detect further items.
    /// </summary>
    /// <param name="variables">Query variables to extend.</param>
    /// <param name="pageSize">Requested page size.</param>
    /// <param name="offset">Number of items to skip.</param>
    public static void AddLookaheadPagingVariables(Dictionary<string, object> variables, int pageSize, int offset)
    {
        variables["limit"] = pageSize + 1;
        variables["offset"] = offset;
    }
}
