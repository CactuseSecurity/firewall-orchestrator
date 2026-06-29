using FWO.Ui.Auth;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FWO.Test;

/// <summary>
/// Tests safe formatting of authentication token debug logs.
/// </summary>
[TestFixture]
public class AuthTokenDebugLogFormatterTest
{
    /// <summary>
    /// Verifies a readable JWT is summarized by its jti without writing the bearer token itself.
    /// </summary>
    [Test]
    public void FormatLoginTokenDebugMessage_LogsJtiAndDoesNotExposeAccessToken()
    {
        JwtSecurityToken token = new(
            issuer: "fwo-test",
            audience: "fwo-test",
            claims: [new Claim(JwtRegisteredClaimNames.Jti, "test-jti-123")],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(5));
        string accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        string message = AuthTokenDebugLogFormatter.FormatLoginTokenDebugMessage("alice", accessToken);

        Assert.That(message, Does.Contain("alice"));
        Assert.That(message, Does.Contain("jti=test-jti-123"));
        Assert.That(message, Does.Contain("expires="));
        Assert.That(message, Does.Not.Contain("fingerprint"));
        Assert.That(message, Does.Not.Contain(accessToken));
    }

    /// <summary>
    /// Verifies an unreadable token is reported without leaking the raw token value.
    /// </summary>
    [Test]
    public void FormatLoginTokenDebugMessage_WithUnreadableToken_DoesNotExposeAccessToken()
    {
        const string accessToken = "not-a-real-jwt";

        string message = AuthTokenDebugLogFormatter.FormatLoginTokenDebugMessage("alice", accessToken);

        Assert.That(message, Does.Contain("alice"));
        Assert.That(message, Does.Contain("unreadable"));
        Assert.That(message, Does.Not.Contain(accessToken));
    }
}
