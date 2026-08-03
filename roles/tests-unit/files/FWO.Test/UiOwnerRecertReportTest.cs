using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
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

            SimulatedUserConfig.DummyTranslate["statistics"] = "Statistics";
            SimulatedUserConfig.DummyTranslate["owner_recert_overview"] = "Owner recertification overview";
            SimulatedUserConfig.DummyTranslate["U4003"] = "Overdue owners";
            SimulatedUserConfig.DummyTranslate["U4004"] = "No overdue owners";
            SimulatedUserConfig.DummyTranslate["U4005"] = "Upcoming owners @@DAYS@@ days";
            SimulatedUserConfig.DummyTranslate["U4006"] = "No upcoming owners @@DAYS@@ days";
            SimulatedUserConfig.DummyTranslate["U4007"] = "Further owners";
            SimulatedUserConfig.DummyTranslate["U4008"] = "Further owners not yet recertified";
            SimulatedUserConfig.DummyTranslate["U4009"] = "Inactive owners";
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
                BuildOwnerReport("EXT-BOOL", "Bool Owner", DateTime.Today.AddDays(-1), new Dictionary<string, string>
                {
                    ["recert_required"] = "true"
                })
            ];

            IRenderedComponent<OwnerRecertReport> cut = Render<OwnerRecertReport>(parameters => parameters
                .Add(p => p.OwnerData, ownerData)
                .Add(p => p.OwnerAddInfoFilter, new AddInfoFilter { Name = "recert_required", Mode = AddInfoFilterMode.display_only })
                .Add(p => p.RecertificationDisplayPeriod, 7));

            Assert.That(cut.Markup, Does.Contain("add_info: recert_required"));
            Assert.That(cut.Markup, Does.Contain("bi bi-check-lg"));
            Assert.That(cut.Markup, Does.Not.Contain(">true<"));
            Assert.That(ownerData[0].Owner.AdditionalInfoValue, Is.EqualTo("true"));
        }

        [Test]
        public void OwnerRecertReport_AddInfoFilterHidesNonMatchingOwners()
        {
            List<OwnerConnectionReport> ownerData =
            [
                BuildOwnerReport("EXT-A", "A Owner", DateTime.Today.AddDays(-1), new Dictionary<string, string> { ["department"] = "A" }),
                BuildOwnerReport("EXT-B", "B Owner", DateTime.Today.AddDays(-1), new Dictionary<string, string> { ["department"] = "B" })
            ];

            IRenderedComponent<OwnerRecertReport> cut = Render<OwnerRecertReport>(parameters => parameters
                .Add(p => p.OwnerData, ownerData)
                .Add(p => p.OwnerAddInfoFilter, new AddInfoFilter
                {
                    Name = "department",
                    Mode = AddInfoFilterMode.value,
                    Value = "A"
                })
                .Add(p => p.RecertificationDisplayPeriod, 7));

            Assert.That(cut.Markup, Does.Contain("EXT-A"));
            Assert.That(cut.Markup, Does.Not.Contain("EXT-B"));
        }

        [Test]
        public void OwnerRecertReport_UsesOwnerAdditionalInfoKeyWhenAddInfoFilterIsMissing()
        {
            List<OwnerConnectionReport> ownerData =
            [
                BuildOwnerReport("EXT-KEY", "Key Owner", DateTime.Today.AddDays(-1), new Dictionary<string, string>
                {
                    ["business_unit"] = "Payments"
                })
            ];

            IRenderedComponent<OwnerRecertReport> cut = Render<OwnerRecertReport>(parameters => parameters
                .Add(p => p.OwnerData, ownerData)
                .Add(p => p.OwnerAdditionalInfoKey, "business_unit")
                .Add(p => p.RecertificationDisplayPeriod, 7));

            Assert.Multiple(() =>
            {
                Assert.That(cut.Markup, Does.Contain("add_info: business_unit"));
                Assert.That(cut.Markup, Does.Contain("Payments"));
                Assert.That(ownerData[0].Owner.AdditionalInfoValue, Is.EqualTo("Payments"));
            });
        }

        [Test]
        public void OwnerRecertReport_ShowsSplitSectionsForAllOwnerGroups()
        {
            List<OwnerConnectionReport> ownerData =
            [
                BuildOwnerReport("EXT-OVERDUE", "Overdue Owner", DateTime.Today.AddDays(-1)),
                BuildOwnerReport("EXT-UPCOMING", "Upcoming Owner", DateTime.Today.AddDays(3)),
                BuildOwnerReport("EXT-FURTHER", "Further Owner", null),
                BuildOwnerReport("EXT-INACTIVE", "Inactive Owner", null)
            ];
            ownerData[3].Owner.RecertActive = false;

            IRenderedComponent<OwnerRecertReport> cut = Render<OwnerRecertReport>(parameters => parameters
                .Add(p => p.OwnerData, ownerData)
                .Add(p => p.MergeOwnerRecertTables, false)
                .Add(p => p.RecertificationDisplayPeriod, 7));

            Assert.Multiple(() =>
            {
                Assert.That(cut.Markup, Does.Contain("Overdue owners"));
                Assert.That(cut.Markup, Does.Contain("Upcoming owners 7 days"));
                Assert.That(cut.Markup, Does.Contain("Further owners not yet recertified"));
                Assert.That(cut.Markup, Does.Contain("Inactive owners"));
                Assert.That(cut.Markup, Does.Contain("EXT-INACTIVE"));
            });
        }

        private static OwnerConnectionReport BuildOwnerReport(string extAppId, string name, DateTime? nextRecertDate,
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
