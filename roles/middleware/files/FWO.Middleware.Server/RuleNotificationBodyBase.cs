using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report;
using FWO.Services;
using FWO.Ui.Display;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Shared HTML body helpers for rule-based notifications.
    /// </summary>
    public abstract class RuleNotificationBodyBase(GlobalConfig globalConfig)
    {
        private static readonly List<int> RawRuleHtmlColumnIndexes = [2, 3, 4];

        private readonly CustomFieldKeyCache changeIdKeyCache = new();

        /// <summary>
        /// Builds a plain-text rule notification body with the standard rule columns and optional extra columns.
        /// </summary>
        /// <param name="owner">Owner used for placeholder replacement.</param>
        /// <param name="bodyTemplate">Configured mail body template.</param>
        /// <param name="timeIntervalText">Resolved notification interval text.</param>
        /// <param name="rules">Rules to render.</param>
        /// <param name="extraHeaders">Optional extra header texts.</param>
        /// <param name="getExtraCellValues">Optional extra cell values per row.</param>
        /// <returns>Plain-text mail body.</returns>
        protected string BuildRuleTextBody<TRule>(
            FwoOwner owner,
            string bodyTemplate,
            string timeIntervalText,
            IEnumerable<TRule> rules,
            IEnumerable<string>? extraHeaders = null,
            Func<TRule, IEnumerable<string?>>? getExtraCellValues = null)
            where TRule : Rule
        {
            List<string> headerTexts = BuildHeaderTexts(extraHeaders);
            string introText = BuildIntroText(owner, bodyTemplate, timeIntervalText);
            List<NotificationTableRow> rows = BuildNotificationRows(rules, getExtraCellValues);
            string textRuleTable = NotificationTableBodyBuilder.BuildTextTable(headerTexts, rows);

            return NotificationTableBodyBuilder.BuildTextBody(introText, textRuleTable);
        }

        /// <summary>
        /// Builds an HTML rule notification body with the standard rule columns and optional extra columns.
        /// </summary>
        /// <param name="owner">Owner used for placeholder replacement.</param>
        /// <param name="bodyTemplate">Configured mail body template.</param>
        /// <param name="timeIntervalText">Resolved notification interval text.</param>
        /// <param name="rules">Rules to render.</param>
        /// <param name="extraHeaders">Optional extra header texts.</param>
        /// <param name="getExtraCellValues">Optional extra cell values per row.</param>
        /// <param name="frameTitle">Optional localized title for a framed HTML report section.</param>
        /// <returns>HTML mail body.</returns>
        protected string BuildRuleHtmlBody<TRule>(
            FwoOwner owner,
            string bodyTemplate,
            string timeIntervalText,
            IEnumerable<TRule> rules,
            IEnumerable<string>? extraHeaders = null,
            Func<TRule, IEnumerable<string?>>? getExtraCellValues = null,
            string? frameTitle = null)
            where TRule : Rule
        {
            List<string> headerTexts = BuildHeaderTexts(extraHeaders);
            string introText = BuildIntroText(owner, bodyTemplate, timeIntervalText);
            List<NotificationTableRow> rows = BuildNotificationRows(rules, getExtraCellValues);
            string htmlRuleTable = NotificationTableBodyBuilder.BuildHtmlTable(headerTexts, rows, RawRuleHtmlColumnIndexes);

            if (!string.IsNullOrWhiteSpace(frameTitle))
            {
                htmlRuleTable = BuildHtmlReportSection(frameTitle, htmlRuleTable, owner);
            }

            htmlRuleTable = NotificationTableBodyBuilder.HtmlTableStyleBlock + htmlRuleTable;
            return NotificationTableBodyBuilder.BuildHtmlBody(introText, htmlRuleTable);
        }

        /// <summary>
        /// Extracts a change identifier from rule custom fields.
        /// </summary>
        /// <param name="rule">Rule containing the serialized custom fields.</param>
        /// <returns>The extracted change identifier or an empty string.</returns>
        protected string ExtractChangeId(Rule rule)
        {
            string? changeId = CustomFieldResolver.ExtractCustomFieldValue<string>(
                rule,
                changeIdKeyCache.GetKeys(GlobalConfig.CustomFieldChangeIdKey),
                out _);
            return changeId ?? "";
        }

        private NotificationTableRow CreateNotificationRow<TRule>(
            TRule rule,
            RuleDisplayHtml ruleDisplayHtml,
            Func<TRule, IEnumerable<string?>>? getExtraCellValues)
            where TRule : Rule
        {
            string? sourceHtml = ruleDisplayHtml.DisplaySource(rule, OutputLocation.export, ReportType.ResolvedRules);
            string? destinationHtml = ruleDisplayHtml.DisplayDestination(rule, OutputLocation.export, ReportType.ResolvedRules);
            string? servicesHtml = ruleDisplayHtml.DisplayServices(rule, OutputLocation.export, ReportType.ResolvedRules);
            string changeId = ExtractChangeId(rule);
            string lastHit = rule.Metadata.LastHit?.ToString("yyyy-MM-dd") ?? "";

            List<string?> htmlCells =
            [
                rule.Uid,
                rule.Name,
                sourceHtml,
                destinationHtml,
                servicesHtml,
                changeId,
                lastHit
            ];

            List<string> textCells =
            [
                NotificationTableBodyBuilder.NormalizeTextCell(rule.Uid),
                NotificationTableBodyBuilder.NormalizeTextCell(rule.Name),
                NotificationTableBodyBuilder.NormalizeTextCell(sourceHtml),
                NotificationTableBodyBuilder.NormalizeTextCell(destinationHtml),
                NotificationTableBodyBuilder.NormalizeTextCell(servicesHtml),
                NotificationTableBodyBuilder.NormalizeTextCell(changeId),
                NotificationTableBodyBuilder.NormalizeTextCell(lastHit)
            ];

            if (getExtraCellValues != null)
            {
                foreach (string? extraCellValue in getExtraCellValues(rule))
                {
                    htmlCells.Add(extraCellValue);
                    textCells.Add(NotificationTableBodyBuilder.NormalizeTextCell(extraCellValue));
                }
            }

            return new NotificationTableRow
            {
                HtmlCells = htmlCells,
                TextCells = textCells
            };
        }

        private List<string> BuildHeaderTexts(IEnumerable<string>? extraHeaders)
        {
            List<string> headerTexts =
            [
                GlobalConfig.GetNotificationText("uid"),
                GlobalConfig.GetNotificationText("name"),
                GlobalConfig.GetNotificationText("source"),
                GlobalConfig.GetNotificationText("destination"),
                GlobalConfig.GetNotificationText("service"),
                GlobalConfig.GetNotificationText("change_id"),
                GlobalConfig.GetNotificationText("last_hit")
            ];

            if (extraHeaders != null)
            {
                headerTexts.AddRange(extraHeaders);
            }

            return headerTexts;
        }

        private static string BuildIntroText(FwoOwner owner, string bodyTemplate, string timeIntervalText)
        {
            return bodyTemplate
                .Replace(Placeholder.APPNAME, owner.Name)
                .Replace(Placeholder.APPID, owner.ExtAppId ?? "")
                .Replace(Placeholder.TIME_INTERVAL, timeIntervalText);
        }

        private List<NotificationTableRow> BuildNotificationRows<TRule>(
            IEnumerable<TRule> rules,
            Func<TRule, IEnumerable<string?>>? getExtraCellValues)
            where TRule : Rule
        {
            RuleDisplayHtml ruleDisplayHtml = CreateRuleDisplayHtml();
            return rules
                .Select(rule => CreateNotificationRow(rule, ruleDisplayHtml, getExtraCellValues))
                .ToList();
        }

        private string BuildHtmlReportSection(string title, string body, FwoOwner? owner)
        {
            return NotificationTableBodyBuilder.BuildHtmlReportSection(
                title,
                body,
                owner?.Name,
                GlobalConfig.GetNotificationText("generated_on"),
                GlobalConfig.GetNotificationText("owners"));
        }

        /// <summary>
        /// Returns the global configuration used by the notification body helpers.
        /// </summary>
        protected GlobalConfig GlobalConfig { get; } = globalConfig;

        private RuleDisplayHtml CreateRuleDisplayHtml()
        {
            UserConfig displayUserConfig = UserConfig.ForTextOnly(GlobalConfig, false);
            return new RuleDisplayHtml(displayUserConfig);
        }

    }
}
