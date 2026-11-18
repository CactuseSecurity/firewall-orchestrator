using FWO.Ai.Client;
using Microsoft.Extensions.AI;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Collects the outcome of a reason check run from the updates of the FWO agent chat client: the pieces of the answer
    /// text (the texts of different messages separated by an empty line), the tool approvals the run asked for, its
    /// error, and its thread for the log. The reason check is a single turn, so the conversation the harness returns is
    /// not kept.
    /// </summary>
    public sealed class AiReasonCheckRun
    {
        /// <summary>Put between the texts of two messages of the run.</summary>
        public const string kMessageSeparator = AgentAnswerText.kMessageSeparator;

        private readonly AgentAnswerText answerText = new();

        /// <summary>Number of tool calls the run asked the user to approve; the reason check does not answer them.</summary>
        public int PendingApprovals { get; private set; }

        /// <summary>The error the harness reported for the run (AG-UI RUN_ERROR), if any.</summary>
        public ErrorContent? Error { get; private set; }

        /// <summary>Thread of the run, if the harness named it.</summary>
        public string? ThreadId { get; private set; }

        /// <summary>
        /// Applies an update of the run.
        /// </summary>
        /// <param name="update">Update of the run</param>
        /// <returns>The text to append to the answer; empty if the update carries none</returns>
        public string Apply(ChatResponseUpdate update)
        {
            ArgumentNullException.ThrowIfNull(update);
            if (AgentConversation.TryGetThreadId(update, out string? threadId))
            {
                ThreadId = threadId;
            }
            PendingApprovals += update.Contents.OfType<ToolApprovalRequestContent>().Count();
            Error ??= update.Contents.OfType<ErrorContent>().FirstOrDefault();
            return answerText.GetDelta(update);
        }
    }
}
