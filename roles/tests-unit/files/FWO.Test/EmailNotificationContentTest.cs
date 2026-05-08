using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class EmailNotificationContentTest
    {
        [Test]
        public void BuildHtmlTableEscapesCellsAndAllowsRawColumns()
        {
            NotificationTableRow row = new()
            {
                HtmlCells = ["A&B", "<b>raw</b>"],
                TextCells = ["A&B", "raw"]
            };

            string html = NotificationTableBodyBuilder.BuildHtmlTable(["H&1", "H2"], [row], [1]);

            Assert.That(html, Does.Contain("<th>H&amp;1</th>"));
            Assert.That(html, Does.Contain("<td>A&amp;B</td>"));
            Assert.That(html, Does.Contain("<td><b>raw</b></td>"));
        }

        [Test]
        public void NormalizeTextCellRemovesHtmlAndDecodesEntities()
        {
            string text = NotificationTableBodyBuilder.NormalizeTextCell("alpha<br><span>beta</span>&amp; gamma");

            Assert.That(text, Is.EqualTo("alpha, beta& gamma"));
        }

        [Test]
        public void BuildBodyUsesLayoutSpecificContent()
        {
            NotificationEmailLayoutContent content = new()
            {
                PlainText = "plain content",
                Html = "<strong>html content</strong>"
            };
            FwoNotification htmlNotification = new()
            {
                Layout = NotificationLayout.HtmlInBody,
                EmailBody = $"before {Placeholder.CONTENT} after"
            };
            FwoNotification textNotification = new()
            {
                Layout = NotificationLayout.SimpleText,
                EmailBody = Placeholder.CONTENT
            };
            FwoNotification attachmentNotification = new()
            {
                Layout = NotificationLayout.CsvAsAttachment,
                EmailBody = $"before {Placeholder.CONTENT} after"
            };

            Assert.That(NotificationEmailLayoutHelper.BuildBody(htmlNotification, content), Is.EqualTo("before <strong>html content</strong> after"));
            Assert.That(NotificationEmailLayoutHelper.BuildBody(textNotification, content), Is.EqualTo("plain content"));
            Assert.That(NotificationEmailLayoutHelper.BuildBody(attachmentNotification, content), Is.EqualTo("before  after"));
        }

        [Test]
        public async Task BuildAttachmentCreatesCsvAttachment()
        {
            NotificationEmailLayoutContent content = new() { Csv = "a,b" };

            FormFile? attachment = await NotificationEmailLayoutHelper.BuildAttachment(NotificationLayout.CsvAsAttachment, content, "Subject Line");

            Assert.That(attachment, Is.Not.Null);
            Assert.That(attachment!.ContentType, Is.EqualTo("application/csv"));
            Assert.That(attachment.FileName, Does.StartWith("SubjectLine_"));
            Assert.That(await ReadFormFile(attachment), Is.EqualTo("a,b"));
        }

        [Test]
        public void FromRequestTasksBuildsTextHtmlCsvAndJsonContent()
        {
            WfReqTask task = new()
            {
                Id = 7,
                TaskNumber = 101,
                Title = "Open web",
                RequestAction = RequestAction.create.ToString(),
                Elements =
                {
                    new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "src-a" },
                    new WfReqElement { Field = ElemFieldType.destination.ToString(), IpString = "10.0.0.1" },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 80, PortEnd = 443, ProtoId = 6 }
                }
            };
            WfReqTask emptyTask = new() { Id = 8, TaskNumber = 102, Title = "Empty" };

            WorkflowEmailContent content = WorkflowEmailContent.FromRequestTasks([task, emptyTask], new EmailNotificationUserConfig());

            Assert.That(content.PlainText, Does.Contain("Requested Connections"));
            Assert.That(content.PlainText, Does.Contain("101 | Open web | create | src-a | 10.0.0.1 | 80-443/6"));
            Assert.That(content.PlainText, Does.Not.Contain("Empty"));
            Assert.That(content.Html, Does.Contain("<h2>Requested Connections</h2>"));
            Assert.That(content.Csv, Does.Contain("\"101\",\"Open web\",\"create\",\"src-a\",\"10.0.0.1\",\"80-443/6\""));
            Assert.That(content.Json, Does.Contain("\"Source\":\"src-a\""));
        }

        [Test]
        public void FromRequestTasksBuildsSeparateGroupSectionWithMembers()
        {
            WfReqTask accessTask = new()
            {
                Id = 7,
                TaskNumber = 101,
                Title = "Open web",
                RequestAction = RequestAction.create.ToString(),
                Elements =
                {
                    new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "src-a" },
                    new WfReqElement { Field = ElemFieldType.destination.ToString(), IpString = "10.0.0.1" },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), GroupName = "WebServices" }
                }
            };
            WfReqTask groupTask = new()
            {
                Id = 8,
                TaskNumber = 102,
                TaskType = WfTaskType.group_create.ToString(),
                Title = "New App Role",
                RequestAction = RequestAction.create.ToString(),
                Elements =
                {
                    new WfReqElement { Field = ElemFieldType.source.ToString(), GroupName = "AR1", IpString = "10.0.0.2" }
                }
            };
            WfReqTask modifyGroupTask = new()
            {
                Id = 9,
                TaskNumber = 103,
                TaskType = WfTaskType.group_modify.ToString(),
                Title = "Update App Role",
                RequestAction = RequestAction.modify.ToString(),
                Elements =
                {
                    new WfReqElement { Field = ElemFieldType.source.ToString(), RequestAction = RequestAction.addAfterCreation.ToString(), GroupName = "AR1", Name = "Server2" }
                },
                RemovedElements =
                {
                    new WfReqElement { Field = ElemFieldType.source.ToString(), RequestAction = RequestAction.delete.ToString(), GroupName = "AR1", IpString = "10.0.0.3" }
                }
            };

            WorkflowEmailContent content = WorkflowEmailContent.FromRequestTasks([groupTask, accessTask, modifyGroupTask], new EmailNotificationUserConfig());

            Assert.That(content.PlainText, Does.Contain("Requested Connections"));
            Assert.That(content.PlainText, Does.Contain("101 | Open web | create | src-a | 10.0.0.1 | WebServices"));
            Assert.That(content.PlainText, Does.Contain("Group Requests"));
            Assert.That(content.PlainText, Does.Contain("Task | Type | Title | Action | Members"));
            Assert.That(content.PlainText, Does.Contain("102 | Create Group | New App Role | create | 10.0.0.2"));
            Assert.That(content.PlainText, Does.Contain("103 | Modify Group | Update App Role | modify | addAfterCreation: Server2, delete: 10.0.0.3"));
            Assert.That(content.Html, Does.Contain("<h2>Group Requests</h2>"));
            Assert.That(content.Csv, Does.Contain("\"102\",\"Create Group\",\"New App Role\",\"create\",\"10.0.0.2\""));
            Assert.That(content.Json, Does.Contain("\"Members\":\"10.0.0.2\""));
        }

        private static async Task<string> ReadFormFile(FormFile formFile)
        {
            using Stream stream = formFile.OpenReadStream();
            using StreamReader reader = new(stream);
            return await reader.ReadToEndAsync();
        }

        private sealed class EmailNotificationUserConfig : SimulatedUserConfig
        {
            private static readonly Dictionary<string, string> Translations = new()
            {
                { "requested_connections", "Requested Connections" },
                { "task", "Task" }
            };

            public override string GetText(string key)
            {
                return Translations.TryGetValue(key, out string? value) ? value : base.GetText(key);
            }
        }
    }
}
