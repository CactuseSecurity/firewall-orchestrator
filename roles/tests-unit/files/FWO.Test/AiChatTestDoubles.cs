using System.Security.Claims;
using System.Text.Json;
using AGUI.Abstractions;
using FWO.Ai.Client;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data.Ai;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace FWO.Test;

/// <summary>
/// Authentication state of a UI session in the tests of the AI chat, whose user can be changed.
/// </summary>
internal sealed class AiChatAuthenticationStateProvider : AuthenticationStateProvider
{
    private AuthenticationState state;

    /// <summary>
    /// Creates the state with the given user; no user if the distinguished name is empty.
    /// </summary>
    internal AiChatAuthenticationStateProvider(string userDn)
    {
        state = CreateState(userDn);
    }

    /// <inheritdoc/>
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(state);
    }

    /// <summary>
    /// Logs in another user, or logs out for an empty distinguished name, and notifies the subscribers.
    /// </summary>
    internal void ChangeUser(string userDn)
    {
        SetUserSilently(userDn);
        NotifyAuthenticationStateChanged(Task.FromResult(state));
    }

    /// <summary>
    /// Changes the user without notifying the subscribers.
    /// </summary>
    internal void SetUserSilently(string userDn)
    {
        state = CreateState(userDn);
    }

    private static AuthenticationState CreateState(string userDn)
    {
        if (string.IsNullOrEmpty(userDn))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
        List<Claim> claims = new() { new Claim(AiChatSession.kUserDnClaim, userDn) };
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
    }
}

/// <summary>
/// User config of the tests of the AI chat: texts are their keys, the chat is offered, and the change event can be raised.
/// </summary>
internal sealed class AiChatUserConfig : SimulatedUserConfig
{
    private static readonly ConfigItem[] kNoChangedItems = [];

    /// <summary>
    /// Creates the config with the chat switched on and an AI provider configured.
    /// </summary>
    internal AiChatUserConfig()
    {
        AiChatEnabled = true;
        AiProvider = AiProvider.OpenAi;
    }

    /// <summary>
    /// Raises the change event, as after a change of the global settings.
    /// </summary>
    internal void RaiseChange()
    {
        InvokeOnChange(this, kNoChangedItems);
    }
}

/// <summary>
/// The AI chat session of a test with the chat client factory, token source and authentication state it uses.
/// </summary>
internal sealed class AiChatTestSession : IDisposable
{
    internal const string kAccessToken = "user.access.token";
    internal const string kUserDn = "uid=tester,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal";

    /// <summary>
    /// Creates the session; every run is answered by <see cref="ChatClient"/> as it is when the run starts.
    /// </summary>
    internal AiChatTestSession(ScriptedChatClient chatClient)
    {
        ChatClient = chatClient;
        IFwoAgentChatClientFactory factory = Substitute.For<IFwoAgentChatClientFactory>();
        factory.CreateChatClient(Arg.Any<Func<CancellationToken, Task<string?>>>()).Returns(callInfo =>
        {
            TokenProvider = callInfo.Arg<Func<CancellationToken, Task<string?>>>();
            return ChatClient;
        });
        IAccessTokenSource accessTokenSource = Substitute.For<IAccessTokenSource>();
        accessTokenSource.GetAccessToken().Returns(kAccessToken);
        Authentication = new AiChatAuthenticationStateProvider(kUserDn);
        Session = new AiChatSession(factory, accessTokenSource, Authentication);
    }

    /// <summary>Answers the runs; may be replaced between runs.</summary>
    internal ScriptedChatClient ChatClient { get; set; }

    /// <summary>The token provider the session gave to the last chat client.</summary>
    internal Func<CancellationToken, Task<string?>>? TokenProvider { get; private set; }

    /// <summary>Authentication state of the session.</summary>
    internal AiChatAuthenticationStateProvider Authentication { get; }

    /// <summary>The session under test.</summary>
    internal AiChatSession Session { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Session.Dispose();
    }
}

/// <summary>
/// Updates of the runs of the AI chat as the FWO agent chat client returns them.
/// </summary>
internal static class AiChatTestUpdates
{
    private static readonly JsonSerializerOptions kJsonOptions = CreateJsonOptions();

    /// <summary>
    /// Returns the RUN_STARTED update naming the thread.
    /// </summary>
    internal static ChatResponseUpdate RunStarted(string threadId)
    {
        return new ChatResponseUpdate { RawRepresentation = new RunStartedEvent { ThreadId = threadId, RunId = "run-1" } };
    }

    /// <summary>
    /// Returns the MESSAGES_SNAPSHOT update holding the given conversation.
    /// </summary>
    internal static ChatResponseUpdate Snapshot(List<ChatMessage> conversation)
    {
        MessagesSnapshotEvent snapshot = new() { Messages = conversation.AsAGUIMessages(kJsonOptions).ToList() };
        return new ChatResponseUpdate { RawRepresentation = snapshot };
    }

    /// <summary>
    /// Returns an update asking the user to approve a call of the given tool.
    /// </summary>
    internal static ChatResponseUpdate ApprovalRequest(string requestId, string toolName, IDictionary<string, object?>? arguments = null)
    {
        FunctionCallContent call = new($"call-{requestId}", toolName, arguments);
        List<AIContent> contents = new() { new ToolApprovalRequestContent(requestId, call) };
        return new ChatResponseUpdate(ChatRole.Assistant, contents);
    }

    /// <summary>
    /// Returns the RUN_ERROR update of a failed run.
    /// </summary>
    internal static ChatResponseUpdate RunError(string message)
    {
        List<AIContent> contents = new() { new ErrorContent(message) { ErrorCode = "run_error" } };
        return new ChatResponseUpdate(ChatRole.Assistant, contents);
    }

    /// <summary>
    /// Returns a conversation of user and assistant texts, alternating, starting with the user.
    /// </summary>
    internal static List<ChatMessage> Conversation(params string[] texts)
    {
        return texts.Select((text, index) => new ChatMessage(index % 2 == 0 ? ChatRole.User : ChatRole.Assistant, text)).ToList();
    }

    /// <summary>
    /// Returns the texts of the messages.
    /// </summary>
    internal static List<string> GetTexts(IEnumerable<ChatMessage> messages)
    {
        return messages.Select(message => message.Text).ToList();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(AIJsonUtilities.DefaultOptions);
        options.TypeInfoResolverChain.Insert(0, AGUIJsonUtilities.DefaultTypeInfoResolver);
        return options;
    }
}
