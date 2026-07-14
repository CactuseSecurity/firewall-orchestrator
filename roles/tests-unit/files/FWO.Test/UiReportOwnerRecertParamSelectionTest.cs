using Bunit;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiReportOwnerRecertParamSelectionTest : BunitContext
    {
        [SetUp]
        public void Setup()
        {
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddLocalization();
        }

        [Test]
        public void ReportOwnerRecertParamSelection_RendersMergeAndLabelFields()
        {
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, new ModellingFilter())
                .Add(p => p.UseLightText, false));

            Assert.That(cut.Find("#mergeOwnerRecertTables"), Is.Not.Null);
            Assert.That(cut.Find("#ownerAdditionalInfoKey"), Is.Not.Null);
            Assert.That(cut.Markup, Does.Contain("Merge all displayed tables"));
            Assert.That(cut.Markup, Does.Contain("Label"));
        }

        [Test]
        public void ReportOwnerRecertParamSelection_UpdatesMergeFlag()
        {
            ModellingFilter filter = new();
            ModellingFilter? changedFilter = null;
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, filter)
                .Add(p => p.ModellingFilterChanged, updated => changedFilter = updated));

            cut.Find("#mergeOwnerRecertTables").Change(true);

            Assert.That(filter.MergeOwnerRecertTables, Is.True);
            Assert.That(changedFilter, Is.SameAs(filter));
        }

        [Test]
        public void ReportOwnerRecertParamSelection_UpdatesOwnerAdditionalInfoKey()
        {
            ModellingFilter filter = new();
            ModellingFilter? changedFilter = null;
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, filter)
                .Add(p => p.ModellingFilterChanged, updated => changedFilter = updated));

            cut.Find("#ownerAdditionalInfoKey").Change(" business_unit ");

            Assert.That(filter.OwnerAdditionalInfoKey, Is.EqualTo("business_unit"));
            Assert.That(changedFilter, Is.SameAs(filter));
        }
    }
}
