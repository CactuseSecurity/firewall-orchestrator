using AGUI.Abstractions;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Ai.Client;

/// <summary>
/// An MCP prompt of the FWO MCP server, sent as the user's turn in the forwarded properties of the AG-UI run
/// (<c>forwardedProps.prompt</c>). The harness gets the prompt with the user's JWT and appends its messages to the
/// conversation.
/// </summary>
public sealed class AgentPromptRequest
{
    /// <summary>Forwarded property naming the prompt. Keep in step with PromptRequest of the harness.</summary>
    internal const string kPromptProperty = "prompt";

    /// <summary>Property of the prompt holding its name.</summary>
    internal const string kPromptNameProperty = "name";

    /// <summary>Property of the prompt holding its arguments.</summary>
    internal const string kPromptArgumentsProperty = "arguments";

    /// <summary>
    /// Creates a prompt request.
    /// </summary>
    /// <param name="name">Name of the MCP prompt</param>
    /// <param name="arguments">Arguments of the prompt</param>
    public AgentPromptRequest(string name, IReadOnlyDictionary<string, string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(arguments);
        Name = name;
        Arguments = arguments;
    }

    /// <summary>Name of the MCP prompt.</summary>
    public string Name { get; }

    /// <summary>Arguments of the prompt.</summary>
    public IReadOnlyDictionary<string, string> Arguments { get; }

    /// <summary>
    /// Returns the chat options of one run sending this prompt as the user's turn. Send them with the conversation so
    /// far, without a new user message. The options are for this run only: options used again would send the prompt
    /// again, so the next run gets options of its own, e.g. <c>new ChatOptions { ConversationId = threadId }</c>.
    /// </summary>
    /// <param name="threadId">Thread (AG-UI threadId) of the conversation; a new thread if null or blank</param>
    public ChatOptions ToChatOptions(string? threadId = null)
    {
        JsonElement forwardedProperties = CreateForwardedProperties();
        return new ChatOptions
        {
            ConversationId = string.IsNullOrWhiteSpace(threadId) ? null : threadId,
            // the AG-UI chat client takes over the forwarded properties of the input this factory returns
            RawRepresentationFactory = _ => new RunAgentInput { ForwardedProperties = forwardedProperties }
        };
    }

    /// <summary>
    /// Returns the forwarded properties naming the prompt: <c>{ "prompt": { "name": ..., "arguments": { ... } } }</c>.
    /// </summary>
    internal JsonElement CreateForwardedProperties()
    {
        JsonObject arguments = [];
        foreach (KeyValuePair<string, string> argument in Arguments)
        {
            arguments[argument.Key] = argument.Value;
        }
        JsonObject prompt = new() { [kPromptNameProperty] = Name, [kPromptArgumentsProperty] = arguments };
        JsonObject forwardedProperties = new() { [kPromptProperty] = prompt };
        return JsonSerializer.SerializeToElement(forwardedProperties, AGUIJsonSerializerContext.Default.JsonObject);
    }
}
