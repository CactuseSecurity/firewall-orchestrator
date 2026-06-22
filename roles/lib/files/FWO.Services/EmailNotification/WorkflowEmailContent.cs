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
            return FromRequestTasks(tasks, userConfig, null);
        }

        /// <summary>
        /// Builds requested connection content from request tasks.
        /// </summary>
        public static WorkflowEmailContent FromRequestTasks(IEnumerable<WfReqTask> tasks, UserConfig userConfig, Dictionary<int, string>? protocolNamesById)
        {
            List<WfReqTask> taskList = [.. tasks];
            List<WorkflowEmailSection> sections = [];
            List<WorkflowConnectionRow> accessRows = [.. taskList.Where(task => !IsGroupTask(task)).Select(task => BuildRequestRow(task, protocolNamesById)).Where(row => row.HasContent())];
            if (accessRows.Count > 0)
            {
                sections.Add(BuildAccessSection(accessRows, userConfig));
            }
            List<WorkflowGroupRow> groupRows = [.. taskList.Where(IsGroupTask).Select(task => BuildGroupRequestRow(task, userConfig, protocolNamesById)).Where(row => row.HasContent())];
            if (groupRows.Count > 0)
            {
                sections.Add(BuildGroupSection(groupRows, userConfig));
            }
            return FromSections(sections);
        }

        /// <summary>
        /// Builds requested connection content from implementation tasks.
        /// </summary>
        public static WorkflowEmailContent FromImplementationTasks(IEnumerable<WfImplTask> tasks, UserConfig userConfig)
        {
            return FromImplementationTasks(tasks, userConfig, null);
        }

        /// <summary>
        /// Builds requested connection content from implementation tasks.
        /// </summary>
        public static WorkflowEmailContent FromImplementationTasks(IEnumerable<WfImplTask> tasks, UserConfig userConfig, Dictionary<int, string>? protocolNamesById)
        {
            List<WorkflowConnectionRow> rows = [.. tasks.Select(task => BuildImplementationRow(task, protocolNamesById)).Where(row => row.HasContent())];
            WorkflowEmailSection section = BuildAccessSection(rows, userConfig);
            return FromSections(section.Rows.Count > 0 ? [section] : []);
        }

        private static WorkflowEmailContent FromSections(List<WorkflowEmailSection> sections)
        {
            string plainText = string.Join("\n\n", sections.Select(section => $"{section.Title}\n{NotificationTableBodyBuilder.BuildTextTable(section.Headers, section.TableRows())}"));
            string bodyHtml = string.Join("", sections.Select(section => $"<h2>{WebUtility.HtmlEncode(section.Title)}</h2>{NotificationTableBodyBuilder.BuildHtmlTable(section.Headers, section.TableRows())}"));

            return new()
            {
                PlainText = plainText.Trim(),
                Html = NotificationTableBodyBuilder.BuildHtmlDocument(bodyHtml),
                Csv = BuildCsv(sections),
                Json = BuildJson(sections)
            };
        }

        private static WorkflowEmailSection BuildAccessSection(List<WorkflowConnectionRow> rows, UserConfig userConfig)
        {
            return new()
            {
                Title = userConfig.GetText("requested_connections"),
                Headers =
                [
                    userConfig.GetText("task"),
                    userConfig.GetText("title"),
                    userConfig.GetText("action"),
                    userConfig.GetText("source"),
                    userConfig.GetText("destination"),
                    userConfig.GetText("services")
                ],
                Rows = [.. rows]
            };
        }

        private static WorkflowEmailSection BuildGroupSection(List<WorkflowGroupRow> rows, UserConfig userConfig)
        {
            return new()
            {
                Title = userConfig.GetText("group_requests"),
                Headers =
                [
                    userConfig.GetText("task"),
                    userConfig.GetText("type"),
                    userConfig.GetText("title"),
                    userConfig.GetText("action"),
                    userConfig.GetText("members")
                ],
                Rows = [.. rows]
            };
        }

        private static WorkflowConnectionRow BuildRequestRow(WfReqTask task, Dictionary<int, string>? protocolNamesById)
        {
            return new()
            {
                Task = BuildTaskReference(task.TaskNumber, task.Id),
                Title = task.Title,
                Action = task.RequestAction,
                Source = BuildElementList(task.Elements, ElemFieldType.source, protocolNamesById),
                Destination = BuildElementList(task.Elements, ElemFieldType.destination, protocolNamesById),
                Services = BuildElementList(task.Elements, ElemFieldType.service, protocolNamesById)
            };
        }

        private static WorkflowConnectionRow BuildImplementationRow(WfImplTask task, Dictionary<int, string>? protocolNamesById)
        {
            return new()
            {
                Task = BuildTaskReference(task.TaskNumber, task.Id),
                Title = task.Title,
                Action = task.ImplAction,
                Source = BuildElementList(task.ImplElements, ElemFieldType.source, protocolNamesById),
                Destination = BuildElementList(task.ImplElements, ElemFieldType.destination, protocolNamesById),
                Services = BuildElementList(task.ImplElements, ElemFieldType.service, protocolNamesById)
            };
        }

        private static WorkflowGroupRow BuildGroupRequestRow(WfReqTask task, UserConfig userConfig, Dictionary<int, string>? protocolNamesById)
        {
            return new()
            {
                Task = BuildTaskReference(task.TaskNumber, task.Id),
                Type = userConfig.GetText(task.TaskType),
                Title = task.Title,
                Action = task.RequestAction,
                Members = BuildGroupMemberList(task, protocolNamesById)
            };
        }

        private static bool IsGroupTask(WfTaskBase task)
        {
            return task.TaskType == WfTaskType.group_create.ToString()
                || task.TaskType == WfTaskType.group_modify.ToString()
                || task.TaskType == WfTaskType.group_delete.ToString();
        }

        private static string BuildTaskReference(int taskNumber, long id)
        {
            return taskNumber > 0 ? taskNumber.ToString() : id.ToString();
        }

        private static string BuildElementList(IEnumerable<WfElementBase> elements, ElemFieldType field, Dictionary<int, string>? protocolNamesById)
        {
            return string.Join(", ", elements
                .Where(element => element.Field == field.ToString())
                .Select(element => BuildElementText(element, protocolNamesById))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct());
        }

        private static string BuildGroupMemberList(WfReqTask task, Dictionary<int, string>? protocolNamesById)
        {
            List<WfElementBase> elements = [.. task.Elements, .. task.RemovedElements];
            return string.Join(", ", elements
                .Select(element => BuildGroupMemberText(element, protocolNamesById))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct());
        }

        private static string BuildGroupMemberText(WfElementBase element, Dictionary<int, string>? protocolNamesById)
        {
            string memberText = element.Field == ElemFieldType.service.ToString()
                ? BuildGroupServiceMemberText(element, protocolNamesById)
                : BuildGroupObjectMemberText(element);
            string elementAction = GetElementAction(element);
            if (string.IsNullOrWhiteSpace(elementAction) || elementAction == RequestAction.create.ToString())
            {
                return memberText;
            }
            return string.IsNullOrWhiteSpace(memberText) ? elementAction : $"{elementAction}: {memberText}";
        }

        private static string GetElementAction(WfElementBase element)
        {
            return element switch
            {
                WfReqElement reqElement => reqElement.RequestAction,
                WfImplElement implElement => implElement.ImplAction,
                _ => ""
            };
        }

        private static string BuildGroupObjectMemberText(WfElementBase element)
        {
            string displayName = FirstNonEmpty(element.Name, element.RuleUid, BuildIpRange(element), element.GroupName);
            if (!string.IsNullOrWhiteSpace(element.RuleUid) && element.RuleUid != displayName)
            {
                return $"{displayName} ({element.RuleUid})";
            }
            return displayName;
        }

        private static string BuildGroupServiceMemberText(WfElementBase element, Dictionary<int, string>? protocolNamesById)
        {
            string displayName = FirstNonEmpty(element.Name, BuildPortRange(element), element.GroupName);
            if (element.ProtoId != null && string.IsNullOrWhiteSpace(element.Name))
            {
                return $"{displayName}/{GetProtocolLabel(element.ProtoId.Value, protocolNamesById)}";
            }
            return displayName;
        }

        private static string BuildElementText(WfElementBase element, Dictionary<int, string>? protocolNamesById)
        {
            return element.Field == ElemFieldType.service.ToString()
                ? BuildServiceText(element, protocolNamesById)
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

        private static string BuildServiceText(WfElementBase element, Dictionary<int, string>? protocolNamesById)
        {
            string displayName = FirstNonEmpty(element.Name, element.GroupName, BuildPortRange(element));
            if (element.ProtoId != null && string.IsNullOrWhiteSpace(element.Name))
            {
                return $"{displayName}/{GetProtocolLabel(element.ProtoId.Value, protocolNamesById)}";
            }
            return displayName;
        }

        private static string GetProtocolLabel(int protocolId, Dictionary<int, string>? protocolNamesById)
        {
            return protocolNamesById != null && protocolNamesById.TryGetValue(protocolId, out string? protocolName) && !string.IsNullOrWhiteSpace(protocolName)
                ? protocolName
                : protocolId.ToString();
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

        private static string BuildCsv(List<WorkflowEmailSection> sections)
        {
            StringBuilder csv = new();
            foreach (WorkflowEmailSection section in sections)
            {
                if (csv.Length > 0)
                {
                    csv.AppendLine();
                }
                csv.AppendLine(CsvCell(section.Title));
                csv.AppendLine(string.Join(",", section.Headers.Select(CsvCell)));
                foreach (IWorkflowEmailRow row in section.Rows)
                {
                    csv.AppendLine(string.Join(",", row.ToCells().Select(CsvCell)));
                }
            }
            return csv.ToString();
        }

        private static string BuildJson(List<WorkflowEmailSection> sections)
        {
            return JsonSerializer.Serialize(sections.Select(section => new
            {
                section.Title,
                section.Headers,
                Rows = section.Rows.Select(row => row.ToJsonObject()).ToList()
            }));
        }

        private static string CsvCell(string value)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }

    internal class WorkflowEmailSection
    {
        public string Title { get; set; } = "";
        public List<string> Headers { get; set; } = [];
        public List<IWorkflowEmailRow> Rows { get; set; } = [];

        public List<NotificationTableRow> TableRows()
        {
            return [.. Rows.Select(row => row.ToTableRow())];
        }
    }

    internal interface IWorkflowEmailRow
    {
        bool HasContent();
        List<string> ToCells();
        NotificationTableRow ToTableRow();
        object ToJsonObject();
    }

    internal class WorkflowConnectionRow : IWorkflowEmailRow
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

        public object ToJsonObject()
        {
            return this;
        }
    }

    internal class WorkflowGroupRow : IWorkflowEmailRow
    {
        public string Task { get; set; } = "";
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string Action { get; set; } = "";
        public string Members { get; set; } = "";

        public bool HasContent()
        {
            return !string.IsNullOrWhiteSpace(Title)
                || !string.IsNullOrWhiteSpace(Members);
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
            return [Task, Type, Title, Action, Members];
        }

        public object ToJsonObject()
        {
            return this;
        }
    }
}
