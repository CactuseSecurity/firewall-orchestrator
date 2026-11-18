using Bunit;
using FWO.Config.Api;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiApprovalComplianceButtonTest
    {
        private static readonly List<string> kReasons = ["forbidden service", "zone violation"];
        private static readonly ApprovalTaskCompliance kNotAllowed = new(false, kReasons);
        private static readonly ApprovalTaskCompliance kOk = new(true, []);

        [Test]
        public void Loading_ShowsDisabledPlaceholderWithAdditionalClasses()
        {
            using BunitContext context = CreateContext();

            IRenderedComponent<ApprovalComplianceButton> cut = context.Render<ApprovalComplianceButton>(parameters => parameters
                .Add(p => p.Loading, true)
                .Add(p => p.ShowNotChecked, true)
                .Add(p => p.Class, "btn-sm"));

            AngleSharp.Dom.IElement placeholder = cut.Find(".approval-compliance-button");
            Assert.Multiple(() =>
            {
                Assert.That(placeholder.TagName, Is.EqualTo("SPAN"));
                Assert.That(placeholder.ClassList, Does.Contain("approval-compliance-loading").And.Contain("disabled").And.Contain("btn-sm"));
                Assert.That(placeholder.TextContent, Is.EqualTo("..."));
                Assert.That(cut.FindAll("button"), Is.Empty);
                Assert.That(cut.FindAll(".approval-compliance-none"), Is.Empty);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecheckButton_IsDisabledWhileLoadingAndStartsTheRecheck(bool loading)
        {
            using BunitContext context = CreateContext();
            int rechecks = 0;

            IRenderedComponent<ApprovalComplianceRecheckButton> cut = context.Render<ApprovalComplianceRecheckButton>(parameters => parameters
                .Add(p => p.Loading, loading)
                .Add(p => p.Class, "mr-2")
                .Add(p => p.Recheck, () => rechecks++));

            AngleSharp.Dom.IElement button = cut.Find("button.approval-compliance-recheck");
            Assert.Multiple(() =>
            {
                Assert.That(button.ClassList, Does.Contain("mr-2"));
                Assert.That(button.HasAttribute("disabled"), Is.EqualTo(loading));
            });
            if (!loading)
            {
                button.Click();
                Assert.That(rechecks, Is.EqualTo(1));
            }
        }

        [Test]
        public void NotChecked_ShowsDashOnlyWhenRequested()
        {
            using BunitContext context = CreateContext();

            IRenderedComponent<ApprovalComplianceButton> withDash = context.Render<ApprovalComplianceButton>(parameters => parameters
                .Add(p => p.ShowNotChecked, true));
            IRenderedComponent<ApprovalComplianceButton> withoutDash = context.Render<ApprovalComplianceButton>();

            Assert.Multiple(() =>
            {
                Assert.That(withDash.Find(".approval-compliance-none").TextContent, Is.EqualTo("-"));
                Assert.That(withDash.FindAll(".approval-compliance-button"), Is.Empty);
                Assert.That(withoutDash.Markup.Trim(), Is.Empty);
            });
        }

        [Test]
        public void Result_TakesPrecedenceOverLoadingAndNotChecked()
        {
            using BunitContext context = CreateContext();

            IRenderedComponent<ApprovalComplianceButton> cut = context.Render<ApprovalComplianceButton>(parameters => parameters
                .Add(p => p.Compliance, kOk)
                .Add(p => p.Loading, true)
                .Add(p => p.ShowNotChecked, true)
                .Add(p => p.Class, "btn-sm"));

            AngleSharp.Dom.IElement ok = cut.Find(".approval-compliance-button");
            Assert.Multiple(() =>
            {
                Assert.That(ok.TagName, Is.EqualTo("SPAN"));
                Assert.That(ok.ClassList, Does.Contain("btn-success").And.Contain("approval-compliance-ok").And.Contain("btn-sm"));
                Assert.That(ok.TextContent, Is.EqualTo("approval_compliance_ok"));
                Assert.That(cut.FindAll(".approval-compliance-loading, .approval-compliance-none"), Is.Empty);
            });
        }

        [Test]
        public void NotAllowed_ShowsReasonsAsTooltipAndPassesThemOnClick()
        {
            using BunitContext context = CreateContext();
            ApprovalTaskCompliance? shown = null;

            IRenderedComponent<ApprovalComplianceButton> cut = context.Render<ApprovalComplianceButton>(parameters => parameters
                .Add(p => p.Compliance, kNotAllowed)
                .Add(p => p.ShowReasons, compliance => shown = compliance));

            AngleSharp.Dom.IElement button = cut.Find("button.approval-compliance-button");
            Assert.Multiple(() =>
            {
                Assert.That(button.ClassList, Does.Contain("btn-danger"));
                Assert.That(button.TextContent.Trim(), Is.EqualTo("approval_compliance_not_allowed"));
                Assert.That(button.GetAttribute("title"), Is.EqualTo("forbidden service\nzone violation"));
            });

            button.Click();

            Assert.That(shown, Is.SameAs(kNotAllowed));
        }

        [Test]
        public void ReasonsPopup_ListsReasonsAndClearsComplianceOnClose()
        {
            using BunitContext context = CreateContext();
            ApprovalTaskCompliance? compliance = kNotAllowed;

            IRenderedComponent<ApprovalComplianceReasonsPopup> cut = context.Render<ApprovalComplianceReasonsPopup>(parameters => parameters
                .Add(p => p.Compliance, compliance)
                .Add(p => p.ComplianceChanged, value => compliance = value));

            Assert.That(cut.FindAll(".approval-compliance-reasons li").Select(item => item.TextContent), Is.EqualTo(kReasons));

            string closeText = new SimulatedUserConfig().GetText("close");
            cut.FindAll("button").Single(button => button.TextContent.Trim() == closeText).Click();

            Assert.That(compliance, Is.Null);
        }

        [Test]
        public void ReasonsPopup_HiddenWithoutCompliance()
        {
            using BunitContext context = CreateContext();

            IRenderedComponent<ApprovalComplianceReasonsPopup> cut = context.Render<ApprovalComplianceReasonsPopup>();

            Assert.That(cut.FindAll(".approval-compliance-reasons"), Is.Empty);
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            return context;
        }
    }
}
