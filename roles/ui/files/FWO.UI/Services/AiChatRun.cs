using System.Text;
using FWO.Ai.Client;
using Microsoft.Extensions.AI;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Collects the outcome of a run of the AI chat from the updates of the FWO agent chat client: the answer text, the
    /// tool calls the run asks the user to approve, its error, its thread and the conversation the harness sent at its
    /// end, which the next run of the chat sends again.
    /// </summary>
    public sealed class AiChatRun
    {
        private readonly AgentAnswerText answerText = new();
        private readonly StringBuilder text = new();

        /// <summary>The answer text received so far.</summary>
        public string Text => text.ToString();

        /// <summary>Tool calls the run asks the user to approve, in the order of the run.</summary>
        public List<ToolApprovalRequestContent> ApprovalRequests { get; } = [];

        /// <summary>The error the harness reported for the run (AG-UI RUN_ERROR), if any.</summary>
        public ErrorContent? Error { get; private set; }

        /// <summary>Thread of the run, if the harness named it.</summary>
        public string? ThreadId { get; private set; }

        /// <summary>The conversation the harness sent at the end of the run (MESSAGES_SNAPSHOT); null if it sent none.</summary>
        public List<ChatMessage>? Conversation { get; private set; }

        /// <summary>
        /// Applies an update of the run.
        /// </summary>
        /// <param name="update">Update of the run</param>
        /// <returns>Whether the update added answer text</returns>
        public bool Apply(ChatResponseUpdate update)
        {
            ArgumentNullException.ThrowIfNull(update);
            if (AgentConversation.TryGetThreadId(update, out string? threadId))
            {
                ThreadId = threadId;
            }
            if (AgentConversation.TryGetSnapshot(update, out List<ChatMessage>? conversation))
            {
                Conversation = conversation;
            }
            ApprovalRequests.AddRange(update.Contents.OfType<ToolApprovalRequestContent>());
            Error ??= update.Contents.OfType<ErrorContent>().FirstOrDefault();
            string textDelta = answerText.GetDelta(update);
            text.Append(textDelta);
            return textDelta.Length > 0;
        }
    }
}
