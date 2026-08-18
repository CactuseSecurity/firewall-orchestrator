namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Defines the standard request contract for middleware POST endpoints with an options object.
/// </summary>
/// <typeparam name="TOptions">The endpoint-specific options type.</typeparam>
public interface IRequestWithOptions<TOptions> : IRequestWithRootAdditionalData
    where TOptions : RequestOptionsDto
{
    /// <summary>
    /// Gets or sets the optional request options.
    /// </summary>
    TOptions? Options { get; set; }
}
