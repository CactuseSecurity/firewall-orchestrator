using FWO.Config.Api;
using FWO.Data.Ai;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Texts, styles and availability of the AI chat (<c>AiChatPanel</c>, <c>AiChatNavButton</c>).
    /// </summary>
    public static class AiChatDisplay
    {
        /// <summary>Text key of the name of the chat, in the navigation bar and the panel title.</summary>
        public const string kChatTextKey = "ai_chat";

        /// <summary>Text key of the button starting a new conversation.</summary>
        public const string kNewChatTextKey = "ai_new_chat";

        /// <summary>Text key of the button minimizing the panel.</summary>
        public const string kMinimizeTextKey = "minimize";

        /// <summary>Text key of the button sending the message.</summary>
        public const string kSendTextKey = "ai_send";

        /// <summary>Text key of the placeholder of the message input.</summary>
        public const string kInputTextKey = "ai_chat_input";

        /// <summary>Text key shown while the AI is answering.</summary>
        public const string kAnsweringTextKey = "ai_answering";

        /// <summary>Text key of the badge of an answer that arrived while the panel was minimized.</summary>
        public const string kNewAnswerTextKey = "ai_new_answer";

        /// <summary>Text key of the badge of tool calls waiting for approval.</summary>
        public const string kApprovalPendingTextKey = "ai_approval_pending";

        /// <summary>Text key introducing the tool calls the AI asks the user to approve.</summary>
        public const string kApprovalRequestTextKey = "ai_approval_request";

        /// <summary>Text key of the button approving a tool call.</summary>
        public const string kApproveTextKey = "approve";

        /// <summary>Text key of the button rejecting a tool call.</summary>
        public const string kRejectTextKey = "reject";

        /// <summary>Text key of an approved tool call.</summary>
        public const string kApprovedTextKey = "approved";

        /// <summary>Text key of a rejected tool call.</summary>
        public const string kRejectedTextKey = "rejected";

        /// <summary>Text key of a cancelled run.</summary>
        public const string kCancelledTextKey = "ai_run_cancelled";

        /// <summary>Text key shown in an empty conversation.</summary>
        public const string kIntroTextKey = "U0017";

        /// <summary>Text key of the disclaimer below the input.</summary>
        public const string kDisclaimerTextKey = "U0018";

        /// <summary>Page of the online help of the chat, see <c>HelpLink</c>.</summary>
        public const string kHelpPage = "ai/chat";

        /// <summary>Css classes of a badge of an approved tool call.</summary>
        public const string kApprovedBadgeClass = "badge bg-success";

        /// <summary>Css classes of a badge of a rejected tool call.</summary>
        public const string kRejectedBadgeClass = "badge bg-secondary";

        /// <summary>
        /// Returns whether the chat is offered: it is switched on in the AI settings and an AI provider is configured.
        /// The AI harness refuses runs of the chat while it is switched off, whatever the UI shows.
        /// </summary>
        /// <param name="userConfig">Config of the user, holding the global AI settings</param>
        public static bool IsAvailable(UserConfig userConfig)
        {
            return userConfig.AiChatEnabled && userConfig.AiProvider != AiProvider.None;
        }

        /// <summary>
        /// Returns the message shown below an answer that was cancelled or failed; empty for all other answers.
        /// </summary>
        /// <param name="userConfig">Config of the user, for the texts</param>
        /// <param name="entry">The answer</param>
        public static string StateMessage(UserConfig userConfig, AiChatEntry entry)
        {
            return entry.State switch
            {
                AiChatAnswerState.Cancelled => userConfig.GetText(kCancelledTextKey),
                AiChatAnswerState.Failed when entry.ClientFailure != null => AiReasonCheckDisplay.FailureMessage(userConfig, entry.ClientFailure),
                AiChatAnswerState.Failed => userConfig.GetText(AiReasonCheckDisplay.kFailedTextKey),
                _ => ""
            };
        }

        /// <summary>
        /// Returns the css classes of the alert of an answer that was cancelled or failed: a warning for a cancelled run
        /// and a failure the user can solve or wait for, an error for all others.
        /// </summary>
        /// <param name="entry">The answer</param>
        public static string StateAlertClass(AiChatEntry entry)
        {
            bool warning = entry.State == AiChatAnswerState.Cancelled
                || (entry.ClientFailure != null && AiReasonCheckDisplay.IsUserSolvable(entry.ClientFailure.Failure));
            return warning ? AiReasonCheckDisplay.kWarningAlertClass : AiReasonCheckDisplay.kErrorAlertClass;
        }

        /// <summary>
        /// Returns the css classes of the badge of an answered tool call.
        /// </summary>
        /// <param name="approval">The tool call</param>
        public static string ApprovalBadgeClass(AiChatApproval approval)
        {
            return approval.Approved == true ? kApprovedBadgeClass : kRejectedBadgeClass;
        }

        /// <summary>
        /// Returns the text key of the badge of an answered tool call.
        /// </summary>
        /// <param name="approval">The tool call</param>
        public static string ApprovalTextKey(AiChatApproval approval)
        {
            return approval.Approved == true ? kApprovedTextKey : kRejectedTextKey;
        }
    }
}
