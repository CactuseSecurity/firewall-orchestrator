using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Labelling;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// The label editor of a connection side: AND-groups as chips, read-only display, removing labels, encoding.
    /// </summary>
    [TestFixture]
    internal class UiConnLabelEditorTest
    {
        private const string kPayload = "<script>x</script>\" onmouseover=\"y' ";
        private static readonly FwoOwner Application = new() { Id = 1, Name = "App1" };

        [Test]
        public async Task ReadOnly_ShowsTheGroupsWithoutEditing()
        {
            await using BunitContext context = CreateContext(out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateHandler(userConfig, Labelled());

            IRenderedComponent<ConnLabelEditor> editor = context.Render<ConnLabelEditor>(parameters => parameters
                .Add(component => component.ConnHandler, handler)
                .Add(component => component.IsSource, true)
                .Add(component => component.ReadOnly, true));

            Assert.Multiple(() =>
            {
                Assert.That(editor.Markup, Does.Contain("Stage: Prod").And.Contain("OS: Linux").And.Contain("Stage: Test"));
                Assert.That(editor.Markup, Does.Contain("∩").And.Contain("∪"));
                Assert.That(editor.FindAll("button"), Is.Empty);
                Assert.That(editor.FindAll("select"), Is.Empty);
            });
        }

        [Test]
        public async Task ReadOnly_RendersNothingWithoutLabels()
        {
            await using BunitContext context = CreateContext(out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateHandler(userConfig, new ModellingConnection { Id = 5 });

            IRenderedComponent<ConnLabelEditor> editor = context.Render<ConnLabelEditor>(parameters => parameters
                .Add(component => component.ConnHandler, handler)
                .Add(component => component.IsSource, false)
                .Add(component => component.ReadOnly, true));

            Assert.That(editor.Markup.Trim(), Is.Empty);
        }

        [Test]
        public async Task Editing_RemovesALabelAndReportsTheChange()
        {
            await using BunitContext context = CreateContext(out SimulatedUserConfig userConfig);
            ModellingConnectionHandler handler = CreateHandler(userConfig, Labelled());
            int changes = 0;

            IRenderedComponent<ConnLabelEditor> editor = context.Render<ConnLabelEditor>(parameters => parameters
                .Add(component => component.ConnHandler, handler)
                .Add(component => component.IsSource, true)
                .Add(component => component.ReadOnly, false)
                .Add(component => component.Changed, () => changes++));
            editor.FindAll($"button[title='remove_label']")[2].Click();

            Assert.Multiple(() =>
            {
                Assert.That(handler.SrcLabelGroups, Has.Count.EqualTo(1));
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(editor.FindAll("select"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task Labels_AreEncoded()
        {
            await using BunitContext context = CreateContext(out SimulatedUserConfig userConfig);
            LabelValue payload = new() { Id = 9, Value = kPayload, Key = new() { Name = kPayload } };
            ModellingConnectionHandler handler = CreateHandler(userConfig, new ModellingConnection { Id = 5, SourceLabels = [new() { LabelId = 9, Label = payload }] });

            IRenderedComponent<ConnLabelEditor> editor = context.Render<ConnLabelEditor>(parameters => parameters
                .Add(component => component.ConnHandler, handler)
                .Add(component => component.IsSource, true)
                .Add(component => component.ReadOnly, true));

            Assert.Multiple(() =>
            {
                Assert.That(editor.Markup, Does.Not.Contain("<script>"));
                Assert.That(editor.Markup, Does.Contain("&lt;script&gt;"));
                Assert.That(editor.FindAll("[onmouseover]"), Is.Empty);
            });
        }

        private static ModellingConnection Labelled()
        {
            return new()
            {
                Id = 5,
                SourceLabels =
                [
                    Assigned(1, "Stage", "Prod", 0), Assigned(3, "OS", "Linux", 0), Assigned(2, "Stage", "Test", 1)
                ]
            };
        }

        private static ConnectionLabel Assigned(long id, string key, string value, int andGroup)
        {
            return new() { LabelId = id, AndGroup = andGroup, Label = new() { Id = id, Value = value, Key = new() { Name = key } } };
        }

        private static ModellingConnectionHandler CreateHandler(SimulatedUserConfig userConfig, ModellingConnection connection)
        {
            return new ModellingConnectionHandler(new SimulatedApiConnection(), userConfig, Application, [connection], connection, false, false,
                DefaultInit.DoNothing, DefaultInit.DoNothing, true);
        }

        private static BunitContext CreateContext(out SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            userConfig = new SimulatedUserConfig { LabelBasedModelling = true };
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<KeyboardInputService>(new KeyboardInputService());
            return context;
        }
    }
}
