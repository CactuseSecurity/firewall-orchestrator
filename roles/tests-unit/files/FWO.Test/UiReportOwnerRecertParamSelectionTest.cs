using Bunit;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiReportOwnerRecertParamSelectionTest
    {
        [Test]
        public async Task ReportOwnerRecertParamSelection_RendersMergeAndLabelFields()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = context.Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, new ModellingFilter())
                .Add(p => p.UseLightText, false));

            Assert.That(cut.Find("#mergeOwnerRecertTables"), Is.Not.Null);
            Assert.That(cut.Find("#ownerAdditionalInfoKey"), Is.Not.Null);
            Assert.That(cut.Markup, Does.Contain("Merge all displayed tables"));
            Assert.That(cut.Markup, Does.Contain("Label"));
        }

        [Test]
        public async Task ReportOwnerRecertParamSelection_UpdatesMergeFlag()
        {
            await using BunitContext context = CreateContext();
            ModellingFilter filter = new();
            ModellingFilter? changedFilter = null;
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = context.Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, filter)
                .Add(p => p.ModellingFilterChanged, updated => changedFilter = updated));

            cut.Find("#mergeOwnerRecertTables").Change(true);

            Assert.That(filter.MergeOwnerRecertTables, Is.True);
            Assert.That(changedFilter, Is.SameAs(filter));
        }

        [Test]
        public async Task ReportOwnerRecertParamSelection_UpdatesOwnerAdditionalInfoKey()
        {
            await using BunitContext context = CreateContext();
            ModellingFilter filter = new();
            ModellingFilter? changedFilter = null;
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = context.Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, filter)
                .Add(p => p.ModellingFilterChanged, updated => changedFilter = updated));

            cut.Find("#ownerAdditionalInfoKey").Change(" business_unit ");

            Assert.That(filter.OwnerAdditionalInfoKey, Is.EqualTo("business_unit"));
            Assert.That(changedFilter, Is.SameAs(filter));
        }

        [Test]
        public async Task ReportOwnerRecertParamSelection_UpdatesShowAllAndInactiveFlagsInFormLayout()
        {
            await using BunitContext context = CreateContext();
            ModellingFilter filter = new();
            ModellingFilter? changedFilter = null;
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = context.Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, filter)
                .Add(p => p.ModellingFilterChanged, updated => changedFilter = updated)
                .Add(p => p.UseFormLayout, true)
                .Add(p => p.UseLightText, false));

            Assert.That(cut.Markup, Does.Contain("form-group row mt-2"));

            cut.Find("#allOwners").Change(true);
            Assert.Multiple(() =>
            {
                Assert.That(filter.ShowAllOwners, Is.True);
                Assert.That(changedFilter, Is.SameAs(filter));
            });

            cut.Find("#showInactiveRecertOwners").Change(true);
            Assert.Multiple(() =>
            {
                Assert.That(filter.ShowInactiveRecertOwners, Is.True);
                Assert.That(changedFilter, Is.SameAs(filter));
            });
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddLocalization();
            return context;
        }
    }
}
