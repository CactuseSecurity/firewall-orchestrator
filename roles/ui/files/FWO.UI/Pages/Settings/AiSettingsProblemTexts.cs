using FWO.Data.Ai;

namespace FWO.Ui.Pages.Settings
{
    /// <summary>
    /// Text keys of the messages the AI settings page shows for the problems of <see cref="AiSettings.Validate"/>.
    /// </summary>
    public static class AiSettingsProblemTexts
    {
        private static readonly Dictionary<AiSettingsProblem, string> kTextKeys = new()
        {
            [AiSettingsProblem.ModelMissing] = "E5303",
            [AiSettingsProblem.CredentialMissing] = "E5304",
            [AiSettingsProblem.EndpointInvalid] = "E5305",
            [AiSettingsProblem.EndpointNotSecure] = "E5306",
            [AiSettingsProblem.ContextWindowInvalid] = "E5307",
            [AiSettingsProblem.MaxOutputTokensInvalid] = "E5308",
            [AiSettingsProblem.MaxIterationsInvalid] = "E5309",
            [AiSettingsProblem.MaxConsecutiveErrorsInvalid] = "E5310",
            [AiSettingsProblem.CompactionReserveInvalid] = "E5311",
            [AiSettingsProblem.OAuthNotSupported] = "E5312",
            [AiSettingsProblem.OAuthEndpointMissing] = "E5313",
            [AiSettingsProblem.TokenUrlInvalid] = "E5314",
            [AiSettingsProblem.CustomHeadersUnreadable] = "E5315",
            [AiSettingsProblem.CustomHeadersTooMany] = "E5316",
            [AiSettingsProblem.CustomHeaderNameInvalid] = "E5317",
            [AiSettingsProblem.CustomHeaderNameReserved] = "E5318",
            [AiSettingsProblem.CustomHeaderNameDuplicate] = "E5319",
            [AiSettingsProblem.CustomHeaderValueInvalid] = "E5320",
            [AiSettingsProblem.RequestTimeoutInvalid] = "E5321"
        };

        /// <summary>
        /// Returns the text key of the message of a problem.
        /// </summary>
        /// <param name="problem">Problem found by the validation</param>
        /// <returns>The text key in the txt table.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The problem has no message.</exception>
        public static string GetTextKey(AiSettingsProblem problem)
        {
            return kTextKeys.TryGetValue(problem, out string? textKey)
                ? textKey
                : throw new ArgumentOutOfRangeException(nameof(problem), problem, "The problem has no message.");
        }
    }
}
