using Bunit;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data.Modelling;
using FWO.Ui.Pages.Settings;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiVarianceOptionsSelectionTest
    {
        private static BunitContext CreateContext(out SimulatedGlobalConfig globalConfig, out RecordingSettingsApiConn apiConn)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            globalConfig = new SimulatedGlobalConfig
            {
                RuleRecognitionOption = JsonSerializer.Serialize(new RuleRecognitionOption())
            };
            apiConn = new RecordingSettingsApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            return context;
        }

        private static IRenderedComponent<VarianceOptionsSelection> RenderComponent(
            BunitContext context,
            RuleRecognitionOption configValue,
            Action<bool>? displayChanged = null,
            Action<RuleRecognitionOption>? configChanged = null,
            bool display = true)
        {
            return context.Render<VarianceOptionsSelection>(parameters => parameters
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.ConfigValue, configValue)
                .Add(p => p.ConfigValueChanged, value => configChanged?.Invoke(value)));
        }

        [Test]
        public void VarianceOptionsSelection_RendersCurrentSelection()
        {
            using BunitContext context = CreateContext(out _, out _);
            IRenderedComponent<VarianceOptionsSelection> component = RenderComponent(context, new RuleRecognitionOption
            {
                NwRegardIp = true,
                NwRegardName = false,
                NwRegardGroupName = true,
                NwResolveGroup = false,
                NwSeparateGroupAnalysis = true,
                SvcRegardPortAndProt = true,
                SvcRegardName = false,
                SvcRegardGroupName = false,
                SvcResolveGroup = true,
                SvcSplitPortRanges = false
            });

            List<AngleSharp.Dom.IElement> checkboxes = component.FindAll("input[type=checkbox]").ToList();
            Assert.That(checkboxes, Has.Count.EqualTo(10));
            Assert.Multiple(() =>
            {
                Assert.That(checkboxes[0].HasAttribute("checked"), Is.True);
                Assert.That(checkboxes[1].HasAttribute("checked"), Is.True);
                Assert.That(checkboxes[2].HasAttribute("checked"), Is.False);
                Assert.That(checkboxes[4].HasAttribute("checked"), Is.True);
                Assert.That(checkboxes[6].HasAttribute("checked"), Is.False);
                Assert.That(checkboxes[7].HasAttribute("checked"), Is.True);
                Assert.That(checkboxes[8].HasAttribute("checked"), Is.True);
            });
        }

        [Test]
        public void VarianceOptionsSelection_CancelClosesPopupWithoutWritingConfig()
        {
            using BunitContext context = CreateContext(out _, out RecordingSettingsApiConn apiConn);
            bool displayChanged = true;
            IRenderedComponent<VarianceOptionsSelection> component = RenderComponent(context, new RuleRecognitionOption(),
                displayChanged: value => displayChanged = value);

            component.FindAll("button").First(button => button.InnerHtml.Contains("Cancel")).Click();

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(apiConn.Queries, Does.Not.Contain(FWO.Api.Client.Queries.ConfigQueries.upsertConfigItems));
            });
        }

        [Test]
        public async Task VarianceOptionsSelection_SavePersistsSerializedRuleRecognitionOptions()
        {
            using BunitContext context = CreateContext(out SimulatedGlobalConfig globalConfig, out RecordingSettingsApiConn apiConn);
            RuleRecognitionOption? savedValue = null;
            bool displayChanged = true;
            IRenderedComponent<VarianceOptionsSelection> component = RenderComponent(context, new RuleRecognitionOption(),
                displayChanged: value => displayChanged = value,
                configChanged: value => savedValue = value);

            component.FindAll("input[type=checkbox]")[2].Change(true);
            component.FindAll("input[type=checkbox]")[9].Change(true);
            component.FindAll("button").First(button => button.InnerHtml.Contains("Save")).Click();

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Does.Contain(FWO.Api.Client.Queries.ConfigQueries.upsertConfigItems));
                Assert.That(apiConn.LastUpsertConfigItems, Has.Count.EqualTo(1));
                Assert.That(apiConn.LastUpsertConfigItems[0].Key, Is.EqualTo("ruleRecognitionOption"));
                Assert.That(apiConn.LastUpsertConfigItems[0].Value, Does.Contain("\"nwRegardName\":true"));
                Assert.That(apiConn.LastUpsertConfigItems[0].Value, Does.Contain("\"svcSplitPortRanges\":true"));
                Assert.That(savedValue, Is.Not.Null);
                Assert.That(savedValue!.NwRegardName, Is.True);
                Assert.That(savedValue.SvcSplitPortRanges, Is.True);
                Assert.That(displayChanged, Is.False);
                Assert.That(globalConfig.RuleRecognitionOption, Does.Contain("\"nwRegardName\":true"));
            });
        }

        [Test]
        public void VarianceOptionsSelection_EmptyPopupCanStillBeClosed()
        {
            using BunitContext context = CreateContext(out _, out RecordingSettingsApiConn apiConn);
            bool displayChanged = true;
            IRenderedComponent<VarianceOptionsSelection> component = RenderComponent(context, new RuleRecognitionOption(), displayChanged: value => displayChanged = value);

            component.FindAll("button").First(button => button.InnerHtml.Contains("Cancel")).Click();

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(apiConn.LastUpsertConfigItems, Is.Empty);
            });
        }
    }
}
