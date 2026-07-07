using Bunit;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReducedProtocolSelectionTest
    {
        private static BunitContext CreateContext(out SimulatedGlobalConfig globalConfig, out RecordingSettingsApiConn apiConn, out SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            globalConfig = new SimulatedGlobalConfig
            {
                ReducedProtocolSetProtocols = """["tcp"]"""
            };
            apiConn = new RecordingSettingsApiConn();
            apiConn.IpProtocols =
            [
                new() { Id = 6, Name = "tcp" },
                new() { Id = 17, Name = "udp" },
                new() { Id = 1, Name = "icmp" },
                new() { Id = 47, Name = "gre" }
            ];
            userConfig = new SimulatedUserConfig();
            SimulatedUserConfig.DummyTranslate.TryAdd("select_visible_protocols", "Select visible protocols");
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<ReducedProtocolSelection> RenderComponent(
            BunitContext context,
            string configValue,
            Action<bool>? displayChanged = null,
            Action<string>? configChanged = null,
            bool display = true)
        {
            return context.Render<ReducedProtocolSelection>(parameters => parameters
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, value => displayChanged?.Invoke(value))
                .Add(p => p.ConfigValue, configValue)
                .Add(p => p.ConfigValueChanged, value => configChanged?.Invoke(value))
                .Add(p => p.AvailableProtocols, context.Services.GetRequiredService<ApiConnection>() is RecordingSettingsApiConn apiConn ? apiConn.IpProtocols : []));
        }

        [Test]
        public void ReducedProtocolSelection_RendersConfiguredCheckboxes()
        {
            using BunitContext context = CreateContext(out _, out _, out _);
            IRenderedComponent<ReducedProtocolSelection> component = RenderComponent(context, """["tcp","udp"]""");

            List<AngleSharp.Dom.IElement> checkboxes = component.FindAll("input[type=checkbox]").ToList();
            Assert.Multiple(() =>
            {
                Assert.That(checkboxes, Has.Count.EqualTo(4));
                Assert.That(checkboxes[0].HasAttribute("checked"), Is.True);
                Assert.That(checkboxes[1].HasAttribute("checked"), Is.True);
                Assert.That(checkboxes[2].HasAttribute("checked"), Is.False);
                Assert.That(checkboxes[3].HasAttribute("checked"), Is.False);
            });
        }

        [Test]
        public void ReducedProtocolSelection_UsesDefaultSelectionWhenConfigIsEmpty()
        {
            using BunitContext context = CreateContext(out _, out _, out _);
            IRenderedComponent<ReducedProtocolSelection> component = RenderComponent(context, "");

            List<AngleSharp.Dom.IElement> checkboxes = component.FindAll("input[type=checkbox]").ToList();
            Assert.Multiple(() =>
            {
                Assert.That(checkboxes[0].HasAttribute("checked"), Is.True);
                Assert.That(checkboxes[1].HasAttribute("checked"), Is.True);
                Assert.That(checkboxes[2].HasAttribute("checked"), Is.True);
                Assert.That(checkboxes[3].HasAttribute("checked"), Is.False);
            });
        }

        [Test]
        public void ReducedProtocolSelection_DisablesSaveWhenNoVisibleProtocolIsSelected()
        {
            using BunitContext context = CreateContext(out _, out _, out _);
            IRenderedComponent<ReducedProtocolSelection> component = RenderComponent(context, """["bogus"]""");

            Assert.That(component.FindAll("button").First(button => button.InnerHtml.Contains("Save")).HasAttribute("disabled"), Is.True);
        }

        [Test]
        public async Task ReducedProtocolSelection_SavePersistsSelectionInAvailableOrder()
        {
            using BunitContext context = CreateContext(out SimulatedGlobalConfig globalConfig, out RecordingSettingsApiConn apiConn, out _);
            string? savedValue = null;
            bool displayChanged = true;
            IRenderedComponent<ReducedProtocolSelection> component = RenderComponent(context, """["tcp"]""",
                displayChanged: value => displayChanged = value,
                configChanged: value => savedValue = value);

            MethodInfo setProtocolMethod = typeof(ReducedProtocolSelection).GetMethod("SetProtocol", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(nameof(ReducedProtocolSelection), "SetProtocol");
            setProtocolMethod.Invoke(component.Instance, ["udp", new ChangeEventArgs { Value = true }]);
            MethodInfo saveMethod = typeof(ReducedProtocolSelection).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(nameof(ReducedProtocolSelection), "Save");
            Task saveTask = (Task)saveMethod.Invoke(component.Instance, [])!;
            await saveTask;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Does.Contain(FWO.Api.Client.Queries.ConfigQueries.upsertConfigItems));
                Assert.That(apiConn.LastUpsertConfigItems, Has.Count.EqualTo(1));
                Assert.That(apiConn.LastUpsertConfigItems[0].Key, Is.EqualTo("reducedProtocolSetProtocols"));
                Assert.That(apiConn.LastUpsertConfigItems[0].Value, Is.EqualTo("""["tcp","udp"]"""));
                Assert.That(savedValue, Is.EqualTo("""["tcp","udp"]"""));
                Assert.That(displayChanged, Is.False);
                Assert.That(globalConfig.ReducedProtocolSetProtocols, Is.EqualTo("""["tcp","udp"]"""));
            });
        }

        [Test]
        public void ReducedProtocolSelection_CancelClosesPopupWithoutWritingConfig()
        {
            using BunitContext context = CreateContext(out _, out RecordingSettingsApiConn apiConn, out _);
            bool displayChanged = true;
            IRenderedComponent<ReducedProtocolSelection> component = RenderComponent(context, """["tcp"]""",
                displayChanged: value => displayChanged = value);

            component.FindAll("button").First(button => button.InnerHtml.Contains("Cancel")).Click();

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(apiConn.Queries, Does.Not.Contain(FWO.Api.Client.Queries.ConfigQueries.upsertConfigItems));
            });
        }

        [Test]
        public void ReducedProtocolSelection_ReopensWithFreshSelection()
        {
            using BunitContext context = CreateContext(out _, out _, out _);
            IRenderedComponent<ReducedProtocolSelection> component = RenderComponent(context, """["tcp"]""");

            component.InvokeAsync(() => component.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ReducedProtocolSelection.Display)] = false,
                [nameof(ReducedProtocolSelection.ConfigValue)] = """["udp"]"""
            }))).GetAwaiter().GetResult();
            component.InvokeAsync(() => component.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ReducedProtocolSelection.Display)] = true,
                [nameof(ReducedProtocolSelection.ConfigValue)] = """["udp"]"""
            }))).GetAwaiter().GetResult();

            List<AngleSharp.Dom.IElement> checkboxes = component.FindAll("input[type=checkbox]").ToList();
            Assert.Multiple(() =>
            {
                Assert.That(checkboxes[0].HasAttribute("checked"), Is.False);
                Assert.That(checkboxes[1].HasAttribute("checked"), Is.True);
            });
        }
    }
}
