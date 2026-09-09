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

        [Test]
        public void CanUseReportType_ExplicitNotVisibleHidesOtherwiseVisibleReportType()
        {
            UserConfig userConfig = BuildUserConfig(Roles.Modeller);
            userConfig.ReportTypeVisibilityByRole = ReportTypeRoleVisibilityConfig.Serialize(new()
            {
                [Roles.Modeller] = new() { [ReportType.Connections] = ReportTypeVisibilityOption.NotVisible }
            });

            Assert.That(userConfig.CanUseReportType(ReportType.Connections), Is.False);
        }

        [Test]
        public void CanUseReportType_ExplicitVisibleShowsOtherwiseHiddenReportType()
        {
            UserConfig userConfig = BuildUserConfig(Roles.Modeller);
            userConfig.ReportTypeVisibilityByRole = ReportTypeRoleVisibilityConfig.Serialize(new()
            {
                [Roles.Modeller] = new() { [ReportType.Rules] = ReportTypeVisibilityOption.Visible }
            });

            Assert.That(userConfig.CanUseReportType(ReportType.Rules), Is.True);
        }

        [Test]
        public void CanUseReportType_ExplicitVisibleStillHonoursModellingOwnerScoping()
        {
            UserConfig userConfig = BuildUserConfig(Roles.Modeller);
            userConfig.ReportTypeVisibilityByRole = ReportTypeRoleVisibilityConfig.Serialize(new()
            {
                [Roles.Modeller] = new() { [ReportType.Connections] = ReportTypeVisibilityOption.Visible }
            });

            Assert.That(userConfig.CanUseReportType(ReportType.Connections, modellingOwnerAllowed: false), Is.False);
            Assert.That(userConfig.CanUseReportType(ReportType.Connections, modellingOwnerAllowed: true), Is.True);
        }

        [Test]
        public void CanUseReportType_InheritedMatchesStandardCategoryRules()
        {
            UserConfig userConfig = BuildUserConfig(Roles.Modeller);
            userConfig.AvailableReportTypes = System.Text.Json.JsonSerializer.Serialize(new List<ReportType> { ReportType.Rules, ReportType.Connections });
            userConfig.ReportTypeVisibilityByRole = ReportTypeRoleVisibilityConfig.Serialize(new()
            {
                [Roles.Modeller] = new() { [ReportType.Rules] = ReportTypeVisibilityOption.Inherited }
            });

            Assert.That(userConfig.CanUseReportType(ReportType.Rules), Is.False);
            Assert.That(userConfig.CanUseReportType(ReportType.Connections), Is.True);
        }

        [Test]
        public void CanUseReportType_InheritedIsHiddenWhenNotGloballyAvailable()
        {
            UserConfig userConfig = BuildUserConfig(Roles.Modeller);
            userConfig.AvailableReportTypes = System.Text.Json.JsonSerializer.Serialize(new List<ReportType>());

            Assert.That(userConfig.CanUseReportType(ReportType.Connections), Is.False);
        }

        [Test]
        public void CanUseReportType_ExplicitVisibleOverridesGloballyDisabledReportType()
        {
            UserConfig userConfig = BuildUserConfig(Roles.Modeller);
            userConfig.AvailableReportTypes = System.Text.Json.JsonSerializer.Serialize(new List<ReportType>());
            userConfig.ReportTypeVisibilityByRole = ReportTypeRoleVisibilityConfig.Serialize(new()
            {
                [Roles.Modeller] = new() { [ReportType.Connections] = ReportTypeVisibilityOption.Visible }
            });

            Assert.That(userConfig.CanUseReportType(ReportType.Connections), Is.True);
        }

        [Test]
        public void CanUseReportType_ExplicitNotVisibleOverridesGloballyEnabledReportType()
        {
            UserConfig userConfig = BuildUserConfig(Roles.Modeller);
            userConfig.AvailableReportTypes = System.Text.Json.JsonSerializer.Serialize(new List<ReportType> { ReportType.Connections });
            userConfig.ReportTypeVisibilityByRole = ReportTypeRoleVisibilityConfig.Serialize(new()
            {
                [Roles.Modeller] = new() { [ReportType.Connections] = ReportTypeVisibilityOption.NotVisible }
            });

            Assert.That(userConfig.CanUseReportType(ReportType.Connections), Is.False);
        }

        [Test]
        public void GetExplicitlyDeniedRoles_ReturnsOnlyRolesSetToNotVisible()
        {
            UserConfig userConfig = BuildUserConfig(Roles.Modeller, Roles.Recertifier);
            userConfig.ReportTypeVisibilityByRole = ReportTypeRoleVisibilityConfig.Serialize(new()
            {
                [Roles.Modeller] = new() { [ReportType.Rules] = ReportTypeVisibilityOption.NotVisible },
                [Roles.Recertifier] = new() { [ReportType.Rules] = ReportTypeVisibilityOption.Visible }
            });

            Assert.That(userConfig.GetExplicitlyDeniedRoles(ReportType.Rules), Is.EqualTo(new List<string> { Roles.Modeller }));
        }

        private static UserConfig BuildUserConfig(params string[] roles)
        {
            UserConfig userConfig = new();
            userConfig.User.Roles = [.. roles];
            return userConfig;
        }
    }
}
