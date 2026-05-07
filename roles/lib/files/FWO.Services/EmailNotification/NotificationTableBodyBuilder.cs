using FWO.Basics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FWO.Services
{
    /// <summary>
    /// Shared helpers for notification table bodies in text and HTML form.
    /// </summary>
    public static class NotificationTableBodyBuilder
    {
        private const string kTextColumnSeparator = " | ";

        /// <summary>
        /// Builds a plain-text table from headers and rows.
        /// </summary>
        public static string BuildTextTable(IEnumerable<string> headerTexts, IEnumerable<NotificationTableRow> rows)
        {
            StringBuilder builder = new();
            builder.AppendLine(string.Join(kTextColumnSeparator, headerTexts.Select(NormalizeTextCell)));
            foreach (NotificationTableRow row in rows)
            {
                builder.AppendLine(string.Join(kTextColumnSeparator, row.TextCells));
            }
            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// Builds an HTML table from headers and rows.
        /// </summary>
        public static string BuildHtmlTable(IEnumerable<string> headerTexts, IEnumerable<NotificationTableRow> rows, IEnumerable<int>? rawHtmlColumnIndexes = null)
        {
            HashSet<int> rawColumns = rawHtmlColumnIndexes?.ToHashSet() ?? [];
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
            foreach (NotificationTableRow row in rows)
            {
                builder.Append("<tr>");
                for (int index = 0; index < row.HtmlCells.Count; ++index)
                {
                    builder.Append(rawColumns.Contains(index) ? HtmlRawCell(row.HtmlCells[index]) : HtmlCell(row.HtmlCells[index]));
                }
                builder.Append("</tr>");
            }

            builder.Append("</tbody></table>");
            return builder.ToString();
        }

        /// <summary>
        /// Replaces a table placeholder in a text body, preserving surrounding text sections.
        /// </summary>
        public static string BuildTextBody(string introText, string textTable, string tablePlaceholder = Placeholder.RULE_TABLE)
        {
            StringBuilder builder = new();
            string[] bodyParts = introText.Split(tablePlaceholder, StringSplitOptions.None);
            for (int index = 0; index < bodyParts.Length; ++index)
            {
                AppendTextSection(builder, bodyParts[index]);
                if (index < bodyParts.Length - 1)
                {
                    AppendTextSection(builder, textTable);
                }
            }
            return builder.ToString().Trim();
        }

        /// <summary>
        /// Replaces a table placeholder in an HTML body, preserving surrounding text sections.
        /// </summary>
        public static string BuildHtmlBody(string introText, string htmlTable, string tablePlaceholder = Placeholder.RULE_TABLE)
        {
            StringBuilder builder = new();
            string[] bodyParts = introText.Split(tablePlaceholder, StringSplitOptions.None);
            for (int index = 0; index < bodyParts.Length; ++index)
            {
                AppendEncodedParagraph(builder, bodyParts[index]);
                if (index < bodyParts.Length - 1)
                {
                    builder.Append(htmlTable);
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Wraps HTML body content in the standard notification HTML document.
        /// </summary>
        public static string BuildHtmlDocument(string body)
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

        /// <summary>
        /// Builds a framed report section for HTML notification attachments.
        /// </summary>
        public static string BuildHtmlReportSection(string title, string body, string? ownerName, string generatedOnText, string ownersText)
        {
            StringBuilder html = new();

            html.Append("<h2>")
                .Append(WebUtility.HtmlEncode(title))
                .AppendLine("</h2>")
                .Append("<p>")
                .Append(WebUtility.HtmlEncode(generatedOnText))
                .Append(": ")
                .Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssK"))
                .AppendLine(" (UTC)</p>");

            if (!string.IsNullOrWhiteSpace(ownerName))
            {
                html.Append("<p>")
                    .Append(WebUtility.HtmlEncode(ownersText))
                    .Append(": ")
                    .Append(WebUtility.HtmlEncode(ownerName))
                    .AppendLine("</p>");
            }

            html.AppendLine("<hr>")
                .Append(body);

            return html.ToString();
        }

        /// <summary>
        /// Builds a single HTML table cell with escaped content.
        /// </summary>
        public static string HtmlCell(string? value)
        {
            return $"<td>{WebUtility.HtmlEncode(value ?? "")}</td>";
        }

        /// <summary>
        /// Normalizes HTML-ish table content for plain-text output.
        /// </summary>
        public static string NormalizeTextCell(string? value)
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

        private static string HtmlRawCell(string? value)
        {
            return $"<td>{value ?? ""}</td>";
        }

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
    }

    /// <summary>
    /// Holds display values for a notification table row.
    /// </summary>
    public sealed class NotificationTableRow
    {
        /// <summary>
        /// Cell values rendered for the HTML table.
        /// </summary>
        public List<string?> HtmlCells { get; set; } = [];

        /// <summary>
        /// Cell values rendered for the plain-text table.
        /// </summary>
        public List<string> TextCells { get; set; } = [];
    }
}
