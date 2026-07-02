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
        public void SaveButton_IsDisabledWhenResolveDisallowed()
        {
            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddBlazorTable();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

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
                .Add(p => p.CanResolve, false)
                .Add(p => p.Size, PopupSize.Auto)
                .Add(p => p.Items, items)
                .Add(p => p.OnResolve, () => Task.CompletedTask)
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
            Assert.That(cut.Find("button.btn.btn-sm.btn-warning").GetAttribute("disabled"), Is.Not.Null);
        }

        [Test]
        public void SaveButton_InvokesResolveWhenAllowed()
        {
            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddBlazorTable();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

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

            bool resolved = false;

            IRenderedComponent<FlowDuplicateResolver<NetworkObject>> cut = context.Render<FlowDuplicateResolver<NetworkObject>>(parameters => parameters
                .Add(p => p.Title, "Duplicate objects")
                .Add(p => p.Show, true)
                .Add(p => p.CanResolve, true)
                .Add(p => p.Size, PopupSize.Auto)
                .Add(p => p.Items, items)
                .Add(p => p.OnResolve, () =>
                {
                    resolved = true;
                    return Task.CompletedTask;
                })
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

            cut.Find("button.btn.btn-sm.btn-warning").Click();

            Assert.That(resolved, Is.True);
        }

        [Test]
        public void RendersSelectedRowClassFromParent()
        {
            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddBlazorTable();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

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
                },
                new NetworkObject
                {
                    Id = 2,
                    Name = "two",
                    IP = "",
                    IpEnd = "",
                    Uid = "uid-2",
                    FlowActive = false
                }
            ];

            IRenderedComponent<FlowDuplicateResolver<NetworkObject>> cut = context.Render<FlowDuplicateResolver<NetworkObject>>(parameters => parameters
                .Add(p => p.Title, "Duplicate objects")
                .Add(p => p.Show, true)
                .Add(p => p.CanResolve, false)
                .Add(p => p.Size, PopupSize.Auto)
                .Add(p => p.Items, items)
                .Add(p => p.OnClose, () => { })
                .Add(p => p.TableRowClass, item => item.Id == 2 ? "table-warning" : "")
                .AddChildContent<Column<NetworkObject>>(column => column
                    .Add(p => p.Title, "Id")
                    .Add(p => p.Field, (Expression<Func<NetworkObject, object>>)(x => x.Id))
                    .Add(p => p.Sortable, true)
                    .Add(p => p.Filterable, true)));

            Assert.That(cut.Markup, Does.Contain("table-warning"));
        }
    }
}
