using FWO.Basics;
using NUnit.Framework;
using System.Collections.Generic;

namespace FWO.Test
{
    [TestFixture]
    public class OwnerResponsibleRoleHelperTest
    {
        [Test]
        public void FilterRolesRemovesModellingAndRecertWhenDisallowed()
        {
            List<string> roles = [Roles.Modeller, Roles.Recertifier, Roles.Reporter];

            List<string> filtered = OwnerResponsibleRoleHelper.FilterRoles(roles, false, false);

            Assert.That(filtered, Is.EquivalentTo(new[] { Roles.Reporter }));
        }

        [Test]
        public void FilterRolesKeepsAllRolesWhenAllowed()
        {
            List<string> roles = [Roles.Modeller, Roles.Recertifier, Roles.Reporter];

            List<string> filtered = OwnerResponsibleRoleHelper.FilterRoles(roles, true, true);

            Assert.That(filtered, Is.EquivalentTo(roles));
        }

        [Test]
        public void FilterRolesRemovesModellingOnlyWhenModellingIsDisallowed()
        {
            List<string> roles = [Roles.Modeller, Roles.Recertifier, Roles.Reporter];

            List<string> filtered = OwnerResponsibleRoleHelper.FilterRoles(roles, false, true);

            Assert.That(filtered, Is.EquivalentTo(new[] { Roles.Recertifier, Roles.Reporter }));
        }

        [Test]
        public void FilterRolesRemovesRecertificationOnlyWhenRecertificationIsDisallowed()
        {
            List<string> roles = [Roles.Modeller, Roles.Recertifier, Roles.Reporter];

            List<string> filtered = OwnerResponsibleRoleHelper.FilterRoles(roles, true, false);

            Assert.That(filtered, Is.EquivalentTo(new[] { Roles.Modeller, Roles.Reporter }));
        }
    }
}
