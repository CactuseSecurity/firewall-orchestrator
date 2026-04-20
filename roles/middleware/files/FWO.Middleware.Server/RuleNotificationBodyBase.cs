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
        private const string kTextColumnSeparator = " | ";

        /// <summary>
        /// Holds the shared display values for a single rule row.
        /// </summary>
        /// <param name="HtmlCells">Cell values rendered for the HTML table.</param>
        /// <param name="TextCells">Cell values rendered for the plain-text table.</param>
        private sealed record RuleNotificationRow(List<string?> HtmlCells, List<string> TextCells);

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
            List<RuleNotificationRow> rows = BuildNotificationRows(rules, getExtraCellValues);
            string textRuleTable = BuildTextRuleTable(headerTexts, rows);

            return BuildTextBody(introText, textRuleTable);
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
            List<RuleNotificationRow> rows = BuildNotificationRows(rules, getExtraCellValues);
            string htmlRuleTable = BuildHtmlRuleTable(headerTexts, rows);

            if (!string.IsNullOrWhiteSpace(frameTitle))
            {
                htmlRuleTable = BuildHtmlReportSection(frameTitle, htmlRuleTable, owner);
            }

            return BuildHtmlDocument(BuildHtmlBody(introText, htmlRuleTable));
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

        private static RuleNotificationRow CreateNotificationRow<TRule>(
            TRule rule,
            RuleDisplayHtml ruleDisplayHtml,
            Func<TRule, IEnumerable<string?>>? getExtraCellValues)
            where TRule : Rule
        {
            string? sourceHtml = ruleDisplayHtml.DisplaySource(rule, FWO.Report.OutputLocation.export, FWO.Basics.ReportType.ResolvedRules);
            string? destinationHtml = ruleDisplayHtml.DisplayDestination(rule, FWO.Report.OutputLocation.export, FWO.Basics.ReportType.ResolvedRules);
            string? servicesHtml = ruleDisplayHtml.DisplayServices(rule, FWO.Report.OutputLocation.export, FWO.Basics.ReportType.ResolvedRules);
            string changeId = ExtractChangeId(rule.CustomFields);
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
                NormalizeTextCell(rule.Uid),
                NormalizeTextCell(rule.Name),
                NormalizeTextCell(sourceHtml),
                NormalizeTextCell(destinationHtml),
                NormalizeTextCell(servicesHtml),
                NormalizeTextCell(changeId),
                NormalizeTextCell(lastHit)
            ];

            if (getExtraCellValues != null)
            {
                foreach (string? extraCellValue in getExtraCellValues(rule))
                {
                    htmlCells.Add(extraCellValue);
                    textCells.Add(NormalizeTextCell(extraCellValue));
                }
            }

            return new RuleNotificationRow(htmlCells, textCells);
        }

        private List<string> BuildHeaderTexts(IEnumerable<string>? extraHeaders)
        {
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

            return headerTexts;
        }

        private static string BuildIntroText(FwoOwner owner, string bodyTemplate, string timeIntervalText)
        {
            return bodyTemplate
                .Replace(Placeholder.APPNAME, owner.Name)
                .Replace(Placeholder.APPID, owner.ExtAppId ?? "")
                .Replace(Placeholder.TIME_INTERVAL, timeIntervalText);
        }

        private List<RuleNotificationRow> BuildNotificationRows<TRule>(
            IEnumerable<TRule> rules,
            Func<TRule, IEnumerable<string?>>? getExtraCellValues)
            where TRule : Rule
        {
            RuleDisplayHtml ruleDisplayHtml = CreateRuleDisplayHtml();
            return rules
                .Select(rule => CreateNotificationRow(rule, ruleDisplayHtml, getExtraCellValues))
                .ToList();
        }

        private static string BuildHtmlRuleTable(IEnumerable<string> headerTexts, IEnumerable<RuleNotificationRow> rows)
        {
            StringBuilder builder = new();
            builder.Append("<table border=\"1\" cellspacing=\"0\" cellpadding=\"4\">")
                .Append("<thead><tr>");

            foreach (string headerText in headerTexts)
            {
                builder.Append("<th>")
                    .Append(WebUtility.HtmlEncode(headerText))
                    .Append("</th>");
            }

            builder.Append("</tr></thead><tbody>");
            foreach (RuleNotificationRow row in rows)
            {
                builder.Append("<tr>");
                for (int index = 0; index < row.HtmlCells.Count; ++index)
                {
                    builder.Append(index is 2 or 3 or 4 ? HtmlRawCell(row.HtmlCells[index]) : HtmlCell(row.HtmlCells[index]));
                }
                builder.Append("</tr>");
            }

            builder.Append("</tbody></table>");
            return builder.ToString();
        }

        private static string BuildTextRuleTable(IEnumerable<string> headerTexts, IEnumerable<RuleNotificationRow> rows)
        {
            StringBuilder builder = new();
            builder.AppendLine(string.Join(kTextColumnSeparator, headerTexts.Select(NormalizeTextCell)));
            foreach (RuleNotificationRow row in rows)
            {
                builder.AppendLine(string.Join(kTextColumnSeparator, row.TextCells));
            }
            return builder.ToString().TrimEnd();
        }

        private static string BuildHtmlBody(string introText, string htmlRuleTable)
        {
            StringBuilder builder = new();
            string[] bodyParts = introText.Split(Placeholder.RULE_TABLE, StringSplitOptions.None);
            for (int index = 0; index < bodyParts.Length; ++index)
            {
                AppendEncodedParagraph(builder, bodyParts[index]);
                if (index < bodyParts.Length - 1)
                {
                    builder.Append(htmlRuleTable);
                }
            }
            return builder.ToString();
        }

        private string BuildHtmlDocument(string body)
        {
            StringBuilder html = new();
            html.AppendLine("<!DOCTYPE html>")
                .AppendLine("<html>")
                .AppendLine("<head>")
                .AppendLine("    <meta charset=\"utf-8\"/>")
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
                .AppendLine("<body>")
                .Append(body)
                .AppendLine()
                .AppendLine("</body>")
                .Append("</html>");

            return html.ToString();
        }

        private string BuildHtmlReportSection(string title, string body, FwoOwner? owner)
        {
            UserConfig userConfig = new(GlobalConfig);
            StringBuilder html = new();

            html.Append("<h2>")
                .Append(WebUtility.HtmlEncode(title))
                .AppendLine("</h2>")
                .Append("<p>")
                .Append(WebUtility.HtmlEncode(userConfig.GetText("generated_on")))
                .Append(": ")
                .Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssK"))
                .AppendLine(" (UTC)</p>");

            if (owner != null)
            {
                html.Append("<p>")
                    .Append(WebUtility.HtmlEncode(userConfig.GetText("owners")))
                    .Append(": ")
                    .Append(WebUtility.HtmlEncode(owner.Name))
                    .AppendLine("</p>");
            }

            html.AppendLine("<hr>")
                .Append(body);

            return html.ToString();
        }

        private static string BuildTextBody(string introText, string textRuleTable)
        {
            StringBuilder builder = new();
            string[] bodyParts = introText.Split(Placeholder.RULE_TABLE, StringSplitOptions.None);
            for (int index = 0; index < bodyParts.Length; ++index)
            {
                AppendTextSection(builder, bodyParts[index]);
                if (index < bodyParts.Length - 1)
                {
                    AppendTextSection(builder, textRuleTable);
                }
            }
            return builder.ToString().Trim();
        }

        private static void AppendTextSection(StringBuilder builder, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine().AppendLine();
            }

            builder.Append(text.Trim());
        }

        private static string NormalizeTextCell(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            string normalized = Regex.Replace(value, @"<br\s*/?>", ", ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            normalized = Regex.Replace(normalized, "<.*?>", string.Empty, RegexOptions.CultureInvariant);
            normalized = WebUtility.HtmlDecode(normalized);
            normalized = Regex.Replace(normalized, @"\s+", " ", RegexOptions.CultureInvariant);
            return normalized.Trim();
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
