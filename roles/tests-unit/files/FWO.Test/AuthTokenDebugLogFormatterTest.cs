using FWO.Ui.Auth;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests safe formatting of authentication token debug logs.
/// </summary>
[TestFixture]
public class AuthTokenDebugLogFormatterTest
{
    /// <summary>
    /// Verifies the debug log contains useful token metadata without writing the bearer token.
    /// </summary>
    [Test]
    public void FormatLoginTokenDebugMessage_DoesNotExposeAccessToken()
    {
        const string accessToken = "not-a-real-jwt";

        string message = AuthTokenDebugLogFormatter.FormatLoginTokenDebugMessage("alice", accessToken);

        Assert.That(message, Does.Contain("alice"));
        Assert.That(message, Does.Contain("fingerprint="));
        Assert.That(message, Does.Not.Contain(accessToken));
    }
}
