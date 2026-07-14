using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiObjectGroupTest : BunitContext
    {
        [Test]
        public void FetchesDetailsWhenContentChanges()
        {
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddScoped(_ => JSInterop.JSRuntime);
            Services.AddLocalization();

            int fetchCount = 0;
            Rule rule1 = new() { Id = 1, MgmtId = 1, Name = "rule-1" };
            Rule rule2 = new() { Id = 2, MgmtId = 1, Name = "rule-2" };

            Func<RsbTab, ObjCategory, Func<Task>, long, bool, Task> fetchObjects =
                async (tab, objCategory, hasChanged, id, nat) =>
                {
                    fetchCount++;
                    if (id == rule1.Id)
                    {
                        rule1.Detailed = true;
                    }
                    if (id == rule2.Id)
                    {
                        rule2.Detailed = true;
                    }
                    await hasChanged();
                };

            IRenderedComponent<ObjectGroup<Rule>> cut = Render<ObjectGroup<Rule>>(parameters => parameters
                .Add(p => p.FetchObjects, fetchObjects)
                .Add(p => p.Tab, RsbTab.rule)
                .Add(p => p.PageSize, 0)
                .Add(p => p.StartContentDetailed, true)
                .Add(p => p.Content, rule1)
                .Add(p => p.NameExtractor, rule => rule.Name ?? string.Empty)
                .Add(p => p.NetworkObjectExtractor, _ => Array.Empty<NetworkObject>())
                .Add(p => p.NetworkServiceExtractor, _ => Array.Empty<NetworkService>())
                .Add(p => p.NetworkUserExtractor, _ => Array.Empty<NetworkUser>()));

            Assert.That(fetchCount, Is.EqualTo(1));

            cut = Render<ObjectGroup<Rule>>(parameters => parameters
                .Add(p => p.FetchObjects, fetchObjects)
                .Add(p => p.Tab, RsbTab.rule)
                .Add(p => p.PageSize, 0)
                .Add(p => p.StartContentDetailed, true)
                .Add(p => p.Content, rule2)
                .Add(p => p.NameExtractor, rule => rule.Name ?? string.Empty)
                .Add(p => p.NetworkObjectExtractor, _ => Array.Empty<NetworkObject>())
                .Add(p => p.NetworkServiceExtractor, _ => Array.Empty<NetworkService>())
                .Add(p => p.NetworkUserExtractor, _ => Array.Empty<NetworkUser>()));

            Assert.That(fetchCount, Is.EqualTo(2));
        }

        [Test]
        public void RendersOwnerReportsWithDuplicateDefaultIds()
        {
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddScoped(_ => JSInterop.JSRuntime);
            Services.AddLocalization();

            List<OwnerConnectionReport> ownerReports =
            [
                new() { Owner = new FwoOwner { Id = 0, Name = "Owner A" }, Name = "Owner A" },
                new() { Owner = new FwoOwner { Id = 0, Name = "Owner B" }, Name = "Owner B" }
            ];

            IRenderedComponent<ObjectGroupCollection<OwnerConnectionReport>> cut =
                Render<ObjectGroupCollection<OwnerConnectionReport>>(parameters => parameters
                    .Add(p => p.Tab, RsbTab.usedObj)
                    .Add(p => p.PageSize, 0)
                    .Add(p => p.Data, ownerReports)
                    .Add(p => p.NameExtractor, ownerReport => ownerReport.Name ?? string.Empty)
                    .Add(p => p.NetworkObjectExtractor, _ => Array.Empty<NetworkObject>())
                    .Add(p => p.NetworkServiceExtractor, _ => Array.Empty<NetworkService>())
                    .Add(p => p.NetworkUserExtractor, _ => Array.Empty<NetworkUser>()));

            Assert.That(cut.Markup, Does.Contain("Owner A"));
            Assert.That(cut.Markup, Does.Contain("Owner B"));
        }
    }
}
