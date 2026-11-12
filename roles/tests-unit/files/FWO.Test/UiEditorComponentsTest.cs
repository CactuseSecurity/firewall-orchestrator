using Bunit;
using AngleSharp.Dom;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal sealed class UiEditorComponentsTest
{
    private static BunitContext CreateContext()
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        context.Services.AddSingleton<DomEventService>();
        context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
        return context;
    }

    [Test]
    public static async Task Collapse_StartsCollapsedAndUsesConfiguredHeaderAndContentClasses()
    {
        await using BunitContext context = CreateContext();

        IRenderedComponent<Collapse> cut = context.Render<Collapse>(parameters => parameters
            .Add(parameter => parameter.StartToggled, true)
            .Add(parameter => parameter.HeaderClass, "bg-secondary-subtle text-dark")
            .Add(parameter => parameter.ContentClass, "p-1")
            .AddChildContent("implementation content"));

        IElement header = cut.Find("button");
        IElement content = cut.Find("div.collapse");

        Assert.Multiple(() =>
        {
            Assert.That(header.ClassList, Does.Contain("bg-secondary-subtle"));
            Assert.That(header.ClassList, Does.Contain("text-dark"));
            Assert.That(content.ClassList, Does.Contain("p-1"));
            Assert.That(content.GetAttribute("class"), Does.Contain("collapse"));
        });
    }

    [Test]
    public static async Task Collapse_ToggleShowsConfiguredContent()
    {
        await using BunitContext context = CreateContext();

        IRenderedComponent<Collapse> cut = context.Render<Collapse>(parameters => parameters
            .Add(parameter => parameter.StartToggled, true)
            .AddChildContent("implementation content"));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("collapse.show p-2"));
        Assert.That(cut.Markup, Does.Contain("implementation content"));
    }

    [Test]
    public static async Task DeviceEditor_InitializesWithTheSelectedDevice()
    {
        Device selected = new() { Id = 2, Name = "Firewall B" };
        List<Device> devices =
        [
            new Device { Id = 1, Name = "Firewall A" },
            selected
        ];

        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.SelectedDevice, selected)
            .Add(parameter => parameter.Devices, devices));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Firewall B"));
    }

    [Test]
    public static async Task DeviceEditor_CancelClosesWithoutSaving()
    {
        Device? saved = null;
        bool? displayChanged = null;

        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Devices, new List<Device> { new() { Id = 1, Name = "Firewall A" } })
            .Add(parameter => parameter.Saved, device =>
            {
                saved = device;
                return Task.CompletedTask;
            })
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            }));

        await cut.FindAll("button.btn-secondary").Single().ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.Null);
            Assert.That(displayChanged, Is.False);
        });
    }

    [Test]
    public static async Task DeviceEditor_DoesNotRenderWhenHidden()
    {
        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, false)
            .Add(parameter => parameter.Devices, new List<Device> { new() { Id = 1, Name = "Firewall A" } }));

        Assert.That(cut.Markup, Is.Empty);
    }

    [Test]
    public static async Task DeviceEditor_AllowsSelectingAndSavingAnotherDevice()
    {
        Device first = new() { Id = 1, Name = "Firewall A" };
        Device second = new() { Id = 2, Name = "Firewall B" };
        Device? saved = null;
        bool? displayChanged = null;

        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Devices, new List<Device> { first, second })
            .Add(parameter => parameter.Saved, device =>
            {
                saved = device;
                return Task.CompletedTask;
            })
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            }));

        await cut.InvokeAsync(() => cut.Find("input").Focus());
        IElement secondDeviceButton = cut.FindAll("button.dropdown-item")
            .Single(button => button.TextContent.Contains("Firewall B", StringComparison.Ordinal));
        await secondDeviceButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.SameAs(second));
            Assert.That(displayChanged, Is.False);
        });
    }

    [Test]
    public static async Task DeviceEditor_SavesNullWhenNoDeviceIsSelected()
    {
        Device? saved = new Device { Id = 1 };

        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.Devices, new List<Device> { new() { Id = 1, Name = "Firewall A" } })
            .Add(parameter => parameter.Saved, device =>
            {
                saved = device;
                return Task.CompletedTask;
            }));

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(saved, Is.Null);
    }

    [Test]
    public static async Task DeviceEditor_ReinitializesAfterClosingAndReopening()
    {
        Device first = new() { Id = 1, Name = "Firewall A" };
        Device second = new() { Id = 2, Name = "Firewall B" };
        bool? displayChanged = null;

        await using BunitContext context = CreateContext();
        IRenderedComponent<DeviceEditor> cut = context.Render<DeviceEditor>(parameters => parameters
            .Add(parameter => parameter.Display, true)
            .Add(parameter => parameter.SelectedDevice, first)
            .Add(parameter => parameter.Devices, new List<Device> { first, second })
            .Add(parameter => parameter.DisplayChanged, value =>
            {
                displayChanged = value;
                return Task.CompletedTask;
            }));

        await cut.FindAll("button.btn-secondary").Single().ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.That(displayChanged, Is.False);

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(DeviceEditor.Display)] = true,
                [nameof(DeviceEditor.SelectedDevice)] = second,
                [nameof(DeviceEditor.Devices)] = new List<Device> { first, second }
            })));

        Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("Firewall B"));
    }

}
