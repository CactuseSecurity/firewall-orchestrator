using AGUI.Abstractions;
using AGUI.Client;
using AGUI.Formatting;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace FWO.Ai.Client.AgUi;

/// <summary>
/// AG-UI transport of the FWO agent chat client, in place of the HTTP transport of the AG-UI SDK: it sends each run
/// with the JWT of the user, maps a refused run to an <see cref="AgentClientException"/> (keeping the Retry-After of the
/// harness), bounds the size of the response, and decodes the server-sent events with the formatter of the SDK.
/// </summary>
internal sealed class FwoAgUiTransport : IAGUITransport
{
    private const string kBearerScheme = "Bearer";
    private const string kJsonMediaType = "application/json";

    private readonly HttpClient httpClient;
    private readonly Uri endpoint;
    private readonly Func<CancellationToken, Task<string?>> accessTokenProvider;
    private readonly long maxResponseBytes;
    private readonly SseEventStreamFormatter formatter = new();

    /// <summary>
    /// Creates the transport.
    /// </summary>
    /// <param name="httpClient">HTTP client to send the runs with, owned by the caller</param>
    /// <param name="endpoint">AG-UI endpoint of the harness, checked by <see cref="AgUiEndpoint"/></param>
    /// <param name="accessTokenProvider">Returns the JWT of the user for each run; null if the user has none</param>
    /// <param name="maxResponseBytes">Maximum size of the response of a run</param>
    internal FwoAgUiTransport(HttpClient httpClient, Uri endpoint, Func<CancellationToken, Task<string?>> accessTokenProvider,
        long maxResponseBytes)
    {
        this.httpClient = httpClient;
        this.endpoint = endpoint;
        this.accessTokenProvider = accessTokenProvider;
        this.maxResponseBytes = maxResponseBytes;
    }

    /// <summary>
    /// Posts the run and returns its events as they arrive, up to the end of the run (RUN_FINISHED or RUN_ERROR). The
    /// rest of the response is not read, so that nothing the harness sends after the end can change the result or keep
    /// the run waiting; the harness keeps no state of the run that would need the response to be read to its end.
    /// </summary>
    /// <exception cref="AgentClientException">The token is missing or unusable, the harness refused the run or could not
    /// be reached, or the response is no valid AG-UI event stream, exceeds the limit or ends before the run finished.</exception>
    public async IAsyncEnumerable<BaseEvent> SendAsync(RunAgentInput input, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        string accessToken = await GetAccessTokenAsync(cancellationToken);
        using HttpRequestMessage request = CreateRequest(input, accessToken);
        using HttpResponseMessage response = await SendRequestAsync(request, cancellationToken);
        AgUiFailures.ThrowIfRefused(response, DateTimeOffset.UtcNow);
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using BoundedReadStream boundedBody = new(body, maxResponseBytes);
        await using IAsyncEnumerator<BaseEvent> events = formatter.ReadAsync(boundedBody, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (await MoveNextAsync(events))
        {
            BaseEvent aguiEvent = events.Current;
            yield return aguiEvent;
            if (aguiEvent is RunFinishedEvent or RunErrorEvent)
            {
                yield break;
            }
        }
        throw AgUiFailures.Protocol("The event stream of the AI harness ended before the run finished.");
    }

    /// <summary>
    /// Moves to the next event of the response, mapping a malformed or broken stream to a protocol error.
    /// </summary>
    /// <param name="events">Events of the response</param>
    /// <returns>Whether there is a next event</returns>
    /// <exception cref="AgentClientException">The stream is no valid AG-UI event stream, or broke off before the run finished.</exception>
    internal static async Task<bool> MoveNextAsync(IAsyncEnumerator<BaseEvent> events)
    {
        try
        {
            return await events.MoveNextAsync();
        }
        catch (JsonException exception)
        {
            throw AgUiFailures.Protocol("The AI harness sent an event that is no valid AG-UI event.", exception);
        }
        catch (InvalidOperationException exception) when (exception is not ObjectDisposedException)
        {
            // the formatter of the SDK reports an event of the JSON value null this way
            throw AgUiFailures.Protocol("The AI harness sent an event that is no valid AG-UI event.", exception);
        }
        catch (IOException exception)
        {
            throw AgUiFailures.Protocol("The connection to the AI harness broke off before the run finished.", exception);
        }
    }

    /// <summary>
    /// Returns the access token of the user. The token is never part of an error message.
    /// </summary>
    /// <exception cref="AgentClientException">The user has no token, or it contains white space or control characters.</exception>
    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        string? accessToken = await accessTokenProvider(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new AgentClientException(AgentClientFailure.Unauthorized, "There is no access token of the user to send the run with.");
        }
        if (accessToken.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new AgentClientException(AgentClientFailure.Unauthorized, "The access token contains white space or control characters.");
        }
        return accessToken;
    }

    /// <summary>
    /// Creates the request of a run: the AG-UI input as JSON (with a known length, as the request passes the Apache of
    /// the middleware), the access token as bearer token, and server-sent events as accepted answer.
    /// </summary>
    private HttpRequestMessage CreateRequest(RunAgentInput input, string accessToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(input, AGUIJsonSerializerContext.Default.RunAgentInput);
        ByteArrayContent content = new(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(kJsonMediaType);
        HttpRequestMessage request = new(HttpMethod.Post, endpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue(kBearerScheme, accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(SseEventStreamFormatter.ServerSentEventsMediaType));
        return request;
    }

    /// <summary>
    /// Sends the request, returning as soon as the response headers arrived.
    /// </summary>
    /// <exception cref="AgentClientException">The harness could not be reached.</exception>
    private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AgentClientException(AgentClientFailure.Unavailable, "The AI harness could not be reached.", innerException: exception);
        }
    }
}
