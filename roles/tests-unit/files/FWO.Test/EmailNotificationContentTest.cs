using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Modelling;
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
        public void BuildTextTableJoinsHeadersAndRows()
        {
            NotificationTableRow row = new()
            {
                TextCells = ["a", "b"]
            };

            string text = NotificationTableBodyBuilder.BuildTextTable(["H1", "H2"], [row]);

            Assert.That(text, Is.EqualTo($"H1 | H2{Environment.NewLine}a | b"));
        }

        [Test]
        public void BuildHtmlBodyKeepsParagraphsAndInjectsTable()
        {
            string html = NotificationTableBodyBuilder.BuildHtmlBody($"First line\n\n{Placeholder.RULE_TABLE}\nTail", "<table>body</table>");

            Assert.That(html, Is.EqualTo("<p>First line<br><br></p><table>body</table><p><br>Tail</p>"));
        }

        [Test]
        public void BuildHtmlDocumentWrapsBodyWithStandardStyle()
        {
            string html = NotificationTableBodyBuilder.BuildHtmlDocument("<p>content</p>");

            Assert.That(html, Does.StartWith("<!DOCTYPE html>"));
            Assert.That(html, Does.Contain("<body>"));
            Assert.That(html, Does.Contain("font-family: arial, sans-serif;"));
            Assert.That(html, Does.Contain("<p>content</p>"));
        }

        [Test]
        public void BuildHtmlReportSectionIncludesTitleOwnerAndBody()
        {
            string html = NotificationTableBodyBuilder.BuildHtmlReportSection("Section Title", "<p>body</p>", "OwnerX", "Generated on", "Owners");

            Assert.That(html, Does.Contain("<h2>Section Title</h2>"));
            Assert.That(html, Does.Contain("<p>Generated on:"));
            Assert.That(html, Does.Contain("<p>Owners: OwnerX</p>"));
            Assert.That(html, Does.Contain("<p>body</p>"));
        }

        [Test]
        public void NotificationLayoutContentUsesLayoutSpecificBody()
        {
            NotificationEmailLayoutContent content = new()
            {
                PlainText = "plain content",
                Html = "<strong>html content</strong>"
            };

            Assert.That(content.BodyForLayout(NotificationLayout.SimpleText), Is.EqualTo("plain content"));
            Assert.That(content.BodyForLayout(NotificationLayout.HtmlInBody), Is.EqualTo("<strong>html content</strong>"));
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

            string htmlBody = NotificationEmailLayoutHelper.BuildBody(htmlNotification, content);
            Assert.That(htmlBody, Does.StartWith("before <style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100%;}"));
            Assert.That(htmlBody, Does.Contain("<strong>html content</strong>"));
            Assert.That(NotificationEmailLayoutHelper.BuildBody(textNotification, content), Is.EqualTo("plain content"));
            Assert.That(NotificationEmailLayoutHelper.BuildBody(attachmentNotification, content), Is.EqualTo("before  after"));
        }

        [Test]
        public void BuildBodyAppendsContentWhenPlaceholderIsMissing()
        {
            FwoNotification notification = new()
            {
                Layout = NotificationLayout.SimpleText,
                EmailBody = "header\n"
            };

            Assert.That(NotificationEmailLayoutHelper.BuildBody(notification, "details"), Is.EqualTo("header\ndetails"));
        }

        [Test]
        public void BuildBodyWithLayoutContentReturnsPlaceholderFreeBodyWhenContentIsMissing()
        {
            FwoNotification notification = new()
            {
                Layout = NotificationLayout.HtmlInBody,
                EmailBody = $"intro {Placeholder.CONTENT} tail"
            };

            Assert.That(NotificationEmailLayoutHelper.BuildBody(notification, (NotificationEmailLayoutContent?)null), Is.EqualTo("intro  tail"));
        }

        [Test]
        public void BuildBodyWithWorkflowHtmlInBodyPrefixesSharedTableStyle()
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
            WorkflowEmailContent content = WorkflowEmailContent.FromRequestTasks([task], new EmailNotificationUserConfig());
            FwoNotification notification = new()
            {
                Layout = NotificationLayout.HtmlInBody,
                EmailBody = "prefix "
            };

            string body = NotificationEmailLayoutHelper.BuildBody(notification, content);

            Assert.That(body, Does.StartWith("prefix <style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100%;}"));
            Assert.That(body, Does.Contain("<h2>Requested Connections</h2>"));
            Assert.That(body, Does.Contain("<table border=\"1\" cellspacing=\"0\" cellpadding=\"4\">"));
        }

        [Test]
        public async Task BuildAttachmentWrapsHtmlFragmentAndReturnsNullForMissingContent()
        {
            NotificationEmailLayoutContent content = new()
            {
                Html = "<p>html fragment</p>",
                Csv = "a,b",
                Json = "{}"
            };

            FormFile? htmlAttachment = await NotificationEmailLayoutHelper.BuildAttachment(NotificationLayout.HtmlAsAttachment, content, "Subject Line");
            FormFile? nullAttachment = await NotificationEmailLayoutHelper.BuildAttachment(NotificationLayout.HtmlAsAttachment, null, "Subject Line");

            Assert.That(htmlAttachment, Is.Not.Null);
            Assert.That(htmlAttachment!.ContentType, Is.EqualTo("application/html"));
            Assert.That(await ReadFormFile(htmlAttachment), Does.Contain("<!DOCTYPE html>"));
            Assert.That(await ReadFormFile(htmlAttachment), Does.Contain("<p>html fragment</p>"));
            Assert.That(nullAttachment, Is.Null);
        }

        [TestCase(NotificationLayout.HtmlAsAttachment, "application/html", "<p>html fragment</p>")]
        [TestCase(NotificationLayout.JsonAsAttachment, "application/json", "{\"k\":1}")]
        [TestCase(NotificationLayout.CsvAsAttachment, "application/csv", "a,b")]
        public async Task BuildAttachmentWithDelegatesUsesAllTextBasedLayouts(NotificationLayout layout, string expectedContentType, string expectedBody)
        {
            FormFile? attachment = await NotificationEmailLayoutHelper.BuildAttachment(
                layout,
                "Subject Line",
                () => "<p>html fragment</p>",
                () => "{\"k\":1}",
                () => "a,b",
                _ => Task.FromResult<string?>(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("pdf fragment"))));

            Assert.That(attachment, Is.Not.Null);
            Assert.That(attachment!.ContentType, Is.EqualTo(expectedContentType));
            Assert.That(await ReadFormFile(attachment), Is.EqualTo(expectedBody));
        }

        [Test]
        public async Task BuildAttachmentWithDelegatesUsesPdfLayoutAndNullForUnsupportedLayout()
        {
            FormFile? pdfAttachment = await NotificationEmailLayoutHelper.BuildAttachment(
                NotificationLayout.PdfAsAttachment,
                "Subject Line",
                () => "<p>html fragment</p>",
                () => "{\"k\":1}",
                () => "a,b",
                _ => Task.FromResult<string?>(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("pdf fragment"))));

            FormFile? unsupportedAttachment = await NotificationEmailLayoutHelper.BuildAttachment(
                NotificationLayout.SimpleText,
                "Subject Line",
                () => "<p>html fragment</p>",
                () => "{\"k\":1}",
                () => "a,b",
                _ => Task.FromResult<string?>(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("pdf fragment"))));

            Assert.That(pdfAttachment, Is.Not.Null);
            Assert.That(pdfAttachment!.ContentType, Is.EqualTo("application/octet-stream"));
            Assert.That(await ReadFormFile(pdfAttachment), Is.EqualTo("pdf fragment"));
            Assert.That(unsupportedAttachment, Is.Null);
        }

        [Test]
        public void ReplaceWorkflowPlaceholdersUsesTicketOwnerFallback()
        {
            WfTicket ticket = new()
            {
                Requester = new() { Name = "Requester" },
                Tasks =
                {
                    new WfReqTask
                    {
                        Owners =
                        {
                            new() { Owner = new() { Name = "Application", ExtAppId = "APP-1" } }
                        }
                    }
                }
            };

            string text = NotificationPlaceholderResolver.ReplaceWorkflowPlaceholders(
                $"{Placeholder.APPNAME}/{Placeholder.APPID}/{Placeholder.REQUESTER}", ticket, null);

            Assert.That(text, Is.EqualTo("Application/APP-1/Requester"));
        }

        [Test]
        public void NotificationRequestBuilderKeepsNetworkIpsWhenTicketIsSerialized()
        {
            ModellingNotificationRequestBuilder builder = new(new EmailNotificationUserConfig());
            ModellingConnection connection = new()
            {
                Id = 42,
                Name = "Interface based access",
                SourceAppServers =
                [
                    new() { Content = new() { Name = "Source", Ip = "10.0.0.1", IpEnd = "10.0.0.1" } }
                ],
                DestinationAppServers =
                [
                    new() { Content = new() { Name = "Destination", Ip = "10.0.0.2", IpEnd = "10.0.0.2" } }
                ]
            };

            List<WfReqTask> tasks = builder.BuildRequestTasks([connection], new() { Id = 7, Name = "App" }, 1);
            WfTicket ticket = new() { Tasks = tasks };
            ticket.UpdateCidrsInTaskElements();

            WfReqTask accessTask = tasks.First(task => task.TaskType == WfTaskType.access.ToString());
            Assert.That(accessTask.Elements.First(element => element.Field == ElemFieldType.source.ToString()).IpString, Is.EqualTo("10.0.0.1"));
            Assert.That(accessTask.Elements.First(element => element.Field == ElemFieldType.destination.ToString()).IpString, Is.EqualTo("10.0.0.2"));
            Assert.That(accessTask.Elements.First(element => element.Field == ElemFieldType.source.ToString()).Cidr?.CidrString, Is.EqualTo("10.0.0.1/32"));
        }

        [Test]
        public void NotificationRequestBuilderKeepsExistingGroupsInAccessTask()
        {
            ModellingNotificationRequestBuilder builder = new(new EmailNotificationUserConfig());
            ModellingConnection connection = new()
            {
                Id = 42,
                Name = "Interface based access",
                SourceAppRoles =
                [
                    new() { Content = new() { IdString = "AR-Source" } }
                ],
                DestinationAppRoles =
                [
                    new() { Content = new() { IdString = "AR-Destination" } }
                ],
                ServiceGroups =
                [
                    new() { Content = new() { Name = "SG-Web" } }
                ]
            };

            List<WfReqTask> tasks = builder.BuildRequestTasks([connection], new() { Id = 7, Name = "App" }, 1);
            WfReqTask accessTask = tasks.First(task => task.TaskType == WfTaskType.access.ToString());

            Assert.That(accessTask.Elements.Any(element => element.Field == ElemFieldType.source.ToString() && element.GroupName == "AR-Source"), Is.True);
            Assert.That(accessTask.Elements.Any(element => element.Field == ElemFieldType.destination.ToString() && element.GroupName == "AR-Destination"), Is.True);
            Assert.That(accessTask.Elements.Any(element => element.Field == ElemFieldType.service.ToString() && element.GroupName == "SG-Web"), Is.True);
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
        public void FromRequestTasksBuildsProtocolNamesFromSuppliedProtocolMap()
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
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Name = "1000/TCP", Port = 1000, ProtoId = 6 },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 4000, PortEnd = 5000, ProtoId = 17 }
                }
            };

            Dictionary<int, string> protocolNamesById = new()
            {
                { 6, "TCP" },
                { 17, "UDP" }
            };
            WorkflowEmailContent content = WorkflowEmailContent.FromRequestTasks([task], new EmailNotificationUserConfig(), protocolNamesById);

            Assert.That(content.PlainText, Does.Contain("1000/TCP, 4000-5000/UDP"));
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
