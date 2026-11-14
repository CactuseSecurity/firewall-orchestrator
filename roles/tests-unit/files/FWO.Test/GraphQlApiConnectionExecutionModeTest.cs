using FWO.Api.Client;
using FWO.Basics;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    public class GraphQlApiConnectionExecutionModeTest
    {
        private static readonly List<string> ModellingPageRoles = [Roles.Modeller, Roles.Admin, Roles.Auditor];
        private static readonly List<string> ReportPageRoles = [Roles.ReporterViewAll, Roles.Reporter, Roles.Modeller, Roles.Admin, Roles.Auditor];
        private static readonly List<string> ReporterOnlyPageRoles = [Roles.Reporter];
        private static readonly List<string> NoPageRoles = [];

        private static ClaimsPrincipal CreateReporterModeller()
        {
            return new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Reporter),
                new Claim(ClaimTypes.Role, Roles.Modeller)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));
        }

        [Test]
        public void SetAmbientRoleProviderResolvesTheRolesAtTheTimeOfTheRequest()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = CreateReporterModeller();
            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);
            List<string> currentPageRoles = ModellingPageRoles;

            connection.SetAmbientRoleProvider(user, () => currentPageRoles);
            string roleOnModellingPage = connection.GetActRole();
            currentPageRoles = ReporterOnlyPageRoles;
            string roleOnReportPage = connection.GetActRole();

            Assert.Multiple(() =>
            {
                Assert.That(roleOnModellingPage, Is.EqualTo(Roles.Modeller));
                Assert.That(roleOnReportPage, Is.EqualTo(Roles.Reporter));
            });
        }

        [Test]
        public void SetAmbientRoleProviderPrefersTheFirstAcceptedRoleOfTheUser()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = CreateReporterModeller();
            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);

            connection.SetAmbientRoleProvider(user, () => ReportPageRoles);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Reporter));
        }

        [Test]
        public void SetAmbientRoleKeepsTheTargetRolesItWasGiven()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = CreateReporterModeller();
            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);
            List<string> targetRoles = [.. ModellingPageRoles];

            connection.SetAmbientRole(user, targetRoles);
            targetRoles.Clear();

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Modeller));
        }

        [Test]
        public void SetAmbientRoleProviderWithoutAcceptedRolesFallsBackToBaselineRole()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = CreateReporterModeller();
            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);

            connection.SetAmbientRoleProvider(user, () => NoPageRoles);

            Assert.That(connection.GetActRole(), Is.Empty);
        }

        [Test]
        public void SetAmbientRoleProviderFailureFallsBackToBaselineRole()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = CreateReporterModeller();
            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);

            connection.SetAmbientRoleProvider(user, () => throw new InvalidOperationException("navigation manager gone"));

            string role = "not resolved";
            Assert.DoesNotThrow(() => role = connection.GetActRole());
            Assert.That(role, Is.Empty);
        }

        [Test]
        public void SetExecutionModeClearsTheAmbientRoleProvider()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = CreateReporterModeller();
            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);
            connection.SetAmbientRoleProvider(user, () => ModellingPageRoles);

            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);

            Assert.That(connection.GetActRole(), Is.Empty);
        }

        [Test]
        public void ExplicitRoleTakesPrecedenceOverAmbientRoleProvider()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = CreateReporterModeller();
            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);
            connection.SetAmbientRoleProvider(user, () => ModellingPageRoles);

            connection.SetRole(Roles.Reporter);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Reporter));
        }

        [Test]
        public void SetBestRoleUsesRoleClaims()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "recertifier")
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetBestRole(user, ["modeller", "recertifier", "admin"]);

            Assert.That(connection.GetActRole(), Is.EqualTo("recertifier"));
        }

        [Test]
        public void SetRoleSetsAndClearsRoleHeader()
        {
            using GraphQlApiConnection connection = new("http://localhost");

            connection.SetRole(Roles.Modeller);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Modeller));
            Assert.That(connection.IsActRole(Roles.Modeller), Is.True);
            Assert.That(connection.IsActRole(Roles.Admin), Is.False);

            connection.SetRole("");

            Assert.That(connection.GetActRole(), Is.Empty);
        }

        [Test]
        public void SetAuthHeaderRaisesEvent()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            object? senderFromEvent = null;
            string? jwtFromEvent = null;
            connection.OnAuthHeaderChanged += (sender, jwt) =>
            {
                senderFromEvent = sender;
                jwtFromEvent = jwt;
            };

            connection.SetAuthHeader("jwt");

            Assert.That(senderFromEvent, Is.SameAs(connection));
            Assert.That(jwtFromEvent, Is.EqualTo("jwt"));
        }

        [Test]
        public async Task ReconnectSubscriptionsAsyncPreservesRoleHeader()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            connection.SetRole(Roles.Modeller);

            await connection.ReconnectSubscriptionsAsync("jwt", CancellationToken.None);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Modeller));
        }

        [Test]
        public void SetBestRoleUsesUserRoleInsteadOfAdminInNormalMode()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Admin),
                new Claim(ClaimTypes.Role, Roles.Modeller)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetBestRole(user, [Roles.Admin, Roles.Modeller]);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Modeller));
        }

        [Test]
        public void SetBestRoleMatchesTargetRolesCaseInsensitively()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Modeller)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetBestRole(user, [Roles.Modeller.ToUpperInvariant()]);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Modeller.ToUpperInvariant()));
        }

        [Test]
        public void SetBestRoleRejectsAdminOnlyTargetForMixedRoleUserInNormalMode()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Admin),
                new Claim(ClaimTypes.Role, Roles.Modeller)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            Assert.Throws<System.Security.Authentication.AuthenticationException>(() =>
                connection.SetBestRole(user, [Roles.Admin]));

            Assert.That(connection.GetActRole(), Is.EqualTo(""));
        }

        [Test]
        public void SetAmbientRoleDoesNotFallBackToElevatedRoleForMixedUserInNormalMode()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Auditor),
                new Claim(ClaimTypes.Role, Roles.Modeller)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));
            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);

            connection.SetAmbientRole(user, [Roles.Admin, Roles.Auditor]);

            Assert.That(connection.GetActRole(), Is.Empty);
        }

        [Test]
        public void SetAmbientRoleAllowsElevatedRoleForElevatedOnlyUser()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Auditor)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));
            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);

            connection.SetAmbientRole(user, [Roles.Admin, Roles.Auditor]);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Auditor));
        }

        [Test]
        public void SetBestRoleParsesHasuraAllowedRolesJsonArrayClaim()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim("x-hasura-allowed-roles", "[\"auditor\",\"admin\"]")
            ], "test"));

            connection.SetBestRole(user, ["modeller", "admin"]);

            Assert.That(connection.GetActRole(), Is.EqualTo("admin"));
        }

        [Test]
        public void SetBestRoleParsesSingleHasuraAllowedRoleClaim()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim("x-hasura-allowed-roles", Roles.Modeller)
            ], "test"));

            connection.SetBestRole(user, [Roles.Modeller]);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Modeller));
        }

        [Test]
        public void SetBestRoleIgnoresBlankRolesInHasuraAllowedRolesArray()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim("x-hasura-allowed-roles", $"[\"\", \"{Roles.Modeller}\"]")
            ], "test"));

            connection.SetBestRole(user, [Roles.Modeller]);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Modeller));
        }

        [Test]
        public void SetBestRoleParsesNamespacedHasuraAllowedRolesClaim()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim("https://hasura.io/jwt/claims/x-hasura-allowed-roles", "[\"auditor\",\"modeller\"]")
            ], "test"));

            connection.SetBestRole(user, ["recertifier", "modeller"]);

            Assert.That(connection.GetActRole(), Is.EqualTo("modeller"));
        }

        [Test]
        public void SetBestRoleUsesCustomRoleClaimType()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim("roles", Roles.Modeller)
            ], "test", ClaimTypes.Name, "roles"));

            connection.SetBestRole(user, [Roles.Modeller]);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Modeller));
        }

        [Test]
        public void SetBestRoleAllowsAdminForAdminOnlyUserInNormalMode()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Admin)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetBestRole(user, [Roles.Admin]);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Admin));
        }

        [Test]
        public void SetBestRoleAllowsAdminWhenOnlyTechnicalRoleExistsBesideAdmin()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Admin),
                new Claim(ClaimTypes.Role, Roles.Anonymous)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetBestRole(user, [Roles.Admin]);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Admin));
        }

        [Test]
        public void RunWithBestRoleRestoresRoleAfterException()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "admin")
            ], "test", ClaimTypes.Name, ClaimTypes.Role));
            connection.SetRole("modeller");

            Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.RunWithBestRole(user, ["admin"], async () =>
            {
                Assert.That(connection.GetActRole(), Is.EqualTo("admin"));
                await Task.CompletedTask;
                throw new InvalidOperationException("test");
            }));

            Assert.That(connection.GetActRole(), Is.EqualTo("modeller"));
        }

        [Test]
        public async Task NestedRoleSwitchesRestoreInOrder()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "admin")
            ], "test", ClaimTypes.Name, ClaimTypes.Role));
            connection.SetRole("auditor");

            await connection.RunWithRole("modeller", async () =>
            {
                Assert.That(connection.GetActRole(), Is.EqualTo("modeller"));
                await connection.RunWithBestRole(user, ["admin"], async () =>
                {
                    Assert.That(connection.GetActRole(), Is.EqualTo("admin"));
                    await Task.CompletedTask;
                });
                Assert.That(connection.GetActRole(), Is.EqualTo("modeller"));
            });

            Assert.That(connection.GetActRole(), Is.EqualTo("auditor"));
        }

        [Test]
        public void ClearingExecutionModeRestoresNormalRoleSwitching()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Admin),
                new Claim(ClaimTypes.Role, Roles.Modeller)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetExecutionMode(user, Roles.Admin);
            connection.SetExecutionMode(user, "");
            connection.SetBestRole(user, [Roles.Modeller]);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Modeller));

            connection.SwitchBack();

            Assert.That(connection.GetActRole(), Is.EqualTo(""));
        }

        [Test]
        public void SetExecutionModeWithUserRejectsDisallowedElevatedRole()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Modeller)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            Assert.Throws<System.Security.Authentication.AuthenticationException>(() =>
                connection.SetExecutionMode(user, Roles.Admin));

            Assert.That(connection.GetActRole(), Is.EqualTo(""));
        }

        [Test]
        public void SetExecutionModeWithUserAcceptsHasuraAllowedElevatedRole()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim("x-hasura-allowed-roles", $"[\"{Roles.Auditor}\"]")
            ], "test"));

            connection.SetExecutionMode(user, Roles.Auditor);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Auditor));
        }

        [Test]
        public void SetExecutionModeMatchesElevatedRoleCaseInsensitively()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Admin)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetExecutionMode(user, Roles.Admin.ToUpperInvariant());

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Admin));
        }

        [Test]
        public void SetBestRoleKeepsForcedAdminRole()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Admin),
                new Claim(ClaimTypes.Role, Roles.Modeller)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetExecutionMode(user, Roles.Admin);
            connection.SetBestRole(user, [Roles.Modeller]);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Admin));

            connection.SwitchBack();

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Admin));
        }

        [Test]
        public async Task RunWithBestRoleKeepsForcedAuditorRole()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Admin),
                new Claim(ClaimTypes.Role, Roles.Auditor)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetExecutionMode(user, Roles.Auditor);

            await connection.RunWithBestRole(user, [Roles.Admin], async () =>
            {
                Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Auditor));
                await Task.CompletedTask;
            });

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Auditor));
        }

        [Test]
        public void SetRoleKeepsForcedAdminRoleUntilExecutionModeIsCleared()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Admin),
                new Claim(ClaimTypes.Role, Roles.Modeller)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetExecutionMode(user, Roles.Admin);
            connection.SetRole(Roles.Modeller);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Admin));

            connection.SwitchBack();

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Admin));

            connection.SetExecutionMode(user, "");
            connection.SetRole(Roles.Modeller);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Modeller));
        }

        [Test]
        public void SetRoleRejectsElevatedRoleInUserRolesModeForMixedUser()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Admin),
                new Claim(ClaimTypes.Role, Roles.Modeller)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);

            Assert.Throws<System.Security.Authentication.AuthenticationException>(() =>
                connection.SetRole(Roles.Admin));

            connection.SwitchBack();

            Assert.That(connection.GetActRole(), Is.EqualTo(""));
        }

        [Test]
        public void SetRoleAllowsElevatedRoleForUserWithoutUserRoleSelection()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Admin),
                new Claim(ClaimTypes.Role, Roles.Auditor)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);
            connection.SetRole(Roles.Auditor);

            Assert.That(connection.GetActRole(), Is.EqualTo(Roles.Admin));
        }

        [Test]
        public void SetBestRoleDoesNotPushRoleWhenNoTargetRoleIsAllowed()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, Roles.Reporter)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetRole(Roles.Auditor);

            Assert.Throws<System.Security.Authentication.AuthenticationException>(() =>
                connection.SetBestRole(user, [Roles.Admin]));

            connection.SwitchBack();

            Assert.That(connection.GetActRole(), Is.EqualTo(""));
        }

        [Test]
        public void SwitchBackWithoutPreviousRoleKeepsCurrentRole()
        {
            using GraphQlApiConnection connection = new("http://localhost");

            connection.SwitchBack();

            Assert.That(connection.GetActRole(), Is.EqualTo(""));
        }
    }
}
