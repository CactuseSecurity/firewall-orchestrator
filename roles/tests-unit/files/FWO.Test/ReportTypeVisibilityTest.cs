using FWO.Basics;
using FWO.Config.Api;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ReportTypeVisibilityTest
    {
        [Test]
        public void GetReportVisibility_ModellerOnly_HidesComplianceAndWorkflowReports()
        {
            ReportVisibility visibility = BuildUserConfig(Roles.Modeller).GetReportVisibility();

            Assert.That(visibility.RuleRelated, Is.False);
            Assert.That(visibility.ModellingRelated, Is.True);
            Assert.That(visibility.ComplianceRelated, Is.False);
            Assert.That(visibility.OwnerRelated, Is.False);
            Assert.That(visibility.WorkflowRelated, Is.False);
        }

        [Test]
        public void CustomSortReportType_FiltersUnorderedComplianceAndArchiveOnlyReports()
        {
            ReportVisibility visibility = BuildUserConfig(Roles.Modeller).GetReportVisibility();
            List<ReportType> sortedTypes = ReportTypeGroups.CustomSortReportType(
            [
                ReportType.ComplianceReport,
                ReportType.ComplianceDiffReport,
                ReportType.Connections,
                ReportType.RecertificationEvent,
                ReportType.TicketReport
            ], visibility);

            Assert.That(sortedTypes, Is.EqualTo(new List<ReportType> { ReportType.Connections }));
        }

        [Test]
        public void CustomSortReportType_ReporterDoesNotSeeModellingOverlapOrOwnerReports()
        {
            ReportVisibility visibility = BuildUserConfig(Roles.Reporter).GetReportVisibility();
            List<ReportType> sortedTypes = ReportTypeGroups.CustomSortReportType(
            [
                ReportType.Rules,
                ReportType.AppRules,
                ReportType.RecertEventReport,
                ReportType.Owners,
                ReportType.Connections
            ], visibility);

            Assert.That(sortedTypes, Is.EqualTo(new List<ReportType> { ReportType.Rules }));
        }

        [Test]
        public void CustomSortReportType_AdminSeesOwnersReport()
        {
            ReportVisibility visibility = BuildUserConfig(Roles.Admin).GetReportVisibility();
            List<ReportType> sortedTypes = ReportTypeGroups.CustomSortReportType(
            [
                ReportType.Owners,
                ReportType.Connections
            ], visibility);

            Assert.That(sortedTypes, Does.Contain(ReportType.Owners));
        }

        [Test]
        public void GetReportVisibility_UserRolesModeSuppressesAdminVisibilityWhenUserRoleExists()
        {
            UserConfig userConfig = BuildUserConfig(Roles.Admin, Roles.Modeller);

            ReportVisibility visibility = userConfig.GetReportVisibility();

            Assert.That(visibility.OwnerRelated, Is.False);
            Assert.That(visibility.ComplianceRelated, Is.False);
            Assert.That(visibility.ModellingRelated, Is.True);
        }

        [Test]
        public void GetReportVisibility_AdminModeEnablesAdminVisibility()
        {
            UserConfig userConfig = BuildUserConfig(Roles.Admin, Roles.Modeller);
            userConfig.SetExecutionMode(Roles.Admin);

            ReportVisibility visibility = userConfig.GetReportVisibility();

            Assert.That(visibility.OwnerRelated, Is.True);
            Assert.That(visibility.ComplianceRelated, Is.True);
            Assert.That(visibility.ModellingRelated, Is.True);
        }

        [Test]
        public void ReportTypeSelection_DoesNotContainArchiveOnlyReport()
        {
            List<ReportType> reportTypes = ReportTypeGroups.ReportTypeSelection(new(true, true, true, true, true));

            Assert.That(reportTypes, Does.Not.Contain(ReportType.RecertificationEvent));
        }

        private static UserConfig BuildUserConfig(params string[] roles)
        {
            UserConfig userConfig = new();
            userConfig.User.Roles = [.. roles];
            return userConfig;
        }
    }
}
