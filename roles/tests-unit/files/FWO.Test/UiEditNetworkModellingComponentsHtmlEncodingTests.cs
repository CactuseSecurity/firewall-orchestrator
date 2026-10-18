using Bunit;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services.Modelling;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Modelling components that render connection, interface and owner data as markup must encode it,
    /// and must keep only the allowed formatting of reasons (GHSA-mm29-m6vq-gwgg).
    /// </summary>
    internal partial class UiEditNetworkModellingComponentsTest
    {
        private const string kHtmlPayload = "<img src=x onerror=console.log(1)>\"'&amp;";
        private const string kReasonHtmlPayload = "<b onclick=console.log(1)>bold</b><img src=x onerror=console.log(1)>";

        private static FwoOwner PayloadOwner()
        {
            return new FwoOwner
            {
                Id = 1,
                Name = kHtmlPayload,
                ExtAppId = "APP1",
                Active = true,
                ConnectionCount = new AggregateCount { Aggregate = new Aggregate { Count = 1 } }
            };
        }

        [Test]
        public async Task EditConn_ReadOnly_SanitizesReason()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnection connection = new() { Name = "conn", Reason = kReasonHtmlPayload };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, connection, readOnly: true);

            IRenderedComponent<EditConn> component = RenderEditConn(context, handler, display: true);

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("img"), Is.Empty, "the reason created an <img> element");
                Assert.That(component.FindAll("[onclick]"), Is.Empty, "the reason kept an event handler");
                Assert.That(component.FindAll("label b").Select(element => element.TextContent), Does.Contain("bold"), "the formatting is kept");
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task DecommissionInterfacePopup_EncodesInterfaceNameInMessage()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig userConfig);
            ModellingConnection actConn = new() { Id = 702, Name = kHtmlPayload, IsInterface = true, IsPublished = true, AppId = 1, App = PayloadOwner() };
            ModellingConnectionHandler handler = CreateConnectionHandler(new SimulatedApiConnection(), userConfig, actConn);

            List<ModellingConnection> possibleInterfaces = [actConn];

            IRenderedComponent<DecommissionInterfacePopup> component = RenderDecommissionInterfacePopup(
                context, display: true, connHandler: handler, possibleInterfaces: possibleInterfaces);

            Assert.That(GetPrivateProperty<string>(component.Instance, "Message"), Does.Contain(HtmlOutputEncoder.EncodeText(kHtmlPayload)));
            Assert.That(component.FindAll("img"), Is.Empty);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ConnectionTable_EncodesNameAndSanitizesReason()
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig _);
            List<ModellingConnection> connections = [new() { Id = 1, Name = kHtmlPayload, Reason = kReasonHtmlPayload, IsRequested = true }];

            IRenderedComponent<ConnectionTable> component = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<ConnectionTable>(table => table
                    .Add(p => p.Connections, connections)
                    .Add(p => p.Readonly, true)))
                .FindComponent<ConnectionTable>();

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("img[src='x']"), Is.Empty, "a stored value created an <img> element");
                Assert.That(component.FindAll("[onerror], [onclick]"), Is.Empty, "a stored value created an event handler");
                Assert.That(component.Markup, Does.Not.Contain(kHtmlPayload), "the name reached the markup unencoded");
                Assert.That(string.Concat(component.Nodes.Select(node => node.TextContent)), Does.Contain(kHtmlPayload), "the name is shown as text");
                Assert.That(component.FindAll("b").Select(element => element.TextContent), Does.Contain("bold"), "the formatting of the reason is kept");
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task SelectOwner_EncodesOwnerName(bool withConnectionStatus)
        {
            await using BunitContext context = CreateContext(out _, out SimulatedUserConfig _);
            FwoOwner owner = PayloadOwner();
            List<FwoOwner> apps = [owner];

            IRenderedComponent<SelectOwner> component = context.Render<SelectOwner>(parameters => parameters
                .Add(p => p.Apps, apps)
                .Add(p => p.SelectedOwner, owner)
                .Add(p => p.WithConnectionStatus, withConnectionStatus));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("img"), Is.Empty, "the owner name created an <img> element");
                Assert.That(component.Markup, Does.Not.Contain(kHtmlPayload), "the owner name reached the markup unencoded");
                Assert.That(string.Concat(component.Nodes.Select(node => node.TextContent)), Does.Contain(kHtmlPayload), "the owner name is shown as text");
            });
        }
    }
}
