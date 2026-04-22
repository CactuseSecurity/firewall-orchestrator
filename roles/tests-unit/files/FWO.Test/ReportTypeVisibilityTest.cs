using System.Security.Claims;
using FWO.Basics;
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
            ReportVisibility visibility = RoleGroups.GetReportVisibility(BuildPrincipal(Roles.Modeller));

            Assert.That(visibility.RuleRelated, Is.False);
            Assert.That(visibility.ModellingRelated, Is.True);
            Assert.That(visibility.ComplianceRelated, Is.False);
            Assert.That(visibility.WorkflowRelated, Is.False);
        }

        [Test]
        public void CustomSortReportType_FiltersUnorderedComplianceAndArchiveOnlyReports()
        {
            ReportVisibility visibility = RoleGroups.GetReportVisibility(BuildPrincipal(Roles.Modeller));
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
        public void ReportTypeSelection_DoesNotContainArchiveOnlyReport()
        {
            List<ReportType> reportTypes = ReportTypeGroups.ReportTypeSelection(new(true, true, true, true));

            Assert.That(reportTypes, Does.Not.Contain(ReportType.RecertificationEvent));
        }

        private static ClaimsPrincipal BuildPrincipal(params string[] roles)
        {
            ClaimsIdentity identity = new(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                authenticationType: "test",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);

            return new ClaimsPrincipal(identity);
        }
    }
}
