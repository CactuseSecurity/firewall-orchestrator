using BlazorTable;
using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Shared;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace FWO.Test
{
    [TestFixture]
    internal class UiFlowDuplicateResolverTest
    {
        [Test]
        public void RendersSummaryTableAndInvokesClose()
        {
            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddBlazorTable();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            bool closed = false;
            List<NetworkObject> items =
            [
                new NetworkObject
                {
                    Id = 1,
                    Name = "one",
                    IP = "",
                    IpEnd = "",
                    Uid = "uid-1",
                    FlowActive = false
                }
            ];

            IRenderedComponent<FlowDuplicateResolver<NetworkObject>> cut = context.Render<FlowDuplicateResolver<NetworkObject>>(parameters => parameters
                .Add(p => p.Title, "Duplicate objects")
                .Add(p => p.Show, true)
                .Add(p => p.CanResolve, true)
                .Add(p => p.Size, PopupSize.Auto)
                .Add(p => p.Items, items)
                .Add(p => p.OnClose, () => closed = true)
                .Add(p => p.SummaryContent, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div>Flow object: test</div>")))
                .AddChildContent<Column<NetworkObject>>(column => column
                    .Add(p => p.Title, "Id")
                    .Add(p => p.Field, (Expression<Func<NetworkObject, object>>)(x => x.Id))
                    .Add(p => p.Sortable, true)
                    .Add(p => p.Filterable, true))
                .AddChildContent<Column<NetworkObject>>(column => column
                    .Add(p => p.Title, "Uid")
                    .Add(p => p.Field, (Expression<Func<NetworkObject, object>>)(x => x.Uid))
                    .Add(p => p.Sortable, true)
                    .Add(p => p.Filterable, true))
                .AddChildContent<Column<NetworkObject>>(column => column
                    .Add(p => p.Title, "Details")
                    .Add(p => p.Sortable, false)
                    .Add(p => p.Filterable, false))
                .AddChildContent<Column<NetworkObject>>(column => column
                    .Add(p => p.Title, "Actions")
                    .Add(p => p.Sortable, false)
                    .Add(p => p.Filterable, false)));

            Assert.That(cut.Markup, Does.Contain("Duplicate objects"));
            Assert.That(cut.Markup, Does.Contain("Flow object: test"));
            Assert.That(cut.Markup, Does.Contain("Save"));
            Assert.That(cut.Markup, Does.Contain("Cancel"));

            cut.Find("button.modern-close").Click();

            Assert.That(closed, Is.True);
        }
    }
}
