using BlazorTable;
using Bunit;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace FWO.Test
{
    [TestFixture]
    internal class UiFlowObjectTableTest
    {
        [Test]
        public void RendersSearchSummaryAndCanClearSearch()
        {
            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddBlazorTable();

            List<FlowNwObject> items =
            [
                new FlowNwObject
                {
                    Id = 1,
                    Name = "one",
                    IpStart = "192.0.2.10",
                    IpEnd = ""
                }
            ];

            IRenderedComponent<FlowObjectTable<FlowNwObject>> cut = context.Render<FlowObjectTable<FlowNwObject>>(parameters => parameters
                .Add(p => p.FilteredItems, items)
                .Add(p => p.FilteredCount, 1)
                .Add(p => p.TotalCount, 3)
                .Add(p => p.PageSize, 25)
                .Add(p => p.SearchLabel, "Search")
                .Add(p => p.SearchText, "abc")
                .AddChildContent<Column<FlowNwObject>>(column => column
                    .Add(p => p.Title, "Id")
                    .Add(p => p.Field, (Expression<Func<FlowNwObject, object>>)(x => x.Id))
                    .Add(p => p.Sortable, true)
                    .Add(p => p.Filterable, true))
                .AddChildContent<Pager>(pager => pager
                    .Add(p => p.ShowPageNumber, true)
                    .Add(p => p.ShowTotalCount, true)));

            Assert.That(cut.Markup, Does.Contain("Search"));
            Assert.That(cut.Markup, Does.Contain("1 / 3"));
            Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("abc"));

            cut.Find("button.btn-outline-secondary").Click();

            Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo(""));
            Assert.That(cut.Markup, Does.Not.Contain("abc"));
        }
    }
}
