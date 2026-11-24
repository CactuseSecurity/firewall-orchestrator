using AngleSharp.Dom;
using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using static FWO.Test.UiRequestWorkflowTest;

namespace FWO.Test
{
    /// <summary>
    /// Display of the pre-workflow ticket reference in the ticket dialog, the ticket list and the task view headlines.
    /// </summary>
    [TestFixture]
    internal class UiPreWorkflowTicketReferenceTest
    {
        private const string kLabel = "ChangeId";
        private const string kReference = "CHG-42";

        private static RequestWorkflowUserConfig CreateUserConfig(string label)
        {
            return new RequestWorkflowUserConfig { ReqPreWorkflowTicketReferenceLabel = label };
        }

        private static IRenderedComponent<DisplayTicket> RenderTicketDialog(BunitContext context, string label, string? reference)
        {
            WfHandler handler = new()
            {
                ReadOnlyMode = true,
                DisplayTicketMode = true,
                ActTicket = new WfTicket { Id = 10, Title = "Open port", PreWorkflowTicketReference = reference }
            };
            context.Services.AddSingleton<UserConfig>(CreateUserConfig(label));
            return RenderDisplayTicket(context, handler, WorkflowPhases.request, new WfStateDict());
        }

        private static IElement? FindReferenceLabel(IRenderedComponent<DisplayTicket> component)
        {
            return component.FindAll("label").FirstOrDefault(label => label.TextContent.Trim() == $"{kLabel}:");
        }

        private static string? ReferenceCellText(BunitContext context, string? reference)
        {
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(),
                new WfTicket { Id = 1, Title = "Open port", PreWorkflowTicketReference = reference });
            IRenderedComponent<DisplayTicketTable> component = RenderDisplayTicketTableWithUserConfig(
                context, handler, WorkflowPhases.request, new WfStateDict(), CreateUserConfig(kLabel));

            List<string> headerTexts = [.. component.FindAll("thead th").Select(header => header.TextContent.Trim())];
            int referenceColumn = headerTexts.IndexOf(kLabel);
            if (referenceColumn < 0)
            {
                return null;
            }
            return component.FindAll("tbody tr")[0].QuerySelectorAll("td")[referenceColumn].TextContent.Trim();
        }

        [Test]
        public async Task DisplayTicket_WithLabel_ShowsReferenceBehindShortenedTitle()
        {
            await using BunitContext context = new();

            IRenderedComponent<DisplayTicket> component = RenderTicketDialog(context, kLabel, kReference);

            IElement? referenceLabel = FindReferenceLabel(component);
            Assert.Multiple(() =>
            {
                Assert.That(referenceLabel, Is.Not.Null);
                Assert.That(referenceLabel?.NextElementSibling?.TextContent.Trim(), Is.EqualTo(kReference));
                Assert.That(component.Find("#ticketTitle").ParentElement?.ClassList, Does.Contain("col-sm-6"));
            });
        }

        [TestCase(null)]
        [TestCase("")]
        public async Task DisplayTicket_WithLabelAndEmptyReference_ShowsDash(string? reference)
        {
            await using BunitContext context = new();

            IRenderedComponent<DisplayTicket> component = RenderTicketDialog(context, kLabel, reference);

            Assert.That(FindReferenceLabel(component)?.NextElementSibling?.TextContent.Trim(), Is.EqualTo("-"));
        }

        [Test]
        public async Task DisplayTicket_WithoutLabel_HidesReferenceAndKeepsFullTitleWidth()
        {
            await using BunitContext context = new();

            IRenderedComponent<DisplayTicket> component = RenderTicketDialog(context, "", kReference);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Not.Contain(kReference));
                Assert.That(component.Find("#ticketTitle").ParentElement?.ClassList, Does.Contain("col-sm-9"));
            });
        }

        [TestCase(kReference, kReference)]
        [TestCase(null, "-")]
        public async Task DisplayTicketTable_WithLabel_ShowsReferenceColumnAfterId(string? reference, string expected)
        {
            await using BunitContext context = new();

            string? cellText = ReferenceCellText(context, reference);

            Assert.That(cellText, Is.EqualTo(expected));
        }

        [Test]
        public async Task DisplayTicketTable_ReferenceColumnFollowsIdColumn()
        {
            await using BunitContext context = new();
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(), new WfTicket { Id = 1 });
            RequestWorkflowUserConfig userConfig = CreateUserConfig(kLabel);

            IRenderedComponent<DisplayTicketTable> component = RenderDisplayTicketTableWithUserConfig(
                context, handler, WorkflowPhases.request, new WfStateDict(), userConfig);

            List<string> headerTexts = [.. component.FindAll("thead th").Select(header => header.TextContent.Trim())];
            int idColumn = headerTexts.IndexOf(userConfig.GetText("id"));
            Assert.That(headerTexts.IndexOf(kLabel), Is.EqualTo(idColumn + 1));
        }

        [Test]
        public async Task DisplayTicketTable_WithoutLabel_HasNoReferenceColumn()
        {
            await using BunitContext context = new();
            WfHandler handler = CreateWorkflowHandler(WorkflowPhases.request, WfTaskType.access.ToString(),
                new WfTicket { Id = 1, PreWorkflowTicketReference = kReference });

            IRenderedComponent<DisplayTicketTable> component = RenderDisplayTicketTableWithUserConfig(
                context, handler, WorkflowPhases.request, new WfStateDict(), CreateUserConfig(""));

            Assert.That(component.Markup, Does.Not.Contain(kReference));
        }

        [TestCase(kLabel, kReference, "{0} CHG-42: Open port (")]
        [TestCase("", kReference, "{0}: Open port (")]
        [TestCase(kLabel, null, "{0}: Open port (")]
        public void ApprovalTaskDetailsView_HeadlineContainsReferenceOnlyWhenActive(string label, string? reference, string expectedStart)
        {
            RequestWorkflowUserConfig userConfig = CreateUserConfig(label);
            ApprovalTaskDetailsView component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(ApprovalTaskDetailsView.WfHandler),
                new WfHandler { ActTicket = new WfTicket { Id = 5, Title = "Open port", PreWorkflowTicketReference = reference } });

            string title = GetMember<string>(component, "ApprovalTaskViewTitle");

            Assert.That(title, Does.StartWith(string.Format(expectedStart, userConfig.GetText("ticket"))));
        }

        [TestCase(kLabel, kReference, "{0} CHG-42: Open port (")]
        [TestCase("", kReference, "{0}: Open port (")]
        public void PlanningDetailsView_HeadlineContainsReferenceOnlyWhenActive(string label, string? reference, string expectedStart)
        {
            RequestWorkflowUserConfig userConfig = CreateUserConfig(label);
            PlanningDetailsView component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(PlanningDetailsView.WfHandler),
                new WfHandler { ActTicket = new WfTicket { Id = 5, Title = "Open port", PreWorkflowTicketReference = reference } });

            string title = GetMember<string>(component, "PlanningViewTitle");

            Assert.That(title, Does.StartWith(string.Format(expectedStart, userConfig.GetText("ticket"))));
        }
    }
}
