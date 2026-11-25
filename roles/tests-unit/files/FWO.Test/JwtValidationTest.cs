using FWO.Basics;
using FWO.Config.File;
using FWO.Data;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.Security.Claims;
using System.Security.Cryptography;
using JwtConstants = FWO.Basics.JwtConstants;

namespace FWO.Test;

/// <summary>
/// Tests the validation of the middleware server's JWTs by the services accepting them for FWO users (AI MCP server,
/// AI harness): the validation parameters, and tokens written by <see cref="JwtWriter"/> validated with them.
/// </summary>
[TestFixture]
internal class JwtValidationTest
{
    private const int kRsaKeySize = 2048;
    private const string kUserName = "test-user";
    private const string kUserDn = "uid=test-user,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal";
    private static readonly List<string> kExpectedAudiences = [JwtConstants.Audience];
    private static readonly List<string> kExpectedIssuers = [JwtConstants.Issuer];
    private static readonly List<string> kUserRoles = [Roles.Reporter, Roles.Modeller];
    private static readonly TimeSpan kTokenLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Verifies that only signed, unexpired tokens of the middleware server's issuer and audience are accepted, and
    /// that name and roles are read from the claim names written in the token.
    /// </summary>
    [Test]
    public void CreateParameters_AcceptsOnlySignedUnexpiredTokensOfTheMiddleware()
    {
        using RSA rsa = RSA.Create(kRsaKeySize);
        RsaSecurityKey publicKey = new(rsa.ExportParameters(false));

        TokenValidationParameters parameters = JwtValidation.CreateParameters(publicKey);

        Assert.Multiple(() =>
        {
            Assert.That(parameters.RequireSignedTokens, Is.True);
            Assert.That(parameters.RequireExpirationTime, Is.True);
            Assert.That(parameters.ValidateLifetime, Is.True);
            Assert.That(parameters.ValidateAudience, Is.True);
            Assert.That(parameters.ValidAudiences, Is.EquivalentTo(kExpectedAudiences));
            Assert.That(parameters.ValidateIssuer, Is.True);
            Assert.That(parameters.ValidIssuers, Is.EquivalentTo(kExpectedIssuers));
            Assert.That(parameters.IssuerSigningKey, Is.SameAs(publicKey));
            Assert.That(parameters.NameClaimType, Is.EqualTo("unique_name"));
            Assert.That(parameters.RoleClaimType, Is.EqualTo("role"));
        });
    }

    /// <summary>
    /// Verifies that the parameters cannot be created without a signing key.
    /// </summary>
    [Test]
    public void CreateParameters_WithoutKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => JwtValidation.CreateParameters(null!));
    }

    /// <summary>
    /// Verifies that a token of a user, as written by the middleware server, is valid and carries the user's name,
    /// roles and directory identity under the shared claim names, and that the user passes the FWO user policy.
    /// </summary>
    [Test]
    public async Task UserTokenOfTheMiddleware_IsValidAndAuthorized()
    {
        using RSA rsa = RSA.Create(kRsaKeySize);
        UiUser user = new() { Name = kUserName, Dn = kUserDn, Roles = [.. kUserRoles] };
        string jwt = new JwtWriter(new RsaSecurityKey(rsa)).CreateJWT(user, kTokenLifetime);

        TokenValidationResult result = await Validate(jwt, new RsaSecurityKey(rsa.ExportParameters(false)));
        ClaimsPrincipal principal = new(result.ClaimsIdentity);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(principal.Identity?.Name, Is.EqualTo(kUserName));
            Assert.That(principal.IsInRole(Roles.Reporter), Is.True);
            Assert.That(principal.IsInRole(Roles.Modeller), Is.True);
            Assert.That(principal.FindFirst(JwtClaimTypes.kUserDn)?.Value, Is.EqualTo(kUserDn));
        });
        Assert.That(await IsFwoUser(principal), Is.True);
    }

    /// <summary>
    /// Verifies that the anonymous bootstrap token and the token of a technical user, which the middleware server
    /// signs as well, are valid tokens but do not pass the FWO user policy.
    /// </summary>
    [Test]
    public async Task AnonymousAndTechnicalTokensOfTheMiddleware_AreNotAuthorized()
    {
        using RSA rsa = RSA.Create(kRsaKeySize);
        JwtWriter writer = new(new RsaSecurityKey(rsa));
        RsaSecurityKey publicKey = new(rsa.ExportParameters(false));

        TokenValidationResult anonymous = await Validate(writer.CreateJWT(null, kTokenLifetime), publicKey);
        TokenValidationResult technical = await Validate(writer.CreateJWTMiddlewareServer(kTokenLifetime), publicKey);

        Assert.Multiple(() =>
        {
            Assert.That(anonymous.IsValid, Is.True);
            Assert.That(technical.IsValid, Is.True);
        });
        Assert.That(await IsFwoUser(new ClaimsPrincipal(anonymous.ClaimsIdentity)), Is.False);
        Assert.That(await IsFwoUser(new ClaimsPrincipal(technical.ClaimsIdentity)), Is.False);
    }

    /// <summary>
    /// Verifies that a token signed with another key is refused.
    /// </summary>
    [Test]
    public async Task TokenSignedWithAnotherKey_IsInvalid()
    {
        using RSA signingRsa = RSA.Create(kRsaKeySize);
        using RSA otherRsa = RSA.Create(kRsaKeySize);
        UiUser user = new() { Name = kUserName, Dn = kUserDn, Roles = [.. kUserRoles] };
        string jwt = new JwtWriter(new RsaSecurityKey(signingRsa)).CreateJWT(user, kTokenLifetime);

        TokenValidationResult result = await Validate(jwt, new RsaSecurityKey(otherRsa.ExportParameters(false)));

        Assert.That(result.IsValid, Is.False);
    }

    private static async Task<TokenValidationResult> Validate(string jwt, SecurityKey publicKey)
    {
        // like the JWT bearer authentication of the services (JwtBearerOptions.MapInboundClaims = false)
        JsonWebTokenHandler tokenHandler = new() { MapInboundClaims = false };
        return await tokenHandler.ValidateTokenAsync(jwt, JwtValidation.CreateParameters(publicKey));
    }

    private static async Task<bool> IsFwoUser(ClaimsPrincipal user)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddAuthorization();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IAuthorizationService authorizationService = serviceProvider.GetRequiredService<IAuthorizationService>();
        AuthorizationResult result = await authorizationService.AuthorizeAsync(user, FwoUserPolicy.CreateBuilder().Build());
        return result.Succeeded;
    }
}
