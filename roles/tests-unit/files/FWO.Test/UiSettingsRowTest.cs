using AngleSharp.Dom;
using Bunit;
using FWO.Config.Api;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiSettingsRowTest
    {
        private const string kLabelKey = "settings_row_label";
        private const string kTooltipKey = "settings_row_tooltip";
        private const string kInputId = "settingsRowInput";
        private const string kTopClass = "settings-row-top";
        private const string kMarkupPayload = "<img src=x onerror=alert(1)>\"'";

        /// <summary>
        /// The label shows the localized text followed by a colon and is bound to the given input.
        /// </summary>
        [Test]
        public async Task Render_ShowsLocalizedLabelBoundToInput()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsRow> row = RenderRow(context, marked: false, tooltipKey: null, alignTop: false);
            IElement label = row.Find("label.settings-label");

            Assert.Multiple(() =>
            {
                Assert.That(label.TextContent, Is.EqualTo($"{kLabelKey}:"));
                Assert.That(label.GetAttribute("for"), Is.EqualTo(kInputId));
                Assert.That(row.Find(".settings-value input").Id, Is.EqualTo(kInputId));
            });
        }

        /// <summary>
        /// Settings that users can overwrite are marked with an asterisk before the colon.
        /// </summary>
        [Test]
        public async Task Render_MarksLabelWithAsterisk()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsRow> row = RenderRow(context, marked: true, tooltipKey: null, alignTop: false);

            Assert.That(row.Find("label.settings-label").TextContent, Is.EqualTo($"{kLabelKey}*:"));
        }

        /// <summary>
        /// The tooltip is only rendered when a tooltip key is given, an empty title would show an empty tooltip.
        /// </summary>
        [Test]
        public async Task Render_RendersTooltipOnlyWithKey()
        {
            await using BunitContext context = CreateContext();

            IElement rowWithTooltip = RenderRow(context, marked: false, tooltipKey: kTooltipKey, alignTop: false).Find(".settings-row");
            IElement rowWithoutTooltip = RenderRow(context, marked: false, tooltipKey: null, alignTop: false).Find(".settings-row");

            Assert.Multiple(() =>
            {
                Assert.That(rowWithTooltip.GetAttribute("title"), Is.EqualTo(kTooltipKey));
                Assert.That(rowWithoutTooltip.HasAttribute("title"), Is.False);
            });
        }

        /// <summary>
        /// A pre-built tooltip (combined from several text keys) takes precedence over the tooltip key.
        /// </summary>
        [Test]
        public async Task Render_PrefersPrebuiltTooltip()
        {
            await using BunitContext context = CreateContext();
            const string prebuiltTooltip = "combined tooltip text";

            IRenderedComponent<SettingsRow> row = context.Render<SettingsRow>(parameters => parameters
                .Add(p => p.LabelKey, kLabelKey)
                .Add(p => p.TooltipKey, kTooltipKey)
                .Add(p => p.Tooltip, prebuiltTooltip));

            Assert.That(row.Find(".settings-row").GetAttribute("title"), Is.EqualTo(prebuiltTooltip));
        }

        /// <summary>
        /// Multi-line values align the label with their first line instead of their center.
        /// </summary>
        [Test]
        public async Task Render_AlignsTopOnlyWhenRequested()
        {
            await using BunitContext context = CreateContext();

            IElement topRow = RenderRow(context, marked: false, tooltipKey: null, alignTop: true).Find(".settings-row");
            IElement centeredRow = RenderRow(context, marked: false, tooltipKey: null, alignTop: false).Find(".settings-row");

            Assert.Multiple(() =>
            {
                Assert.That(topRow.ClassList, Does.Contain(kTopClass));
                Assert.That(centeredRow.ClassList, Does.Not.Contain(kTopClass));
            });
        }

        /// <summary>
        /// A label text containing markup is rendered as text, never as HTML.
        /// </summary>
        [Test]
        public async Task Render_EncodesLabelAndTooltipText()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new PayloadUserConfig());

            IRenderedComponent<SettingsRow> row = RenderRow(context, marked: false, tooltipKey: kTooltipKey, alignTop: false);

            Assert.Multiple(() =>
            {
                Assert.That(row.FindAll("img"), Is.Empty);
                Assert.That(row.Find("label.settings-label").TextContent, Is.EqualTo($"{kMarkupPayload}:"));
                Assert.That(row.Find(".settings-row").GetAttribute("title"), Is.EqualTo(kMarkupPayload));
            });
        }

        private sealed class PayloadUserConfig : SimulatedUserConfig
        {
            public override string GetText(string key)
            {
                return kMarkupPayload;
            }
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            return context;
        }

        private static IRenderedComponent<SettingsRow> RenderRow(BunitContext context, bool marked, string? tooltipKey, bool alignTop)
        {
            return context.Render<SettingsRow>(parameters => parameters
                .Add(p => p.LabelKey, kLabelKey)
                .Add(p => p.TooltipKey, tooltipKey)
                .Add(p => p.Marked, marked)
                .Add(p => p.LabelFor, kInputId)
                .Add(p => p.AlignTop, alignTop)
                .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddMarkupContent(0, $"<input id=\"{kInputId}\" />"))));
        }
    }
}
