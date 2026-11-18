using System.Text.Json;
using FWO.Ai.Client;
using Microsoft.Extensions.AI;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Who wrote an entry of the AI chat.
    /// </summary>
    public enum AiChatEntryKind
    {
        /// <summary>A message of the user.</summary>
        User,

        /// <summary>An answer of the AI.</summary>
        Assistant
    }

    /// <summary>
    /// State of an answer of the AI.
    /// </summary>
    public enum AiChatAnswerState
    {
        /// <summary>The run is going on.</summary>
        Running,

        /// <summary>The run has ended with an answer.</summary>
        Completed,

        /// <summary>The run waits until the user approved or rejected the tool calls of the answer.</summary>
        WaitingForApproval,

        /// <summary>The user cancelled the run.</summary>
        Cancelled,

        /// <summary>The run could not be done or ended without an answer.</summary>
        Failed
    }

    /// <summary>
    /// A tool call the AI asks the user to approve, as the chat shows it.
    /// </summary>
    public sealed class AiChatApproval
    {
        /// <summary>Maximum length of the arguments shown.</summary>
        public const int kMaxArgumentsLength = 2000;

        /// <summary>Marks shown arguments that were cut to <see cref="kMaxArgumentsLength"/>.</summary>
        public const string kCutMarker = "…";

        private static readonly JsonSerializerOptions kArgumentsJsonOptions = new(AIJsonUtilities.DefaultOptions) { WriteIndented = true };

        /// <summary>
        /// Creates the approval of a tool approval request of a run.
        /// </summary>
        /// <param name="request">The request of the run</param>
        public AiChatApproval(ToolApprovalRequestContent request)
        {
            ArgumentNullException.ThrowIfNull(request);
            Request = request;
            FunctionCallContent? functionCall = request.ToolCall as FunctionCallContent;
            ToolName = functionCall?.Name ?? "";
            Arguments = FormatArguments(functionCall?.Arguments);
        }

        /// <summary>The request of the run, to create the answer from.</summary>
        internal ToolApprovalRequestContent Request { get; }

        /// <summary>Id of the request.</summary>
        public string RequestId => Request.RequestId;

        /// <summary>Name of the tool the AI wants to run.</summary>
        public string ToolName { get; }

        /// <summary>Arguments of the tool call as indented JSON, cut to <see cref="kMaxArgumentsLength"/>.</summary>
        public string Arguments { get; }

        /// <summary>Whether the user approved the call; null while the user has not answered.</summary>
        public bool? Approved { get; internal set; }

        /// <summary>
        /// Returns the arguments as indented JSON, cut to <see cref="kMaxArgumentsLength"/>; empty without arguments.
        /// </summary>
        internal static string FormatArguments(IDictionary<string, object?>? arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return "";
            }
            string json = JsonSerializer.Serialize(arguments, kArgumentsJsonOptions);
            if (json.Length <= kMaxArgumentsLength)
            {
                return json;
            }
            int keptLength = kMaxArgumentsLength - kCutMarker.Length;
            if (char.IsHighSurrogate(json[keptLength - 1]))
            {
                keptLength--;
            }
            return string.Concat(json.AsSpan(0, keptLength), kCutMarker);
        }
    }

    /// <summary>
    /// An entry of the AI chat: a message of the user or an answer of the AI. The text of an answer grows while its run
    /// is going on; the chat session changes the entries only while holding its lock, and replaces the text as a whole.
    /// </summary>
    public sealed class AiChatEntry
    {
        /// <summary>
        /// Creates an entry.
        /// </summary>
        /// <param name="kind">Who wrote the entry</param>
        /// <param name="text">Text of the entry</param>
        public AiChatEntry(AiChatEntryKind kind, string text = "")
        {
            Kind = kind;
            Text = text;
            State = kind == AiChatEntryKind.User ? AiChatAnswerState.Completed : AiChatAnswerState.Running;
        }

        /// <summary>Id of the entry, to key its rendering.</summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>Who wrote the entry.</summary>
        public AiChatEntryKind Kind { get; }

        /// <summary>Text of the entry; for an answer the text received so far.</summary>
        public string Text { get; internal set; }

        /// <summary>State of an answer; a message of the user is always completed.</summary>
        public AiChatAnswerState State { get; internal set; }

        /// <summary>Why a failed run could not be done, if the client knows it.</summary>
        public AgentClientException? ClientFailure { get; internal set; }

        /// <summary>Tool calls of the answer the AI asked the user to approve.</summary>
        public IReadOnlyList<AiChatApproval> Approvals { get; internal set; } = [];
    }
}
