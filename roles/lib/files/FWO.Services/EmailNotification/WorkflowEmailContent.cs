using FWO.Config.Api;
using FWO.Data.Workflow;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FWO.Services
{
    /// <summary>
    /// Builds workflow action email content from the workflow ticket data already in scope.
    /// </summary>
    public class WorkflowEmailContent : NotificationEmailLayoutContent
    {
        /// <summary>
        /// Builds requested connection content from request tasks.
        /// </summary>
        public static WorkflowEmailContent FromRequestTasks(IEnumerable<WfReqTask> tasks, UserConfig userConfig)
        {
            List<WorkflowConnectionRow> rows = [.. tasks.Select(BuildRequestRow).Where(row => row.HasContent())];
            return FromRows(rows, userConfig);
        }

        /// <summary>
        /// Builds requested connection content from implementation tasks.
        /// </summary>
        public static WorkflowEmailContent FromImplementationTasks(IEnumerable<WfImplTask> tasks, UserConfig userConfig)
        {
            List<WorkflowConnectionRow> rows = [.. tasks.Select(BuildImplementationRow).Where(row => row.HasContent())];
            return FromRows(rows, userConfig);
        }

        private static WorkflowEmailContent FromRows(List<WorkflowConnectionRow> rows, UserConfig userConfig)
        {
            string title = userConfig.GetText("requested_connections");
            List<string> headers = BuildHeaders(userConfig);
            List<NotificationTableRow> tableRows = [.. rows.Select(row => row.ToTableRow())];
            string tableText = NotificationTableBodyBuilder.BuildTextTable(headers, tableRows);
            string tableHtml = NotificationTableBodyBuilder.BuildHtmlTable(headers, tableRows);
            string bodyHtml = $"<h2>{WebUtility.HtmlEncode(title)}</h2>{tableHtml}";

            return new()
            {
                PlainText = NotificationTableBodyBuilder.BuildTextBody($"{title}\n@@CONTENT@@", tableText, "@@CONTENT@@"),
                Html = NotificationTableBodyBuilder.BuildHtmlDocument(bodyHtml),
                Csv = BuildCsv(headers, rows),
                Json = JsonSerializer.Serialize(rows)
            };
        }

        private static List<string> BuildHeaders(UserConfig userConfig)
        {
            return
            [
                userConfig.GetText("task"),
                userConfig.GetText("title"),
                userConfig.GetText("action"),
                userConfig.GetText("source"),
                userConfig.GetText("destination"),
                userConfig.GetText("services")
            ];
        }

        private static WorkflowConnectionRow BuildRequestRow(WfReqTask task)
        {
            return new()
            {
                Task = BuildTaskReference(task.TaskNumber, task.Id),
                Title = task.Title,
                Action = task.RequestAction,
                Source = BuildElementList(task.Elements, ElemFieldType.source),
                Destination = BuildElementList(task.Elements, ElemFieldType.destination),
                Services = BuildElementList(task.Elements, ElemFieldType.service)
            };
        }

        private static WorkflowConnectionRow BuildImplementationRow(WfImplTask task)
        {
            return new()
            {
                Task = BuildTaskReference(task.TaskNumber, task.Id),
                Title = task.Title,
                Action = task.ImplAction,
                Source = BuildElementList(task.ImplElements, ElemFieldType.source),
                Destination = BuildElementList(task.ImplElements, ElemFieldType.destination),
                Services = BuildElementList(task.ImplElements, ElemFieldType.service)
            };
        }

        private static string BuildTaskReference(int taskNumber, long id)
        {
            return taskNumber > 0 ? taskNumber.ToString() : id.ToString();
        }

        private static string BuildElementList(IEnumerable<WfElementBase> elements, ElemFieldType field)
        {
            return string.Join(", ", elements
                .Where(element => element.Field == field.ToString())
                .Select(BuildElementText)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct());
        }

        private static string BuildElementText(WfElementBase element)
        {
            return element.Field == ElemFieldType.service.ToString()
                ? BuildServiceText(element)
                : BuildObjectOrRuleText(element);
        }

        private static string BuildObjectOrRuleText(WfElementBase element)
        {
            string displayName = FirstNonEmpty(element.Name, element.GroupName, element.RuleUid, BuildIpRange(element));
            if (!string.IsNullOrWhiteSpace(element.RuleUid) && element.RuleUid != displayName)
            {
                return $"{displayName} ({element.RuleUid})";
            }
            return displayName;
        }

        private static string BuildServiceText(WfElementBase element)
        {
            string displayName = FirstNonEmpty(element.Name, BuildPortRange(element));
            if (element.ProtoId != null && string.IsNullOrWhiteSpace(element.Name))
            {
                return $"{displayName}/{element.ProtoId}";
            }
            return displayName;
        }

        private static string BuildIpRange(WfElementBase element)
        {
            string start = FirstNonEmpty(element.IpString, GetCidrString(element, false));
            string end = FirstNonEmpty(element.IpEnd, GetCidrString(element, true));
            return !string.IsNullOrWhiteSpace(end) && end != start ? $"{start}-{end}" : start;
        }

        private static string GetCidrString(WfElementBase element, bool end)
        {
            return element switch
            {
                WfReqElement reqElement => end ? reqElement.CidrEnd?.CidrString ?? "" : reqElement.Cidr?.CidrString ?? "",
                WfImplElement implElement => end ? implElement.CidrEnd?.CidrString ?? "" : implElement.Cidr?.CidrString ?? "",
                _ => ""
            };
        }

        private static string BuildPortRange(WfElementBase element)
        {
            if (element.Port == null)
            {
                return "";
            }

            return element.PortEnd != null && element.PortEnd != element.Port
                ? $"{element.Port}-{element.PortEnd}"
                : element.Port.ToString() ?? "";
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }

        private static string BuildCsv(List<string> headers, List<WorkflowConnectionRow> rows)
        {
            StringBuilder csv = new();
            csv.AppendLine(string.Join(",", headers.Select(CsvCell)));
            foreach (WorkflowConnectionRow row in rows)
            {
                csv.AppendLine(string.Join(",", row.ToCells().Select(CsvCell)));
            }
            return csv.ToString();
        }

        private static string CsvCell(string value)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }

    internal class WorkflowConnectionRow
    {
        public string Task { get; set; } = "";
        public string Title { get; set; } = "";
        public string Action { get; set; } = "";
        public string Source { get; set; } = "";
        public string Destination { get; set; } = "";
        public string Services { get; set; } = "";

        public bool HasContent()
        {
            return !string.IsNullOrWhiteSpace(Source)
                || !string.IsNullOrWhiteSpace(Destination)
                || !string.IsNullOrWhiteSpace(Services);
        }

        public NotificationTableRow ToTableRow()
        {
            List<string> cells = ToCells();
            return new()
            {
                HtmlCells = [.. cells],
                TextCells = cells
            };
        }

        public List<string> ToCells()
        {
            return [Task, Title, Action, Source, Destination, Services];
        }
    }
}
