using FWO.Basics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test;

/// <summary>
/// Tests the authorization policy of the services that accept the middleware server's JWTs for FWO users only.
/// </summary>
[TestFixture]
internal class FwoUserPolicyTest
{
    private const string kAuthenticationType = "test";
    private const string kUserName = "test-user";

    /// <summary>
    /// Verifies that a user role gives access, while the anonymous and the technical roles do not.
    /// </summary>
    [TestCase(Roles.Reporter, true)]
    [TestCase(Roles.Admin, true)]
    [TestCase(Roles.Anonymous, false)]
    [TestCase(Roles.MiddlewareServer, false)]
    [TestCase(Roles.Importer, false)]
    [TestCase(Roles.DbBackup, false)]
    [TestCase(Roles.AiHarness, false)]
    public async Task CreateBuilder_AuthorizesUserRolesOnly(string role, bool expectedAuthorized)
    {
        ClaimsPrincipal user = CreateUser(kAuthenticationType, JwtClaimTypes.kRole, role);

        Assert.That(await IsAuthorized(user, FwoUserPolicy.CreateBuilder()), Is.EqualTo(expectedAuthorized));
    }

    /// <summary>
    /// Verifies that one user role is enough, even next to the anonymous role.
    /// </summary>
    [Test]
    public async Task CreateBuilder_AuthorizesUserRoleNextToAnonymousRole()
    {
        ClaimsPrincipal user = CreateUser(kAuthenticationType, JwtClaimTypes.kRole, Roles.Anonymous, Roles.Reporter);

        Assert.That(await IsAuthorized(user, FwoUserPolicy.CreateBuilder()), Is.True);
    }

    /// <summary>
    /// Verifies that a user role does not help an unauthenticated caller.
    /// </summary>
    [Test]
    public async Task CreateBuilder_RefusesUnauthenticatedUser()
    {
        ClaimsPrincipal user = CreateUser(null, JwtClaimTypes.kRole, Roles.Reporter);

        Assert.That(await IsAuthorized(user, FwoUserPolicy.CreateBuilder()), Is.False);
    }

    /// <summary>
    /// Verifies that the roles are read from the claim name written in the token only, not from a mapped claim type.
    /// </summary>
    [Test]
    public async Task CreateBuilder_IgnoresRolesUnderOtherClaimTypes()
    {
        ClaimsPrincipal user = CreateUser(kAuthenticationType, ClaimTypes.Role, Roles.Reporter);

        Assert.That(await IsAuthorized(user, FwoUserPolicy.CreateBuilder()), Is.False);
    }

    /// <summary>
    /// Verifies that requirements a service adds apply on top of the FWO user requirement.
    /// </summary>
    [Test]
    public async Task CreateBuilder_KeepsRequirementsAddedByTheService()
    {
        ClaimsPrincipal user = CreateUser(kAuthenticationType, JwtClaimTypes.kRole, Roles.Reporter);
        AuthorizationPolicyBuilder policyBuilder = FwoUserPolicy.CreateBuilder()
            .RequireAssertion(context => context.User.HasClaim(claim => claim.Type == JwtClaimTypes.kUserDn));

        Assert.That(await IsAuthorized(user, policyBuilder), Is.False);
    }

    private static ClaimsPrincipal CreateUser(string? authenticationType, string roleClaimType, params string[] roles)
    {
        List<Claim> claims = [new Claim(JwtClaimTypes.kName, kUserName), .. roles.Select(role => new Claim(roleClaimType, role))];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType, JwtClaimTypes.kName, JwtClaimTypes.kRole));
    }

    private static async Task<bool> IsAuthorized(ClaimsPrincipal user, AuthorizationPolicyBuilder policyBuilder)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddAuthorization();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IAuthorizationService authorizationService = serviceProvider.GetRequiredService<IAuthorizationService>();
        AuthorizationResult result = await authorizationService.AuthorizeAsync(user, policyBuilder.Build());
        return result.Succeeded;
    }
}
