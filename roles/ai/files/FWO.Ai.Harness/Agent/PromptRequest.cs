using System.Text.Json;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// An MCP prompt a run asks for in the forwarded properties of its AG-UI input, e.g.
/// <c>"forwardedProps": { "prompt": { "name": "check_access_request_reason", "arguments": { "reason": "..." } } }</c>.
/// The harness gets the prompt from the MCP server with the user's JWT and sends its messages as the user's turn.
/// The MCP server decides which prompts the user may get and checks the arguments; the prompts it offers are
/// listed by the MCP server itself, not by the harness.
/// </summary>
/// <param name="Name">Name of the prompt</param>
/// <param name="Arguments">Arguments of the prompt by argument name; all values are strings</param>
internal sealed record PromptRequest(string Name, Dictionary<string, object?> Arguments)
{
    /// <summary>Forwarded property naming the prompt.</summary>
    internal const string kPromptProperty = "prompt";

    /// <summary>Property of the prompt holding its name.</summary>
    internal const string kNameProperty = "name";

    /// <summary>Property of the prompt holding its arguments.</summary>
    internal const string kArgumentsProperty = "arguments";

    /// <summary>Maximum length of a prompt name.</summary>
    internal const int kMaxNameLength = 128;

    /// <summary>Maximum number of prompt arguments.</summary>
    internal const int kMaxArguments = 16;

    /// <summary>
    /// Reads the prompt from the forwarded properties of an AG-UI input.
    /// </summary>
    /// <param name="forwardedProperties">Forwarded properties of the AG-UI input; none if null</param>
    /// <returns>The requested prompt, or null if the forwarded properties name none</returns>
    /// <exception cref="ArgumentException">The prompt is malformed or exceeds a limit.</exception>
    internal static PromptRequest? FromForwardedProperties(JsonElement? forwardedProperties)
    {
        if (forwardedProperties is not { ValueKind: JsonValueKind.Object } properties
            || !properties.TryGetProperty(kPromptProperty, out JsonElement prompt))
        {
            return null;
        }
        if (prompt.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The prompt must be an object with a name and optional arguments.", nameof(forwardedProperties));
        }
        string name = ReadName(prompt);
        Dictionary<string, object?> arguments = prompt.TryGetProperty(kArgumentsProperty, out JsonElement argumentsElement)
            ? ReadArguments(argumentsElement)
            : [];
        return new PromptRequest(name, arguments);
    }

    /// <summary>
    /// Reads the name of the prompt.
    /// </summary>
    /// <exception cref="ArgumentException">The name is missing, blank, not a string or too long.</exception>
    private static string ReadName(JsonElement prompt)
    {
        bool hasName = prompt.TryGetProperty(kNameProperty, out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.String;
        string name = hasName ? nameElement.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The prompt needs a name.", nameof(prompt));
        }
        if (name.Length > kMaxNameLength)
        {
            throw new ArgumentException($"The prompt name must not be longer than {kMaxNameLength} characters.", nameof(prompt));
        }
        return name;
    }

    /// <summary>
    /// Reads the arguments of the prompt; MCP prompt arguments are strings.
    /// </summary>
    /// <exception cref="ArgumentException">The arguments are no object, too many or not strings.</exception>
    private static Dictionary<string, object?> ReadArguments(JsonElement argumentsElement)
    {
        if (argumentsElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The prompt arguments must be an object.", nameof(argumentsElement));
        }
        Dictionary<string, object?> arguments = [];
        foreach (JsonProperty argument in argumentsElement.EnumerateObject())
        {
            if (arguments.Count == kMaxArguments)
            {
                throw new ArgumentException($"A prompt takes at most {kMaxArguments} arguments.", nameof(argumentsElement));
            }
            // the argument name is not part of the messages, as it is chosen by the client
            if (argument.Value.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Every prompt argument must be a string.", nameof(argumentsElement));
            }
            arguments[argument.Name] = argument.Value.GetString() ?? "";
        }
        return arguments;
    }
}
