using BlazorTable;
using Bunit;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
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
                },
                new FlowNwObject
                {
                    Id = 2,
                    Name = "two",
                    IpStart = "198.51.100.10",
                    IpEnd = ""
                }
            ];

            string searchText = "";
            List<FlowNwObject> filteredItems = items;

            IRenderedComponent<FlowObjectTable<FlowNwObject>> cut = context.Render<FlowObjectTable<FlowNwObject>>(parameters => parameters
                .Add(p => p.FilteredItems, filteredItems)
                .Add(p => p.FilteredCount, filteredItems.Count)
                .Add(p => p.TotalCount, 2)
                .Add(p => p.PageSize, 25)
                .Add(p => p.SearchLabel, "Search")
                .Add(p => p.ClearSearchLabel, "Clear input")
                .Add(p => p.SearchText, searchText)
                .Add(p => p.SearchTextChanged, EventCallback.Factory.Create<string>(this, value =>
                {
                    searchText = value;
                    filteredItems = string.IsNullOrWhiteSpace(value)
                        ? items
                        : [.. items.Where(item => item.Name?.Contains(value, StringComparison.OrdinalIgnoreCase) == true)];
                }))
                .AddChildContent<Column<FlowNwObject>>(column => column
                    .Add(p => p.Title, "Id")
                    .Add(p => p.Field, (Expression<Func<FlowNwObject, object>>)(x => x.Id))
                    .Add(p => p.Sortable, true)
                    .Add(p => p.Filterable, true))
                .AddChildContent<Pager>(pager => pager
                    .Add(p => p.ShowPageNumber, true)
                    .Add(p => p.ShowTotalCount, true)));

            Assert.That(cut.Markup, Does.Contain("Search"));
            Assert.That(cut.Markup, Does.Contain("2 / 2"));
            Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo(""));

            cut.Find("input").Input("one");

            Assert.That(searchText, Is.EqualTo("one"));
            cut = context.Render<FlowObjectTable<FlowNwObject>>(parameters => parameters
                .Add(p => p.FilteredItems, filteredItems)
                .Add(p => p.FilteredCount, filteredItems.Count)
                .Add(p => p.TotalCount, 2)
                .Add(p => p.PageSize, 25)
                .Add(p => p.SearchLabel, "Search")
                .Add(p => p.ClearSearchLabel, "Clear input")
                .Add(p => p.SearchText, searchText)
                .Add(p => p.SearchTextChanged, EventCallback.Factory.Create<string>(this, value =>
                {
                    searchText = value;
                    filteredItems = string.IsNullOrWhiteSpace(value)
                        ? items
                        : [.. items.Where(item => item.Name?.Contains(value, StringComparison.OrdinalIgnoreCase) == true)];
                }))
                .AddChildContent<Column<FlowNwObject>>(column => column
                    .Add(p => p.Title, "Id")
                    .Add(p => p.Field, (Expression<Func<FlowNwObject, object>>)(x => x.Id))
                    .Add(p => p.Sortable, true)
                    .Add(p => p.Filterable, true))
                .AddChildContent<Pager>(pager => pager
                    .Add(p => p.ShowPageNumber, true)
                    .Add(p => p.ShowTotalCount, true)));

            Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo("one"));
            Assert.That(cut.Markup, Does.Contain("1 / 2"));
            Assert.That(cut.Markup, Does.Contain("Clear input"));
            Assert.That(cut.Find("button.btn-outline-secondary").GetAttribute("title"), Is.EqualTo("Clear input"));
            Assert.That(cut.Find("button.btn-outline-secondary").GetAttribute("aria-label"), Is.EqualTo("Clear input"));

            cut.Find("button.btn-outline-secondary").Click();

            Assert.That(cut.Find("input").GetAttribute("value"), Is.EqualTo(""));
            Assert.That(searchText, Is.EqualTo(""));
            Assert.That(filteredItems, Has.Count.EqualTo(2));
        }
    }
}
