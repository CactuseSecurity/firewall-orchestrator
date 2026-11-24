using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace FWO.Ai.Tests;

/// <summary>
/// Chat client standing in for an AI provider in the harness tests: answers each request with the first matching rule,
/// or with the next scripted response, and records the messages and options of every request.
/// In strict mode it also rejects requests the way the providers do. No AI provider is called.
/// </summary>
internal sealed class ScriptedChatClient : IChatClient
{
    private const string kRejectionPrefix = "The provider rejected the request: ";

    private readonly object recordLock = new();
    private readonly Queue<Func<List<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>>> script = new();
    private readonly List<(Func<List<ChatMessage>, ChatOptions?, bool> Matches, Func<List<ChatMessage>, ChatResponse> Respond)> rules = [];
    private readonly List<List<ChatMessage>> receivedMessages = [];
    private readonly List<ChatOptions?> receivedOptions = [];
    private readonly List<string> rejections = [];
    private int disposeCount;

    /// <summary>
    /// Answer used once the script is exhausted; if null, such a request fails.
    /// </summary>
    internal Func<List<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>>? Fallback { get; set; }

    /// <summary>
    /// Emulates the checks of the AI providers: a request breaking one of the <see cref="ProviderHistoryRules"/> fails
    /// with an <see cref="InvalidOperationException"/>, and the broken rule is recorded in <see cref="Rejections"/>.
    /// </summary>
    internal bool Strict { get; init; }

    /// <summary>
    /// Number of times the client was disposed.
    /// </summary>
    internal int DisposeCount => Volatile.Read(ref disposeCount);

    /// <summary>
    /// The rules broken by rejected requests, in the order the requests were received.
    /// </summary>
    internal List<string> Rejections
    {
        get
        {
            lock (recordLock)
            {
                return [.. rejections];
            }
        }
    }

    /// <summary>
    /// Copies of the messages of every request, in the order the requests were received.
    /// </summary>
    internal List<List<ChatMessage>> ReceivedMessages
    {
        get
        {
            lock (recordLock)
            {
                return receivedMessages.Select(messages => new List<ChatMessage>(messages)).ToList();
            }
        }
    }

    /// <summary>
    /// Copies of the options of every request, in the order the requests were received.
    /// </summary>
    internal List<ChatOptions?> ReceivedOptions
    {
        get
        {
            lock (recordLock)
            {
                return [.. receivedOptions];
            }
        }
    }

    /// <summary>
    /// Adds an assistant text answer to the script.
    /// </summary>
    internal ScriptedChatClient RespondWithText(string text)
    {
        return Respond((_, _, _) => Task.FromResult(CreateTextResponse(text)));
    }

    /// <summary>
    /// Adds an assistant message calling the given tool with the given arguments (none if null) to the script.
    /// </summary>
    internal ScriptedChatClient RespondWithFunctionCall(string callId, string toolName, IDictionary<string, object?>? arguments = null)
    {
        return Respond((_, _, _) => Task.FromResult(CreateFunctionCallResponse(callId, toolName, arguments)));
    }

    /// <summary>
    /// Adds an arbitrary answer to the script, e.g. one that throws or waits.
    /// </summary>
    internal ScriptedChatClient Respond(Func<List<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> answer)
    {
        lock (recordLock)
        {
            script.Enqueue(answer);
        }
        return this;
    }

    /// <summary>
    /// Answers every request matching the condition with the given answer, before the script is used.
    /// </summary>
    internal ScriptedChatClient RespondWhen(Func<List<ChatMessage>, ChatOptions?, bool> matches, Func<List<ChatMessage>, ChatResponse> respond)
    {
        lock (recordLock)
        {
            rules.Add((matches, respond));
        }
        return this;
    }

    /// <summary>
    /// Creates a response with an assistant text message.
    /// </summary>
    internal static ChatResponse CreateTextResponse(string text)
    {
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    /// <summary>
    /// Creates a response with an assistant message calling the given tool with the given arguments (none if null).
    /// </summary>
    internal static ChatResponse CreateFunctionCallResponse(string callId, string toolName, IDictionary<string, object?>? arguments = null)
    {
        List<AIContent> contents = [new FunctionCallContent(callId, toolName, arguments)];
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents));
    }

    /// <summary>
    /// Records the request and answers it with the first matching rule, the next scripted answer or the fallback.
    /// In strict mode a request breaking a provider rule fails instead.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no answer left for the request, or the strict mode rejects it.</exception>
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> requestMessages = messages.ToList();
        Func<List<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> answer;
        lock (recordLock)
        {
            receivedMessages.Add(requestMessages);
            receivedOptions.Add(options?.Clone());
            string? violation = Strict ? ProviderHistoryRules.FindViolation(requestMessages) : null;
            if (violation != null)
            {
                rejections.Add(violation);
                return Task.FromException<ChatResponse>(new InvalidOperationException(kRejectionPrefix + violation));
            }
            answer = SelectAnswer(requestMessages, options);
        }
        return answer([.. requestMessages], options, cancellationToken);
    }

    /// <summary>
    /// Answers like <see cref="GetResponseAsync"/> and returns the answer as updates.
    /// </summary>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    /// <summary>
    /// Returns the client itself if asked for its type, nothing otherwise.
    /// </summary>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceKey == null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    /// <summary>
    /// Counts the disposal.
    /// </summary>
    public void Dispose()
    {
        Interlocked.Increment(ref disposeCount);
    }

    /// <summary>
    /// Selects the answer of a request; to be called holding the record lock.
    /// </summary>
    private Func<List<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> SelectAnswer(List<ChatMessage> requestMessages, ChatOptions? options)
    {
        foreach ((Func<List<ChatMessage>, ChatOptions?, bool> matches, Func<List<ChatMessage>, ChatResponse> respond) in rules)
        {
            if (matches(requestMessages, options))
            {
                return (ruleMessages, _, _) => Task.FromResult(respond(ruleMessages));
            }
        }
        if (script.Count > 0)
        {
            return script.Dequeue();
        }
        return Fallback ?? ((_, _, _) => Task.FromException<ChatResponse>(new InvalidOperationException("The script has no answer left.")));
    }
}
