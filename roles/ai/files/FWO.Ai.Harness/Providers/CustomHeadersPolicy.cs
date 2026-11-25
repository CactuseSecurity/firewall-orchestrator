using System.ClientModel.Primitives;

namespace FWO.Ai.Harness.Providers;

/// <summary>
/// Pipeline policy of the OpenAI client that sets the additional headers of the AI settings on every request, e.g. the
/// profile an API gateway in front of the model needs. Added per call, so it runs before the authentication policy, which
/// sets the API key or the access token afterwards; the headers carrying these are refused by the validation anyway.
/// </summary>
internal sealed class CustomHeadersPolicy : PipelinePolicy
{
    private readonly List<KeyValuePair<string, string>> headers;

    /// <summary>
    /// Creates the policy.
    /// </summary>
    /// <param name="headers">Additional headers by name, checked by <see cref="ChatClientFactory.Validate"/></param>
    internal CustomHeadersPolicy(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        this.headers = [.. headers];
    }

    /// <inheritdoc />
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        SetHeaders(message.Request);
        ProcessNext(message, pipeline, currentIndex);
    }

    /// <inheritdoc />
    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        SetHeaders(message.Request);
        return ProcessNextAsync(message, pipeline, currentIndex);
    }

    /// <summary>
    /// Sets the headers on the request, replacing a value set before.
    /// </summary>
    private void SetHeaders(PipelineRequest request)
    {
        foreach (KeyValuePair<string, string> header in headers)
        {
            request.Headers.Set(header.Key, header.Value);
        }
    }
}
