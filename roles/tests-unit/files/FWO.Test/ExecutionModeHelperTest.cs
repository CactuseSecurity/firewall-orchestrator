using FWO.Api.Client;
using FWO.Basics;
using NUnit.Framework;

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

            Assert.That(result, Is.EqualTo(new[] { ExecutionModeHelper.UserRolesSelection, Roles.Admin, Roles.Auditor }));
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

            Assert.That(result, Is.EqualTo(ExecutionModeHelper.UserRolesSelection));
        }
    }
}
