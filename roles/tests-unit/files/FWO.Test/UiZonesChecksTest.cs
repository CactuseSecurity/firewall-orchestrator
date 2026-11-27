using System.Reflection;
using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using FWO.Ui.Pages.Compliance;
using Microsoft.Extensions.DependencyInjection;
using NetTools;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class UiZonesChecksTest
{
    [Test]
    public void CheckWithoutInput_DoesNotDisplayResult()
    {
        using BunitContext context = CreateContext(CreateZones(out _, out _));

        IRenderedComponent<ZonesChecks> page = context.Render<ZonesChecks>();
        page.Find("button").Click();

        Assert.That(page.FindAll("span.bg-success, span.bg-danger"), Is.Empty);
    }

    [Test]
    public void CheckForbiddenCommunication_ShowsViolatingZonePair()
    {
        List<NetworkZone> zones = CreateZones(out NetworkZone sourceZone, out NetworkZone destinationZone);
        using BunitContext context = CreateContext(zones);

        IRenderedComponent<ZonesChecks> page = RenderWithIpInput(context, "10.0.0.5", "192.168.1.10");
        page.Find("button").Click();

        Assert.Multiple(() =>
        {
            Assert.That(page.FindAll("span.bg-danger"), Has.Count.EqualTo(1));
            Assert.That(page.Markup, Does.Contain(sourceZone.Name));
            Assert.That(page.Markup, Does.Contain(destinationZone.Name));
            Assert.That(page.FindAll("tbody tr"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void CheckAllowedCommunication_ShowsCompliantResultWithoutViolations()
    {
        List<NetworkZone> zones = CreateZones(out NetworkZone sourceZone, out NetworkZone destinationZone);
        sourceZone.AllowedCommunicationDestinations = new List<NetworkZone> { destinationZone }.ToArray();
        using BunitContext context = CreateContext(zones);

        IRenderedComponent<ZonesChecks> page = RenderWithIpInput(context, "10.0.0.5", "192.168.1.10");
        page.Find("button").Click();

        Assert.That(page.FindAll("span.bg-success"), Has.Count.EqualTo(1));
        Assert.That(page.FindAll("span.bg-danger"), Is.Empty);
    }

    [Test]
    public void CheckUndefinedRanges_UsesInternetLocalZone()
    {
        List<NetworkZone> zones = CreateZones(out _, out _);
        SimulatedUserConfig userConfig = new();
        using BunitContext context = CreateContext(zones, userConfig);

        IRenderedComponent<ZonesChecks> page = RenderWithIpInput(context, "172.16.0.5", "172.17.0.10");
        page.Find("button").Click();

        Assert.Multiple(() =>
        {
            Assert.That(page.FindAll("span.bg-danger"), Has.Count.EqualTo(1));
            Assert.That(page.Markup, Does.Contain("Internet/Local"));
        });
    }

    [Test]
    public void CheckCompliance_UsesProvidedIpRanges()
    {
        List<NetworkZone> zones = CreateZones(out _, out _);
        using BunitContext context = CreateContext(zones);
        ZonesChecks component = context.Render<ZonesChecks>().Instance;

        List<IPAddressRange> sourceRanges = [IPAddressRange.Parse("10.0.0.5")];
        List<IPAddressRange> destinationRanges = [IPAddressRange.Parse("192.168.1.10")];

        (bool compliant, List<(NetworkZone, NetworkZone)> violations) =
            InvokeCheckCompliance(component, sourceRanges, destinationRanges);

        Assert.Multiple(() =>
        {
            Assert.That(compliant, Is.False);
            Assert.That(violations, Has.Count.EqualTo(1));
        });
    }

    private static BunitContext CreateContext(List<NetworkZone> networkZones, SimulatedUserConfig? userConfig = null)
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
        context.Services.AddSingleton<UserConfig>(userConfig ?? new SimulatedUserConfig());
        context.Services.AddSingleton(new NetworkZoneService { NetworkZones = networkZones });
        return context;
    }

    private static IRenderedComponent<ZonesChecks> RenderWithIpInput(BunitContext context, string source, string destination)
    {
        IRenderedComponent<ZonesChecks> page = context.Render<ZonesChecks>();
        IReadOnlyList<AngleSharp.Dom.IElement> inputs = page.FindAll("input");
        Assert.That(inputs, Has.Count.EqualTo(2));
        inputs[0].Input(source);
        inputs[1].Input(destination);
        return page;
    }

    private static List<NetworkZone> CreateZones(out NetworkZone sourceZone, out NetworkZone destinationZone)
    {
        sourceZone = new NetworkZone
        {
            Id = 1,
            Name = "Internal",
            IPRanges = new List<IPAddressRange> { IPAddressRange.Parse("10.0.0.0/8") }.ToArray()
        };
        destinationZone = new NetworkZone
        {
            Id = 2,
            Name = "DMZ",
            IPRanges = new List<IPAddressRange> { IPAddressRange.Parse("192.168.0.0/16") }.ToArray()
        };
        return [sourceZone, destinationZone];
    }

    private static (bool Compliant, List<(NetworkZone, NetworkZone)> Violations) InvokeCheckCompliance(
        ZonesChecks component, List<IPAddressRange> source, List<IPAddressRange> destination)
    {
        MethodInfo method = typeof(ZonesChecks).GetMethod("CheckCompliance", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(typeof(ZonesChecks).FullName, "CheckCompliance");
        object?[] parameters = { source, destination, null };
        bool compliant = (bool)(method.Invoke(component, parameters) ?? throw new InvalidOperationException());
        List<(NetworkZone, NetworkZone)> violations = (List<(NetworkZone, NetworkZone)>)(parameters[2]
            ?? throw new InvalidOperationException("The method did not set forbidden communications."));
        return (compliant, violations);
    }

}
