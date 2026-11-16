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

        [TestCase(true)]
        [TestCase(false)]
        public void DisplayButtonWithTooltip_EncodesTooltipPayload(bool iconify)
        {
            UserConfig userConfig = new SimulatedUserConfig
            {
                ModIconify = iconify
            };
            const string payload = "Bob\" onmouseover=\"alert(1)\" x='<img src=x>";

            MarkupString result = DisplayService.DisplayButtonWithTooltip(userConfig, "save", "bi bi-save", payload);

            Assert.Multiple(() =>
            {
                Assert.That(result.Value, Does.Not.Contain("\" onmouseover="));
                Assert.That(result.Value, Does.Not.Contain("<img"));
                Assert.That(result.Value, Does.Contain("Bob&quot; onmouseover=&quot;alert(1)&quot; x=&#39;&lt;img src=x&gt;"));
            });
        }

        [Test]
        public void DisplayConfigurableText_SanitizesCustomizedText()
        {
            UserConfig userConfig = new CustomizedUserConfig();
            userConfig.Overwrite["support_details"] = "<b>call us</b><img src=x onerror=\"alert(1)\"><a href=\"javascript:alert(2)\" title='x'>link</a>";

            MarkupString result = DisplayService.DisplayConfigurableText(userConfig, "support_details");

            Assert.Multiple(() =>
            {
                Assert.That(result.Value, Does.Contain("<b>call us</b>"));
                Assert.That(result.Value, Does.Not.Contain("<img"));
                Assert.That(result.Value, Does.Not.Contain("<a href"));
                Assert.That(result.Value, Does.Not.Contain("onerror=\""));
            });
        }

        [Test]
        public void DisplayConfigurableText_KeepsShippedTextMarkup()
        {
            UserConfig userConfig = new SimulatedUserConfig();

            MarkupString result = DisplayService.DisplayConfigurableText(userConfig, "save");

            Assert.That(result.Value, Is.EqualTo(userConfig.GetText("save")));
        }

        [Test]
        public void DisplayButtonWithTooltip_KeepsEntitiesOfTextsReadable()
        {
            UserConfig userConfig = new SimulatedUserConfig
            {
                ModIconify = true
            };

            MarkupString result = DisplayService.DisplayButtonWithTooltip(userConfig, "save", "bi bi-save", "Pr&uuml;fung");

            Assert.That(result.Value, Does.Contain("Pr&#252;fung"));
            Assert.That(result.Value, Does.Not.Contain("&amp;uuml;"));
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

        /// <summary>
        /// Simulated configuration that resolves texts from the admin customizations first, like UserConfig does.
        /// </summary>
        private sealed class CustomizedUserConfig : SimulatedUserConfig
        {
            public override string GetText(string key)
            {
                return Overwrite.TryGetValue(key, out string? customText) ? customText : base.GetText(key);
            }
        }
    }
}
