using FWO.Basics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the checks of the endpoints secrets (API keys, client secrets, JWTs) are sent to, and of the listeners of the
/// web servers they are sent to.
/// </summary>
[TestFixture]
[Parallelizable]
internal class EndpointGuardTest
{
    private const string kPurpose = "test endpoint";
    private const string kUserInfo = "admin:s3cr3t";
    private const string kRemoteHost = "middleware.example.com";
    private const string kLoopbackListener = "http://127.0.0.1:8891/";
    private const string kRemoteListener = "http://middleware.example.com:8891/";
    private const string kPorts = "8891";

    /// <summary>
    /// Verifies that https endpoints and http endpoints of loopback addresses are accepted.
    /// </summary>
    [TestCase("https://middleware.example.com:8888/mcp")]
    [TestCase("http://127.0.0.1:8890/mcp")]
    [TestCase("http://[::1]:8890/mcp")]
    [TestCase("http://localhost/mcp")]
    public void RequireHttpsOrLoopback_WithHttpsOrLoopback_Accepts(string endpoint)
    {
        Assert.DoesNotThrow(() => EndpointGuard.RequireHttpsOrLoopback(new Uri(endpoint), kPurpose));
    }

    /// <summary>
    /// Verifies that plain http to a remote host is refused, without naming the endpoint in the message.
    /// </summary>
    [Test]
    public void RequireHttpsOrLoopback_WithRemoteHttp_ThrowsWithoutEndpoint()
    {
        Uri endpoint = new($"http://{kUserInfo}@{kRemoteHost}/mcp");

        ArgumentException exception = Assert.Throws<ArgumentException>(() => EndpointGuard.RequireHttpsOrLoopback(endpoint, kPurpose))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain(kPurpose));
            Assert.That(exception.Message, Does.Not.Contain(kUserInfo));
            Assert.That(exception.Message, Does.Not.Contain(kRemoteHost));
        }
    }

    /// <summary>
    /// Verifies that relative endpoints and other schemes are refused by both checks.
    /// </summary>
    [TestCase("/mcp")]
    [TestCase("ftp://middleware.example.com/mcp")]
    [TestCase("file:///etc/passwd")]
    public void Require_WithRelativeOrOtherScheme_Throws(string endpoint)
    {
        Uri uri = new(endpoint, UriKind.RelativeOrAbsolute);

        Assert.Throws<ArgumentException>(() => EndpointGuard.RequireHttpsOrLoopback(uri, kPurpose));
        Assert.Throws<ArgumentException>(() => EndpointGuard.RequireHttpOrHttps(uri, kPurpose));
    }

    /// <summary>
    /// Verifies that plain http to a remote host is accepted where no secret is sent.
    /// </summary>
    [Test]
    public void RequireHttpOrHttps_WithRemoteHttp_Accepts()
    {
        Assert.DoesNotThrow(() => EndpointGuard.RequireHttpOrHttps(new Uri("http://ollama.example.com:11434/"), kPurpose));
    }

    /// <summary>
    /// Verifies that a missing endpoint is refused by both checks.
    /// </summary>
    [Test]
    public void Require_WithoutEndpoint_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => EndpointGuard.RequireHttpsOrLoopback(null!, kPurpose));
        Assert.Throws<ArgumentNullException>(() => EndpointGuard.RequireHttpOrHttps(null!, kPurpose));
    }

    /// <summary>
    /// Verifies that listeners with https or with plain http bound to loopback only are accepted, also as a list.
    /// </summary>
    [TestCase("http://127.0.0.1:8880/")]
    [TestCase("http://127.0.0.2:8880")]
    [TestCase("http://localhost:8880/")]
    [TestCase("http://LOCALHOST:8880/")]
    [TestCase("http://[::1]:8880/")]
    [TestCase("HTTP://127.0.0.1:8880/")]
    [TestCase("https://middleware.example.com:8880/")]
    [TestCase("https://0.0.0.0:8880/")]
    [TestCase("https://*:8880/")]
    [TestCase("http://127.0.0.1:8880/; http://[::1]:8880/;")]
    public void RequireHttpsOrLoopbackListener_WithHttpsOrLoopback_Accepts(string listenerUrls)
    {
        Assert.DoesNotThrow(() => EndpointGuard.RequireHttpsOrLoopbackListener(listenerUrls, kPurpose));
    }

    /// <summary>
    /// Verifies that plain http listeners that Kestrel binds to every interface are refused: every host name but
    /// localhost (also "loopback", which System.Uri treats as loopback), the wildcards and the unspecified addresses,
    /// also as a single entry of a list.
    /// </summary>
    [TestCase("http://middleware.example.com:8880/")]
    [TestCase("http://loopback:8880/")]
    [TestCase("http://localhost.:8880/")]
    [TestCase("http://10.1.1.1:8880/")]
    [TestCase("http://0.0.0.0:8880/")]
    [TestCase("http://[::]:8880/")]
    [TestCase("http://*:8880/")]
    [TestCase("http://+:8880/")]
    [TestCase("http://127.0.0.1:8880/;http://*:8881/")]
    public void RequireHttpsOrLoopbackListener_WithRemoteHttp_Throws(string listenerUrls)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => EndpointGuard.RequireHttpsOrLoopbackListener(listenerUrls, kPurpose))!;

        Assert.That(exception.Message, Does.Contain(kPurpose));
    }

    /// <summary>
    /// Verifies that listener URLs without a scheme, without a host, with another scheme, or no URL at all are refused.
    /// </summary>
    [TestCase("127.0.0.1:8880")]
    [TestCase("http://:8880/")]
    [TestCase("ftp://127.0.0.1:8880/")]
    [TestCase(";")]
    [TestCase(" ")]
    public void RequireHttpsOrLoopbackListener_WithInvalidUrls_Throws(string listenerUrls)
    {
        Assert.Throws<ArgumentException>(() => EndpointGuard.RequireHttpsOrLoopbackListener(listenerUrls, kPurpose));
    }

    /// <summary>
    /// Verifies that missing listener URLs are refused.
    /// </summary>
    [Test]
    public void RequireHttpsOrLoopbackListener_WithoutUrls_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => EndpointGuard.RequireHttpsOrLoopbackListener((string)null!, kPurpose));
    }

    /// <summary>
    /// Verifies that the listener of a configuration is accepted when its urls bind plain http to loopback only (also
    /// when http ports are set, as the urls take precedence), when only https ports are set, and when nothing is set,
    /// where Kestrel binds to localhost.
    /// </summary>
    [TestCase(kLoopbackListener, null, null)]
    [TestCase(kLoopbackListener, kPorts, null)]
    [TestCase(null, null, kPorts)]
    [TestCase(null, null, null)]
    [TestCase(" ", null, null)]
    public void RequireHttpsOrLoopbackListener_WithLoopbackConfiguration_Accepts(string? urls, string? httpPorts, string? httpsPorts)
    {
        IConfiguration configuration = CreateConfiguration(urls, httpPorts, httpsPorts);

        Assert.DoesNotThrow(() => EndpointGuard.RequireHttpsOrLoopbackListener(configuration, kPurpose));
    }

    /// <summary>
    /// Verifies that the listener of a configuration is refused when its urls bind plain http to more than loopback,
    /// or when, without urls, http ports are set, which Kestrel binds to every interface.
    /// </summary>
    [TestCase(kRemoteListener, null)]
    [TestCase(kRemoteListener, kPorts)]
    [TestCase(null, kPorts)]
    public void RequireHttpsOrLoopbackListener_WithRemoteHttpConfiguration_Throws(string? urls, string? httpPorts)
    {
        IConfiguration configuration = CreateConfiguration(urls, httpPorts, null);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => EndpointGuard.RequireHttpsOrLoopbackListener(configuration, kPurpose))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain(kPurpose));
            Assert.That(exception.Message, Does.Not.Contain(kRemoteHost));
        }
    }

    /// <summary>
    /// Verifies that a missing configuration is refused.
    /// </summary>
    [Test]
    public void RequireHttpsOrLoopbackListener_WithoutConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => EndpointGuard.RequireHttpsOrLoopbackListener((IConfiguration)null!, kPurpose));
    }

    /// <summary>
    /// Verifies that a refused listener is not named in the message, neither when it is refused as plain http to every
    /// interface nor when it cannot be parsed, as it may contain user information.
    /// </summary>
    [TestCase("http://" + kUserInfo + "@" + kRemoteHost + ":8880/")]
    [TestCase(kUserInfo + "@" + kRemoteHost)]
    public void RequireHttpsOrLoopbackListener_WhenRefused_DoesNotNameListener(string listenerUrls)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => EndpointGuard.RequireHttpsOrLoopbackListener(listenerUrls, kPurpose))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Not.Contain(kUserInfo));
            Assert.That(exception.Message, Does.Not.Contain(kRemoteHost));
        }
    }

    /// <summary>
    /// Creates a configuration with the listener settings Kestrel reads; a null value leaves the setting out.
    /// </summary>
    private static IConfiguration CreateConfiguration(string? urls, string? httpPorts, string? httpsPorts)
    {
        Dictionary<string, string?> settings = new()
        {
            [WebHostDefaults.ServerUrlsKey] = urls,
            [WebHostDefaults.HttpPortsKey] = httpPorts,
            [WebHostDefaults.HttpsPortsKey] = httpsPorts
        };
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
