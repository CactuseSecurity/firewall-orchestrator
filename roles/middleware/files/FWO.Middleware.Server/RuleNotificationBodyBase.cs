using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Display;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

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

            StringBuilder tableBuilder = new();
            tableBuilder.Append("<table border=\"1\" cellspacing=\"0\" cellpadding=\"4\">")
                .Append("<thead><tr>");

            foreach (string headerText in headerTexts)
            {
                tableBuilder.Append("<th>")
                    .Append(WebUtility.HtmlEncode(headerText))
                    .Append("</th>");
            }

            tableBuilder.Append("</tr></thead><tbody>");

            foreach (TRule rule in rules)
            {
                tableBuilder.Append("<tr>")
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
                        tableBuilder.Append(HtmlCell(extraCellValue));
                    }
                }

                tableBuilder.Append("</tr>");
            }

            tableBuilder.Append("</tbody></table>");
            string ruleTable = tableBuilder.ToString();

            StringBuilder bodyBuilder = new();
            string[] bodyParts = introText.Split(Placeholder.RULE_TABLE, StringSplitOptions.None);
            for (int index = 0; index < bodyParts.Length; ++index)
            {
                AppendEncodedParagraph(bodyBuilder, bodyParts[index]);
                if (index < bodyParts.Length - 1)
                {
                    bodyBuilder.Append(ruleTable);
                }
            }
            return bodyBuilder.ToString();
        }

        /// <summary>
        /// Wraps a pre-rendered notification HTML body in the standard report frame.
        /// </summary>
        /// <param name="title">Localized notification title.</param>
        /// <param name="body">Notification body HTML.</param>
        /// <param name="ownerFilter">Optional owner text displayed in the frame header.</param>
        /// <returns>Full HTML mail body including the shared notification frame.</returns>
        protected string CreateNotificationBodyHtml(string title, string body, string? ownerFilter = null)
        {
            return BuildHtmlFrame(title, body, ownerFilter);
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

            string? field2 = TryExtractCustomFieldValue(customFieldsString, GlobalConst.kField2);
            if (!string.IsNullOrWhiteSpace(field2))
            {
                return field2;
            }

            string? fallback = TryExtractCustomFieldValue(customFieldsString, GlobalConst.kDatumRegelpr);
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                return fallback;
            }

            return "";
        }

        private static string? TryExtractCustomFieldValue(string customFieldsString, string key)
        {
            Match match = Regex.Match(
                customFieldsString,
                $@"['""]{Regex.Escape(key)}['""]\s*:\s*['""](?<value>(?:\\.|[^'""])*)['""]",
                RegexOptions.CultureInvariant);

            return match.Success ? Regex.Unescape(match.Groups["value"].Value) : null;
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
        /// Appends a paragraph with HTML-encoded text if content is present.
        /// </summary>
        /// <param name="builder">Target builder.</param>
        /// <param name="text">Text content to append.</param>
        private static void AppendEncodedParagraph(StringBuilder builder, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            builder.Append("<p>")
                .Append(WebUtility.HtmlEncode(text).Replace(Environment.NewLine, "<br>").Replace("\n", "<br>"))
                .Append("</p>");
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

        /// <summary>
        /// Builds the standard HTML frame used for full HTML notification bodies.
        /// </summary>
        /// <param name="title">Localized title shown in the HTML frame.</param>
        /// <param name="body">Pre-rendered notification body HTML.</param>
        /// <param name="ownerFilter">Optional owner text shown in the header.</param>
        /// <returns>Framed HTML document.</returns>
        private string BuildHtmlFrame(string title, string body, string? ownerFilter)
        {
            UserConfig userConfig = new(GlobalConfig);
            StringBuilder html = new();
            html.AppendLine("<!DOCTYPE html>")
                .AppendLine("<html>")
                .AppendLine("<head>")
                .AppendLine("    <meta charset=\"utf-8\"/>")
                .Append("    <title>")
                .Append(WebUtility.HtmlEncode(title))
                .AppendLine("</title>")
                .AppendLine("    <style>")
                .AppendLine("        table {")
                .AppendLine("            font-family: arial, sans-serif;")
                .AppendLine("            font-size: 10px;")
                .AppendLine("            border-collapse: collapse;")
                .AppendLine("            width: 100%;")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        td {")
                .AppendLine("            border: 1px solid #000000;")
                .AppendLine("            text-align: left;")
                .AppendLine("            padding: 3px;")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        th {")
                .AppendLine("            border: 1px solid #000000;")
                .AppendLine("            text-align: left;")
                .AppendLine("            padding: 3px;")
                .AppendLine("            background-color: #dddddd;")
                .AppendLine("        }")
                .AppendLine("    </style>")
                .AppendLine("</head>")
                .AppendLine("<body>");

            html.Append("<h2>")
                .Append(WebUtility.HtmlEncode(title))
                .AppendLine("</h2>")
                .Append("<p>")
                .Append(WebUtility.HtmlEncode(userConfig.GetText("generated_on")))
                .Append(": ")
                .Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssK"))
                .AppendLine(" (UTC)</p>");

            if (!string.IsNullOrWhiteSpace(ownerFilter))
            {
                html.Append("<p>")
                    .Append(WebUtility.HtmlEncode(userConfig.GetText("owners")))
                    .Append(": ")
                    .Append(WebUtility.HtmlEncode(ownerFilter))
                    .AppendLine("</p>");
            }

            html.AppendLine("<hr>")
                .Append(body)
                .AppendLine()
                .AppendLine("</body>")
                .Append("</html>");

            return html.ToString();
        }
    }
}
