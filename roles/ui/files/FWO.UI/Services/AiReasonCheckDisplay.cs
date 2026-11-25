using FWO.Ai.Client;
using FWO.Ai.Client.ReasonChecks;
using FWO.Config.Api;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Texts and styles of the AI reason check (<c>AiReasonCheck</c> component): verdict badges and the messages shown
    /// when a check cannot be done.
    /// </summary>
    public static class AiReasonCheckDisplay
    {
        /// <summary>Text key of the button starting the check.</summary>
        public const string kCheckButtonTextKey = "ai_check_reason";

        /// <summary>Text key shown while the check is running.</summary>
        public const string kRunningTextKey = "ai_checking";

        /// <summary>Text key of the disclaimer below the answer.</summary>
        public const string kDisclaimerTextKey = "U9048";

        /// <summary>Text key of the message for a blank or too long reason.</summary>
        public const string kInvalidReasonTextKey = "E9032";

        /// <summary>Text key of the message for a run waiting for the approval of a tool call.</summary>
        public const string kInterruptedTextKey = "E9033";

        /// <summary>Text key of the message for a run that ended without an answer.</summary>
        public const string kFailedTextKey = "E9034";

        /// <summary>Text key of the message for a session without a valid access token.</summary>
        public const string kUnauthorizedTextKey = "E9035";

        /// <summary>Text key of the message for too many runs, if the time to wait is known.</summary>
        public const string kRateLimitedRetryTextKey = "E9038";

        /// <summary>Css classes of an alert about a problem the user can solve or wait for.</summary>
        public const string kWarningAlertClass = "alert alert-warning";

        /// <summary>Css classes of an alert about a failure of the AI assistant.</summary>
        public const string kErrorAlertClass = "alert alert-danger";

        private const string kMaxLengthPlaceholder = "@@MAX_LENGTH@@";
        private const string kSecondsPlaceholder = "@@SECONDS@@";
        private const string kUnknownVerdictTextKey = "ai_verdict_unknown";
        private const string kUnknownVerdictClass = "bg-secondary";

        private static readonly Dictionary<ReasonVerdict, string> kVerdictTextKeys = new()
        {
            { ReasonVerdict.Genuine, "ai_verdict_genuine" },
            { ReasonVerdict.Insufficient, "ai_verdict_insufficient" },
            { ReasonVerdict.NotGenuine, "ai_verdict_not_genuine" },
            { ReasonVerdict.Unknown, kUnknownVerdictTextKey }
        };

        private static readonly Dictionary<ReasonVerdict, string> kVerdictClasses = new()
        {
            { ReasonVerdict.Genuine, "bg-success" },
            { ReasonVerdict.Insufficient, "bg-warning text-dark" },
            { ReasonVerdict.NotGenuine, "bg-danger" },
            { ReasonVerdict.Unknown, kUnknownVerdictClass }
        };

        private static readonly Dictionary<AgentClientFailure, string> kFailureTextKeys = new()
        {
            { AgentClientFailure.Unauthorized, kUnauthorizedTextKey },
            { AgentClientFailure.Forbidden, "E9036" },
            { AgentClientFailure.RateLimited, "E9037" },
            { AgentClientFailure.RequestTooLarge, "E9040" },
            { AgentClientFailure.Unavailable, "E9041" },
            { AgentClientFailure.ServerError, "E9042" },
            { AgentClientFailure.Timeout, "E9043" },
            { AgentClientFailure.ProtocolError, "E9044" }
        };

        /// <summary>
        /// Failures the user can solve or wait for; all others are failures of the AI assistant or its installation.
        /// </summary>
        private static readonly HashSet<AgentClientFailure> kUserSolvableFailures =
        [
            AgentClientFailure.Unauthorized,
            AgentClientFailure.Forbidden,
            AgentClientFailure.RateLimited,
            AgentClientFailure.RequestTooLarge
        ];

        /// <summary>
        /// Returns the text key of the label of a verdict.
        /// </summary>
        /// <param name="verdict">Verdict of the answer</param>
        public static string VerdictTextKey(ReasonVerdict verdict)
        {
            return kVerdictTextKeys.GetValueOrDefault(verdict, kUnknownVerdictTextKey);
        }

        /// <summary>
        /// Returns the css classes of the badge of a verdict.
        /// </summary>
        /// <param name="verdict">Verdict of the answer</param>
        public static string VerdictCssClass(ReasonVerdict verdict)
        {
            return kVerdictClasses.GetValueOrDefault(verdict, kUnknownVerdictClass);
        }

        /// <summary>
        /// Returns the text key of the message of a failure.
        /// </summary>
        /// <param name="failure">Why the run could not be done</param>
        public static string FailureTextKey(AgentClientFailure failure)
        {
            return kFailureTextKeys.GetValueOrDefault(failure, kFailedTextKey);
        }

        /// <summary>
        /// Returns whether the user can solve or wait for a failure, which is shown as warning rather than as error.
        /// </summary>
        /// <param name="failure">Why the run could not be done</param>
        public static bool IsUserSolvable(AgentClientFailure failure)
        {
            return kUserSolvableFailures.Contains(failure);
        }

        /// <summary>
        /// Returns the localized message of a failed run, naming the seconds to wait if the AI assistant sent them.
        /// </summary>
        /// <param name="userConfig">Config of the user, for the texts</param>
        /// <param name="exception">The failure of the run</param>
        public static string FailureMessage(UserConfig userConfig, AgentClientException exception)
        {
            if (exception.Failure == AgentClientFailure.RateLimited && exception.RetryAfter is TimeSpan retryAfter)
            {
                int seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                return userConfig.GetText(kRateLimitedRetryTextKey).Replace(kSecondsPlaceholder, seconds.ToString());
            }
            return userConfig.GetText(FailureTextKey(exception.Failure));
        }

        /// <summary>
        /// Returns the localized message for a reason that is blank or longer than allowed.
        /// </summary>
        /// <param name="userConfig">Config of the user, for the texts</param>
        public static string InvalidReasonMessage(UserConfig userConfig)
        {
            return userConfig.GetText(kInvalidReasonTextKey).Replace(kMaxLengthPlaceholder, ReasonCheckLimits.kMaxReasonLength.ToString());
        }
    }
}
