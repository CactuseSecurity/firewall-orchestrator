using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Display;
using System.Net;
using System.Text;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Shared HTML body helpers for rule-based notifications.
    /// </summary>
    public abstract class RuleNotificationBodyBase(GlobalConfig globalConfig)
    {
        /// <summary>
        /// Builds a rule notification body with the standard rule columns and optional extra columns.
        /// </summary>
        /// <param name="owner">Owner used for placeholder replacement.</param>
        /// <param name="bodyTemplate">Configured mail body template.</param>
        /// <param name="timeIntervalText">Resolved notification interval text.</param>
        /// <param name="rules">Rules to render.</param>
        /// <param name="extraHeaders">Optional extra header texts.</param>
        /// <param name="getExtraCellValues">Optional extra cell values per row.</param>
        /// <returns>HTML mail body.</returns>
        protected string BuildRuleBody<TRule>(
            FwoOwner owner,
            string bodyTemplate,
            string timeIntervalText,
            IEnumerable<TRule> rules,
            IEnumerable<string>? extraHeaders = null,
            Func<TRule, IEnumerable<string?>>? getExtraCellValues = null)
            where TRule : Rule
        {
            RuleDisplayHtml ruleDisplayHtml = CreateRuleDisplayHtml();
            List<string> headerTexts =
            [
                GlobalConfig.GetText("uid"),
                GlobalConfig.GetText("name"),
                GlobalConfig.GetText("source"),
                GlobalConfig.GetText("destination"),
                GlobalConfig.GetText("service"),
                GlobalConfig.GetText("change_id"),
                GlobalConfig.GetText("last_hit")
            ];

            if (extraHeaders != null)
            {
                headerTexts.AddRange(extraHeaders);
            }

            string introText = bodyTemplate
                .Replace(Placeholder.APPNAME, owner.Name)
                .Replace(Placeholder.APPID, owner.ExtAppId ?? "")
                .Replace(Placeholder.TIME_INTERVAL, timeIntervalText);

            StringBuilder builder = new();
            builder.Append("<p>")
                .Append(WebUtility.HtmlEncode(introText).Replace(Environment.NewLine, "<br>").Replace("\n", "<br>"))
                .Append("</p>")
                .Append("<table border=\"1\" cellspacing=\"0\" cellpadding=\"4\">")
                .Append("<thead><tr>");

            foreach (string headerText in headerTexts)
            {
                builder.Append("<th>")
                    .Append(WebUtility.HtmlEncode(headerText))
                    .Append("</th>");
            }

            builder.Append("</tr></thead><tbody>");

            foreach (TRule rule in rules)
            {
                builder.Append("<tr>")
                    .Append(HtmlCell(rule.Uid))
                    .Append(HtmlCell(rule.Name))
                    .Append(HtmlRawCell(ruleDisplayHtml.DisplaySource(rule, FWO.Report.OutputLocation.export, FWO.Basics.ReportType.ResolvedRules)))
                    .Append(HtmlRawCell(ruleDisplayHtml.DisplayDestination(rule, FWO.Report.OutputLocation.export, FWO.Basics.ReportType.ResolvedRules)))
                    .Append(HtmlRawCell(ruleDisplayHtml.DisplayServices(rule, FWO.Report.OutputLocation.export, FWO.Basics.ReportType.ResolvedRules)))
                    .Append(HtmlCell(ExtractChangeId(rule.CustomFields)))
                    .Append(HtmlCell(rule.Metadata.LastHit?.ToString("yyyy-MM-dd") ?? ""));

                if (getExtraCellValues != null)
                {
                    foreach (string? extraCellValue in getExtraCellValues(rule))
                    {
                        builder.Append(HtmlCell(extraCellValue));
                    }
                }

                builder.Append("</tr>");
            }

            builder.Append("</tbody></table>");
            return builder.ToString();
        }

        /// <summary>
        /// Extracts a change identifier from rule custom fields.
        /// </summary>
        /// <param name="customFieldsString">Serialized rule custom fields.</param>
        /// <returns>The extracted change identifier or an empty string.</returns>
        private static string ExtractChangeId(string customFieldsString)
        {
            if (string.IsNullOrWhiteSpace(customFieldsString))
            {
                return "";
            }

            try
            {
                Dictionary<string, string>? customFields = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(customFieldsString);
                if (customFields == null)
                {
                    return "";
                }

                if (customFields.TryGetValue(GlobalConst.kField2, out string? value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                if (customFields.TryGetValue(GlobalConst.kDatumRegelpr, out string? fallback) && !string.IsNullOrWhiteSpace(fallback))
                {
                    return fallback;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                return "";
            }

            return "";
        }

        /// <summary>
        /// Builds an HTML table cell with escaped text.
        /// </summary>
        /// <param name="value">Cell value.</param>
        /// <returns>HTML table cell.</returns>
        private static string HtmlCell(string? value)
        {
            return $"<td>{WebUtility.HtmlEncode(value ?? "")}</td>";
        }

        /// <summary>
        /// Builds an HTML table cell without escaping the value.
        /// </summary>
        /// <param name="value">Cell value.</param>
        /// <returns>HTML table cell.</returns>
        private static string HtmlRawCell(string? value)
        {
            return $"<td>{value ?? ""}</td>";
        }

        /// <summary>
        /// Returns the global configuration used by the notification body helpers.
        /// </summary>
        protected GlobalConfig GlobalConfig { get; } = globalConfig;

        private RuleDisplayHtml CreateRuleDisplayHtml()
        {
            UserConfig displayUserConfig = new(GlobalConfig, false);
            return new RuleDisplayHtml(displayUserConfig);
        }
    }
}
