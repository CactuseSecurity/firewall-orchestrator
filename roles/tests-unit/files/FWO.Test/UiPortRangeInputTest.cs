using Bunit;
using FWO.Config.Api;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class UiPortRangeInputTest
{
    [Test]
    public async Task PortRangeInput_ParsesASinglePortAndClearsTheEndPort()
    {
        await using BunitContext context = CreateContext();
        int? port = null;
        int? portEnd = 8443;
        IRenderedComponent<PortRangeInput> component = RenderComponent(context, port, portEnd,
            value => port = value, value => portEnd = value);

        component.Find("input").Input("443");

        Assert.Multiple(() =>
        {
            Assert.That(port, Is.EqualTo(443));
            Assert.That(portEnd, Is.Null);
            Assert.That(component.Find("input").ClassList, Does.Contain("is-valid"));
        });
    }

    [Test]
    public async Task PortRangeInput_ParsesAPortRange()
    {
        await using BunitContext context = CreateContext();
        int? port = null;
        int? portEnd = null;
        IRenderedComponent<PortRangeInput> component = RenderComponent(context, port, portEnd,
            value => port = value, value => portEnd = value);

        component.Find("input").Input("80-443");

        Assert.Multiple(() =>
        {
            Assert.That(port, Is.EqualTo(80));
            Assert.That(portEnd, Is.EqualTo(443));
            Assert.That(component.Find("input").ClassList, Does.Contain("is-valid"));
        });
    }

    [TestCase("443-")]
    [TestCase("-443")]
    [TestCase("443/x")]
    [TestCase("not-a-port")]
    public async Task PortRangeInput_RejectsInvalidInput(string input)
    {
        await using BunitContext context = CreateContext();
        int? port = null;
        int? portEnd = null;
        IRenderedComponent<PortRangeInput> component = RenderComponent(context, port, portEnd,
            value => port = value, value => portEnd = value);

        component.Find("input").Input(input);

        Assert.Multiple(() =>
        {
            Assert.That(port, Is.Null);
            Assert.That(portEnd, Is.Null);
            Assert.That(component.Find("input").ClassList, Does.Contain("is-invalid"));
        });
    }

    private static BunitContext CreateContext()
    {
        BunitContext context = new();
        context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
        return context;
    }

    private static IRenderedComponent<PortRangeInput> RenderComponent(BunitContext context, int? port, int? portEnd,
        Action<int?> portChanged, Action<int?> portEndChanged)
    {
        return context.Render<PortRangeInput>(parameters => parameters
            .Add(p => p.Port, port)
            .Add(p => p.PortEnd, portEnd)
            .Add(p => p.PortChanged, EventCallback.Factory.Create<int?>(context, portChanged))
            .Add(p => p.PortEndChanged, EventCallback.Factory.Create<int?>(context, portEndChanged)));
    }
}
