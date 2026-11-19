using System.Globalization;
using FWO.Config.Api;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Builds the ticket comment that documents the result of a rollout check.
    /// </summary>
    public static class RolloutCheckComment
    {
        /// <summary>
        /// Text key of the comment for a passed check.
        /// </summary>
        public const string kPassedTextKey = "rollout_check_ok";

        /// <summary>
        /// Text key of the comment for a passed check of a ticket without managements.
        /// </summary>
        public const string kPassedWithoutManagementsTextKey = "rollout_check_ok_no_mgmt";

        /// <summary>
        /// Text key of the comment for a check with missing imports.
        /// </summary>
        public const string kMissingTextKey = "rollout_check_missing";

        private const string kListSeparator = ", ";
        private const string kDateFormat = "yyyy-MM-dd HH:mm:ss";
        private const string kUnknownDate = "-";

        /// <summary>
        /// Builds the comment text for a rollout check.
        /// </summary>
        /// <param name="userConfig">Configuration providing the texts.</param>
        /// <param name="result">Result of the check.</param>
        /// <param name="importData">Import data providing the management names.</param>
        /// <returns>The comment text.</returns>
        public static string Build(UserConfig userConfig, RolloutCheckResult result, RolloutCheckImportData importData)
        {
            string reviewStart = result.ReviewStart?.ToString(kDateFormat, CultureInfo.InvariantCulture) ?? kUnknownDate;
            if (!result.Passed)
            {
                return string.Format(CultureInfo.InvariantCulture, userConfig.GetText(kMissingTextKey),
                    reviewStart, JoinNames(importData, result.MissingManagementIds));
            }
            if (result.InvolvedManagementIds.Count == 0)
            {
                return userConfig.GetText(kPassedWithoutManagementsTextKey);
            }
            return string.Format(CultureInfo.InvariantCulture, userConfig.GetText(kPassedTextKey),
                reviewStart, JoinNames(importData, result.InvolvedManagementIds));
        }

        private static string JoinNames(RolloutCheckImportData importData, List<int> managementIds)
        {
            return string.Join(kListSeparator, managementIds.Select(importData.GetManagementName));
        }
    }
}
