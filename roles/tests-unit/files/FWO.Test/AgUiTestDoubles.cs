using AGUI.Abstractions;
using Microsoft.Extensions.AI;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace FWO.Test;

/// <summary>
/// A call the <see cref="ScriptedChatClient"/> received.
/// </summary>
/// <param name="Messages">Messages of the call</param>
/// <param name="Options">Chat options of the call</param>
/// <param name="CancellationToken">Cancellation token of the call</param>
internal sealed record ScriptedChatCall(List<ChatMessage> Messages, ChatOptions? Options, CancellationToken CancellationToken);

/// <summary>
/// Chat client standing in for the FWO agent chat client: records the calls and answers each with the next scripted
/// response, the last one being used again.
/// </summary>
internal sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>>> responses;
    private Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> lastResponse;

    /// <summary>
    /// Creates a client answering the calls with the given responses in turn.
    /// </summary>
    internal ScriptedChatClient(params Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>>[] responses)
    {
        this.responses = new Queue<Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>>>(responses);
        lastResponse = responses.Last();
    }

    /// <summary>Calls received, in order.</summary>
    internal List<ScriptedChatCall> Calls { get; } = [];

    /// <summary>
    /// Creates a client answering every call with the given updates.
    /// </summary>
    internal static ScriptedChatClient Returning(params ChatResponseUpdate[] updates)
    {
        return new ScriptedChatClient(Updates(updates));
    }

    /// <summary>
    /// Creates a client answering every call with the given exception.
    /// </summary>
    internal static ScriptedChatClient Throwing(Exception exception)
    {
        return new ScriptedChatClient(Failing(exception));
    }

    /// <summary>
    /// Creates a client answering with the updates written to the channel, ending when the channel is completed.
    /// </summary>
    internal static ScriptedChatClient Streaming(Channel<ChatResponseUpdate> channel)
    {
        return new ScriptedChatClient(cancellationToken => channel.Reader.ReadAllAsync(cancellationToken));
    }

    /// <summary>
    /// Creates a client whose calls wait until they are cancelled.
    /// </summary>
    internal static ScriptedChatClient WaitingForCancellation()
    {
        return new ScriptedChatClient(WaitForCancellationAsync);
    }

    /// <summary>
    /// Returns a response of the given updates.
    /// </summary>
    internal static Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> Updates(params ChatResponseUpdate[] updates)
    {
        return _ => updates.ToAsyncEnumerable();
    }

    /// <summary>
    /// Returns a response failing with the given exception.
    /// </summary>
    internal static Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> Failing(Exception exception)
    {
        return _ => FailAsync(exception);
    }

    /// <summary>
    /// Creates an update of the assistant with the given text.
    /// </summary>
    internal static ChatResponseUpdate Text(string text, string messageId = "message-1")
    {
        return new ChatResponseUpdate(ChatRole.Assistant, text) { MessageId = messageId };
    }

    /// <summary>
    /// Returns the arguments of the MCP prompt the chat options of a run name in the forwarded properties.
    /// </summary>
    internal static Dictionary<string, string> GetPromptArguments(ChatOptions? options)
    {
        JsonElement prompt = GetPrompt(options);
        return prompt.GetProperty("arguments").EnumerateObject().ToDictionary(argument => argument.Name, argument => argument.Value.GetString() ?? "");
    }

    /// <summary>
    /// Returns the name of the MCP prompt the chat options of a run name in the forwarded properties.
    /// </summary>
    internal static string? GetPromptName(ChatOptions? options)
    {
        return GetPrompt(options).GetProperty("name").GetString();
    }

    /// <inheritdoc/>
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        return GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(new ScriptedChatCall(messages.ToList(), options, cancellationToken));
        if (responses.TryDequeue(out Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>>? response))
        {
            lastResponse = response;
        }
        return lastResponse(cancellationToken);
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // nothing to dispose
    }

    /// <summary>
    /// Returns the prompt of the forwarded properties of the AG-UI input the chat options create.
    /// </summary>
    private static JsonElement GetPrompt(ChatOptions? options)
    {
        RunAgentInput? input = options?.RawRepresentationFactory?.Invoke(null!) as RunAgentInput;
        JsonElement forwardedProperties = input?.ForwardedProperties ?? throw new InvalidOperationException("The options name no prompt.");
        return forwardedProperties.GetProperty("prompt");
    }

    /// <summary>
    /// Fails with the given exception before the first update.
    /// </summary>
    private static async IAsyncEnumerable<ChatResponseUpdate> FailAsync(Exception exception)
    {
        await Task.FromException(exception);
        yield break;
    }

    /// <summary>
    /// Waits until the call is cancelled.
    /// </summary>
    private static async IAsyncEnumerable<ChatResponseUpdate> WaitForCancellationAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        yield break;
    }
}

/// <summary>
/// A request the <see cref="AgUiFakeHandler"/> received, read while it was sent.
/// </summary>
/// <param name="Method">HTTP method</param>
/// <param name="RequestUri">Uri the request was sent to</param>
/// <param name="Authorization">Authorization header</param>
/// <param name="Accept">Accepted media types</param>
/// <param name="ContentType">Media type of the body</param>
/// <param name="Body">Body of the request</param>
internal sealed record AgUiRecordedRequest(HttpMethod Method, Uri? RequestUri, AuthenticationHeaderValue? Authorization,
    List<string> Accept, string? ContentType, string Body);

/// <summary>
/// HTTP handler standing in for the AG-UI endpoint of the harness: records the requests and answers them as scripted.
/// </summary>
internal sealed class AgUiFakeHandler : HttpMessageHandler
{
    internal const string kEventStreamMediaType = "text/event-stream";

    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;

    /// <summary>
    /// Creates a handler answering every request with the given function.
    /// </summary>
    internal AgUiFakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        this.respond = respond;
    }

    /// <summary>Requests received, in order.</summary>
    internal List<AgUiRecordedRequest> Requests { get; } = [];

    /// <summary>Number of times the handler was disposed.</summary>
    internal int DisposeCount { get; private set; }

    /// <summary>
    /// Creates a handler answering with the given server-sent events body.
    /// </summary>
    internal static AgUiFakeHandler WithEventStream(string eventStream)
    {
        return new AgUiFakeHandler((_, _) => Task.FromResult(CreateEventStreamResponse(new MemoryStream(Encoding.UTF8.GetBytes(eventStream)))));
    }

    /// <summary>
    /// Creates a handler answering with the given response body stream as server-sent events.
    /// </summary>
    internal static AgUiFakeHandler WithEventStream(Stream eventStream)
    {
        return new AgUiFakeHandler((_, _) => Task.FromResult(CreateEventStreamResponse(eventStream)));
    }

    /// <summary>
    /// Creates a handler answering with the given status, adapted by the given action.
    /// </summary>
    internal static AgUiFakeHandler WithStatus(HttpStatusCode statusCode, Action<HttpResponseMessage>? adapt = null)
    {
        return new AgUiFakeHandler((_, _) =>
        {
            HttpResponseMessage response = new(statusCode);
            adapt?.Invoke(response);
            return Task.FromResult(response);
        });
    }

    /// <summary>
    /// Creates a handler throwing the given exception.
    /// </summary>
    internal static AgUiFakeHandler Throwing(Exception exception)
    {
        return new AgUiFakeHandler((_, _) => Task.FromException<HttpResponseMessage>(exception));
    }

    /// <summary>
    /// Creates a handler that never answers until the request is cancelled.
    /// </summary>
    internal static AgUiFakeHandler Hanging()
    {
        return new AgUiFakeHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
    }

    /// <summary>
    /// Creates a response streaming the given body as server-sent events.
    /// </summary>
    internal static HttpResponseMessage CreateEventStreamResponse(Stream eventStream)
    {
        StreamContent content = new(eventStream);
        content.Headers.ContentType = new MediaTypeHeaderValue(kEventStreamMediaType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        List<string> accept = request.Headers.Accept.Select(mediaType => mediaType.MediaType ?? "").ToList();
        Requests.Add(new AgUiRecordedRequest(request.Method, request.RequestUri, request.Headers.Authorization, accept,
            request.Content?.Headers.ContentType?.MediaType, body));
        return await respond(request, cancellationToken);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        DisposeCount++;
        base.Dispose(disposing);
    }
}

/// <summary>
/// Read-only stream returning the given chunks one per read, at most as many bytes as the reader asks for, and then
/// throwing the given exception or ending. Splits server-sent events at any byte.
/// </summary>
internal sealed class AgUiChunkedStream : Stream
{
    private readonly Queue<byte[]> chunks;
    private readonly Exception? finalException;
    private byte[] currentChunk = Array.Empty<byte>();
    private int currentOffset;

    /// <summary>
    /// Creates a stream of the given chunks.
    /// </summary>
    /// <param name="chunks">Chunks to return</param>
    /// <param name="finalException">Thrown after the last chunk instead of ending; none if null</param>
    internal AgUiChunkedStream(IEnumerable<byte[]> chunks, Exception? finalException = null)
    {
        this.chunks = new Queue<byte[]>(chunks);
        this.finalException = finalException;
    }

    /// <summary>
    /// Creates a stream returning the UTF-8 bytes of the text in chunks of the given size.
    /// </summary>
    internal static AgUiChunkedStream Split(string text, int chunkSize, Exception? finalException = null)
    {
        return new AgUiChunkedStream(Encoding.UTF8.GetBytes(text).Chunk(chunkSize), finalException);
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        if (currentOffset == currentChunk.Length)
        {
            if (!chunks.TryDequeue(out byte[]? nextChunk))
            {
                return finalException == null ? 0 : throw finalException;
            }
            currentChunk = nextChunk;
            currentOffset = 0;
        }
        int length = Math.Min(buffer.Length, currentChunk.Length - currentOffset);
        currentChunk.AsSpan(currentOffset, length).CopyTo(buffer);
        currentOffset += length;
        return length;
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        // nothing to flush on a read-only stream
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}
