using FWO.Config.Api;
using FWO.Data.Workflow;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class DisplayServiceTest
    {
        [Test]
        public void DisplayButton_ReturnsPlainText_WhenIconifyDisabled()
        {
            UserConfig userConfig = new SimulatedUserConfig
            {
                ModIconify = false
            };

            MarkupString result = DisplayService.DisplayButton(userConfig, "save", "bi bi-save", "add", "bi bi-obj");

            Assert.That(result.Value, Is.EqualTo("Save"));
        }

        [Test]
        public void DisplayButton_IncludesIconAndTooltip_WhenIconifyEnabled()
        {
            UserConfig userConfig = new SimulatedUserConfig
            {
                ModIconify = true
            };

            MarkupString result = DisplayService.DisplayButton(userConfig, "save", "bi bi-save", "add", "bi bi-obj");

            Assert.That(result.Value, Does.Contain("class=\"bi bi-save\""));
            Assert.That(result.Value, Does.Contain("data-toggle=\"tooltip\""));
            Assert.That(result.Value, Does.Contain("title=\"Save\""));
            Assert.That(result.Value, Does.Contain("<span class=\"stdtext\">Add</span>"));
            Assert.That(result.Value, Does.Contain("<span class=\"bi bi-obj\"/>"));
        }

        [Test]
        public void DisplayButtonWithTooltip_ReturnsPlainTextWithTooltip_WhenIconifyDisabled()
        {
            UserConfig userConfig = new SimulatedUserConfig
            {
                ModIconify = false
            };

            MarkupString result = DisplayService.DisplayButtonWithTooltip(userConfig, "save", "bi bi-save", "Extra");

            Assert.That(result.Value, Does.Contain("data-toggle=\"tooltip\""));
            Assert.That(result.Value, Does.Contain("Extra"));
            Assert.That(result.Value, Does.Contain("Save"));
        }

        [Test]
        public void DisplayButtonWithTooltip_IncludesTextInTooltip_WhenIconifyEnabled()
        {
            UserConfig userConfig = new SimulatedUserConfig
            {
                ModIconify = true
            };

            MarkupString result = DisplayService.DisplayButtonWithTooltip(userConfig, "save", "bi bi-save", "Extra", "add", "bi bi-obj");

            Assert.That(result.Value, Does.Contain("class=\"bi bi-save\""));
            Assert.That(result.Value, Does.Contain("title=\"Save -  Extra\""));
            Assert.That(result.Value, Does.Contain("<span class=\"stdtext\">Add</span>"));
            Assert.That(result.Value, Does.Contain("<span class=\"bi bi-obj\"/>"));
        }

        [Test]
        public void DisplayState_ReturnsAutomaticForAutomaticState()
        {
            UserConfig userConfig = new SimulatedUserConfig();

            string result = DisplayService.DisplayState(userConfig, new WfState { Id = -1, Name = "Ignored" });

            Assert.That(result, Is.EqualTo(userConfig.GetText("automatic")));
        }

        [Test]
        public void DisplayState_ReturnsConditionalForConditionalState()
        {
            UserConfig userConfig = new SimulatedUserConfig();

            string result = DisplayService.DisplayState(userConfig, new WfState { Id = -2, Name = "Ignored" });

            Assert.That(result, Is.EqualTo(userConfig.GetText("Conditional")));
        }

        [Test]
        public void DisplayStateWithId_ReturnsNameAndIdForPositiveState()
        {
            UserConfig userConfig = new SimulatedUserConfig();

            string result = DisplayService.DisplayStateWithId(userConfig, new WfState { Id = 17, Name = "Approved" });

            Assert.That(result, Is.EqualTo("Approved (17)"));
        }

        [Test]
        public void DisplayStateWithId_DoesNotAppendIdForNegativeState()
        {
            UserConfig userConfig = new SimulatedUserConfig();

            string result = DisplayService.DisplayStateWithId(userConfig, new WfState { Id = -1, Name = "Ignored" });

            Assert.That(result, Is.EqualTo(userConfig.GetText("automatic")));
        }
    }
}
