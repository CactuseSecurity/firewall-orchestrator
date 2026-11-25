using Microsoft.Extensions.AI;

namespace FWO.Ai.Tests;

/// <summary>
/// Answer of the <see cref="ScriptedChatClient"/> that waits until it is released or the request is cancelled,
/// so that a test can act while a turn is running without depending on timing.
/// </summary>
internal sealed class BlockingAnswer
{
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<string> released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes once the request has reached the chat client.
    /// </summary>
    internal Task Started => started.Task;

    /// <summary>
    /// Lets the request complete with the given answer.
    /// </summary>
    internal void Release(string answer)
    {
        released.TrySetResult(answer);
    }

    /// <summary>
    /// Waits for the release or the cancellation of the request and answers with the released text.
    /// </summary>
    internal async Task<ChatResponse> AnswerAsync(List<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
    {
        _ = messages;
        _ = options;
        started.TrySetResult();
        string answer = await released.Task.WaitAsync(cancellationToken);
        return ScriptedChatClient.CreateTextResponse(answer);
    }
}
