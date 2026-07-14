using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiOwnerRecertReportTest : BunitContext
    {
        [SetUp]
        public void Setup()
        {
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddScoped(_ => JSInterop.JSRuntime);
            Services.AddLocalization();
        }

        [Test]
        public void OwnerRecertReport_MergedTableIsSortedByNextRecertDate()
        {
            List<OwnerConnectionReport> ownerData =
            [
                BuildOwnerReport("EXT-LATE", "Late Owner", DateTime.Today.AddDays(20)),
                BuildOwnerReport("EXT-EARLY", "Early Owner", DateTime.Today.AddDays(-5)),
                BuildOwnerReport("EXT-MID", "Mid Owner", DateTime.Today.AddDays(3))
            ];

            IRenderedComponent<OwnerRecertReport> cut = Render<OwnerRecertReport>(parameters => parameters
                .Add(p => p.OwnerData, ownerData)
                .Add(p => p.MergeOwnerRecertTables, true)
                .Add(p => p.RecertificationDisplayPeriod, 7));

            string markup = cut.Markup;
            Assert.That(markup.IndexOf("EXT-EARLY", StringComparison.Ordinal), Is.LessThan(markup.IndexOf("EXT-MID", StringComparison.Ordinal)));
            Assert.That(markup.IndexOf("EXT-MID", StringComparison.Ordinal), Is.LessThan(markup.IndexOf("EXT-LATE", StringComparison.Ordinal)));
        }

        [Test]
        public void OwnerRecertReport_BooleanAdditionalInfoUsesShowAsHtml()
        {
            List<OwnerConnectionReport> ownerData =
            [
                BuildOwnerReport("EXT-BOOL", "Bool Owner", DateTime.Today.AddDays(-1), new()
                {
                    ["recert_required"] = "true"
                })
            ];

            IRenderedComponent<OwnerRecertReport> cut = Render<OwnerRecertReport>(parameters => parameters
                .Add(p => p.OwnerData, ownerData)
                .Add(p => p.OwnerAdditionalInfoKey, "recert_required")
                .Add(p => p.RecertificationDisplayPeriod, 7));

            Assert.That(cut.Markup, Does.Contain("Label: recert_required"));
            Assert.That(cut.Markup, Does.Contain("bi bi-check-lg"));
            Assert.That(cut.Markup, Does.Not.Contain(">true<"));
        }

        private static OwnerConnectionReport BuildOwnerReport(string extAppId, string name, DateTime nextRecertDate,
            Dictionary<string, string>? additionalInfo = null)
        {
            return new()
            {
                Owner = new()
                {
                    Id = Math.Abs(extAppId.GetHashCode()),
                    ExtAppId = extAppId,
                    Name = name,
                    RecertActive = true,
                    NextRecertDate = nextRecertDate,
                    RecertOverdue = nextRecertDate < DateTime.Today,
                    RecertUpcoming = nextRecertDate >= DateTime.Today && nextRecertDate < DateTime.Today.AddDays(7),
                    AdditionalInfo = additionalInfo
                }
            };
        }
    }
}
