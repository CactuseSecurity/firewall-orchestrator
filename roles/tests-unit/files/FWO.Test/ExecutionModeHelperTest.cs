using FWO.Api.Client;
using FWO.Basics;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    public class ExecutionModeHelperTest
    {
        [Test]
        public void ShouldShowExecutionModeSelectionForAdminWithAdditionalRole()
        {
            bool result = ExecutionModeHelper.ShouldShowExecutionModeSelection([Roles.Admin, Roles.Modeller]);

            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldShowExecutionModeSelectionForAuditorWithAdditionalRole()
        {
            bool result = ExecutionModeHelper.ShouldShowExecutionModeSelection([Roles.Auditor, Roles.Recertifier]);

            Assert.That(result, Is.True);
        }

        [Test]
        public void ShouldNotShowExecutionModeSelectionWithoutAdminOrAuditor()
        {
            bool result = ExecutionModeHelper.ShouldShowExecutionModeSelection([Roles.Modeller, Roles.Recertifier]);

            Assert.That(result, Is.False);
        }

        [Test]
        public void ShouldNotShowExecutionModeSelectionForSingleAdminRole()
        {
            bool result = ExecutionModeHelper.ShouldShowExecutionModeSelection([Roles.Admin]);

            Assert.That(result, Is.False);
        }

        [Test]
        public void GetSelectableRolesFiltersTechnicalAndDuplicateRoles()
        {
            List<string> result = ExecutionModeHelper.GetSelectableRoles(
            [
                Roles.Admin,
                Roles.Admin.ToUpperInvariant(),
                Roles.Importer,
                Roles.MiddlewareServer,
                Roles.Anonymous,
                Roles.Modeller
            ]);

            Assert.That(result, Is.EquivalentTo(new[] { Roles.Admin, Roles.Modeller }));
        }

        [Test]
        public void GetSelectableExecutionModesIncludesUserRolesAndElevatedRoles()
        {
            List<string> result = ExecutionModeHelper.GetSelectableExecutionModes(
            [
                Roles.Admin,
                Roles.Auditor,
                Roles.Modeller
            ]);

            Assert.That(result, Is.EqualTo(new[] { GlobalConst.kUserRolesSelection, Roles.Admin, Roles.Auditor }));
        }

        [Test]
        public void GetSelectedExecutionModeKeepsCurrentElevatedRole()
        {
            string result = ExecutionModeHelper.GetSelectedExecutionMode([Roles.Admin, Roles.Auditor], Roles.Auditor);

            Assert.That(result, Is.EqualTo(Roles.Auditor));
        }

        [Test]
        public void GetSelectedExecutionModeFallsBackToUserRoles()
        {
            string result = ExecutionModeHelper.GetSelectedExecutionMode([Roles.Modeller, Roles.Admin], "");

            Assert.That(result, Is.EqualTo(GlobalConst.kUserRolesSelection));
        }

        [Test]
        public void NormalizeExecutionModeFallsBackToUserRolesForEmptyOrInvalidMode()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ExecutionModeHelper.NormalizeExecutionMode([Roles.Admin, Roles.Modeller], ""), Is.EqualTo(GlobalConst.kUserRolesSelection));
                Assert.That(ExecutionModeHelper.NormalizeExecutionMode([Roles.Admin, Roles.Modeller], "invalid"), Is.EqualTo(GlobalConst.kUserRolesSelection));
            });
        }

        [Test]
        public void EmptyExecutionModeDoesNotActivateAdminForMixedRoleUsers()
        {
            bool result = ExecutionModeHelper.HasAnyRoleInExecutionMode([Roles.Admin, Roles.Modeller], "", [Roles.Admin]);

            Assert.That(result, Is.False);
        }

        [Test]
        public void AuditorExecutionModeDoesNotActivateWorkflowRoles()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ExecutionModeHelper.HasAnyRoleInExecutionMode([Roles.Auditor, Roles.Approver], Roles.Auditor, [Roles.Auditor]), Is.True);
                Assert.That(ExecutionModeHelper.HasAnyRoleInExecutionMode([Roles.Auditor, Roles.Approver], Roles.Auditor, [Roles.Approver]), Is.False);
            });
        }

        [Test]
        public void AdminExecutionModeDoesNotActivateUserRoles()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ExecutionModeHelper.HasAnyRoleInExecutionMode([Roles.Admin, Roles.Requester], Roles.Admin, [Roles.Admin]), Is.True);
                Assert.That(ExecutionModeHelper.HasAnyRoleInExecutionMode([Roles.Admin, Roles.Requester], Roles.Admin, [Roles.Requester]), Is.False);
            });
        }

        [Test]
        public void GetUserRolesExtractsRoleAndHasuraAllowedRolesClaims()
        {
            ClaimsIdentity identity = new(
                [
                    new Claim(ClaimTypes.Role, Roles.Modeller),
                    new Claim("x-hasura-allowed-roles", $"[\"{Roles.Admin}\",\"{Roles.Modeller}\"]")
                ], "test");
            ClaimsPrincipal user = new(identity);

            List<string> roles = ExecutionModeHelper.GetUserRoles(user);

            Assert.That(roles, Is.EquivalentTo(new[] { Roles.Admin, Roles.Modeller }));
        }
    }
}
