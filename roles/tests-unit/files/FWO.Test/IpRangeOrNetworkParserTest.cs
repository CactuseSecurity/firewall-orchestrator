using FWO.Middleware.Server.Requests;
using NetTools;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the rules that turn an IP address block of a request into an address range: the accepted forms,
/// the masks allowed on range bounds, CIDR networks, and the rejection of IPv4 written as IPv6.
/// </summary>
[TestFixture]
[Parallelizable]
internal class IpRangeOrNetworkParserTest
{
    /// <summary>Verifies an inclusive range keeps both bounds.</summary>
    [Test]
    public void TryParse_WithRange_ReturnsInclusiveBounds()
    {
        bool isValid = Parse("10.1.0.5", "10.1.0.9", null, out IPAddressRange? range, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(range!.Begin.ToString(), Is.EqualTo("10.1.0.5"));
            Assert.That(range.End.ToString(), Is.EqualTo("10.1.0.9"));
        });
    }

    /// <summary>Verifies a single host is given as a range with equal bounds.</summary>
    [Test]
    public void TryParse_WithEqualBounds_ReturnsSingleHost()
    {
        bool isValid = Parse("10.1.0.5", "10.1.0.5", null, out IPAddressRange? range, out _);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(range!.Begin, Is.EqualTo(range.End));
        });
    }

    /// <summary>Verifies the host mask of each address family is accepted on a range bound and stripped.</summary>
    [TestCase("10.1.0.5/32", "10.1.0.9/32", "10.1.0.5", "10.1.0.9")]
    [TestCase("2001:db8::1/128", "2001:db8::9/128", "2001:db8::1", "2001:db8::9")]
    public void TryParse_WithHostMaskOnBounds_StripsMask(string start, string end, string expectedStart, string expectedEnd)
    {
        bool isValid = Parse(start, end, null, out IPAddressRange? range, out _);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(range!.Begin.ToString(), Is.EqualTo(expectedStart));
            Assert.That(range.End.ToString(), Is.EqualTo(expectedEnd));
        });
    }

    /// <summary>Verifies a broader mask on a range bound is rejected and points the caller at ipNetwork.</summary>
    [Test]
    public void TryParse_WithNetworkMaskOnBound_IsRejected()
    {
        bool isValid = Parse("10.1.0.0/24", "10.1.0.9", null, out IPAddressRange? range, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(range, Is.Null);
            Assert.That(error, Does.Contain("unsupported netmask '/24' in 'ipStart'"));
            Assert.That(error, Does.Contain("'ipNetwork'"));
        });
    }

    /// <summary>Verifies a range whose start lies above its end is rejected.</summary>
    [Test]
    public void TryParse_WithStartAboveEnd_IsRejected()
    {
        bool isValid = Parse("10.1.0.9", "10.1.0.5", null, out _, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(error, Does.Contain("'ipStart' <= 'ipEnd'"));
        });
    }

    /// <summary>Verifies the bounds of a range must share their address family.</summary>
    [Test]
    public void TryParse_WithMixedAddressFamilies_IsRejected()
    {
        bool isValid = Parse("10.1.0.5", "2001:db8::1", null, out _, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(error, Does.Contain("same address family"));
        });
    }

    /// <summary>Verifies an unparsable bound is reported by the field that carries it.</summary>
    [TestCase("not-an-ip", "10.1.0.5", "'ipStart'")]
    [TestCase("10.1.0.5", "10.1.0.300", "'ipEnd'")]
    [TestCase("not-an-ip/32", "10.1.0.5", "'ipStart'")]
    public void TryParse_WithInvalidBound_NamesTheField(string start, string end, string expectedField)
    {
        bool isValid = Parse(start, end, null, out _, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(error, Does.Contain($"invalid {expectedField} value"));
        });
    }

    /// <summary>Verifies a network covers every address from network to broadcast address.</summary>
    [TestCase("10.1.0.0/24", "10.1.0.0", "10.1.0.255")]
    [TestCase("10.1.0.5/32", "10.1.0.5", "10.1.0.5")]
    [TestCase("0.0.0.0/0", "0.0.0.0", "255.255.255.255")]
    [TestCase("2001:db8::/64", "2001:db8::", "2001:db8::ffff:ffff:ffff:ffff")]
    public void TryParse_WithNetwork_ReturnsWholeBlock(string network, string expectedStart, string expectedEnd)
    {
        bool isValid = Parse(null, null, network, out IPAddressRange? range, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(range!.Begin.ToString(), Is.EqualTo(expectedStart));
            Assert.That(range.End.ToString(), Is.EqualTo(expectedEnd));
        });
    }

    /// <summary>
    /// Verifies a network with host bits is rejected instead of silently widened, and the message names the
    /// network that was probably meant.
    /// </summary>
    [Test]
    public void TryParse_WithHostBitsInNetwork_IsRejectedWithSuggestion()
    {
        bool isValid = Parse(null, null, "10.1.0.5/24", out IPAddressRange? range, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(range, Is.Null);
            Assert.That(error, Does.Contain("must not set host bits"));
            Assert.That(error, Does.Contain("'10.1.0.0/24'"));
        });
    }

    /// <summary>Verifies malformed networks are rejected with a reason.</summary>
    [TestCase("10.1.0.0", "requires a valid CIDR network")]
    [TestCase("/24", "requires a valid CIDR network")]
    [TestCase("10.1.0.0/24/8", "requires a valid CIDR network")]
    [TestCase("10.1.0.0/abc", "invalid 'ipNetwork' value")]
    [TestCase("not-an-ip/24", "invalid 'ipNetwork' value")]
    [TestCase("10.1.0.0/33", "invalid CIDR prefix")]
    [TestCase("10.1.0.0/-1", "invalid CIDR prefix")]
    [TestCase("2001:db8::/129", "invalid CIDR prefix")]
    public void TryParse_WithMalformedNetwork_IsRejected(string network, string expectedReason)
    {
        bool isValid = Parse(null, null, network, out _, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(error, Does.Contain(expectedReason));
        });
    }

    /// <summary>
    /// Verifies IPv6 values that only re-encode an IPv4 address are rejected in every field, naming the
    /// notation used, because they would never match an IPv4 zone.
    /// </summary>
    [TestCase("::ffff:10.1.0.5", null, null, "IPv4-mapped", "'ipStart'")]
    [TestCase("::10.1.0.5", null, null, "IPv4-compatible", "'ipStart'")]
    [TestCase("::ffff:10.1.0.5/128", null, null, "IPv4-mapped", "'ipStart'")]
    [TestCase(null, "::ffff:10.1.0.9", null, "IPv4-mapped", "'ipEnd'")]
    [TestCase(null, null, "::ffff:10.1.0.0/120", "IPv4-mapped", "'ipNetwork'")]
    public void TryParse_WithIpv4EncodedAsIpv6_IsRejected(string? start, string? end, string? network, string notation, string field)
    {
        // A range needs both bounds, so the bound that is not under test gets a valid IPv6 value.
        string? rangeStart = network == null ? start ?? "2001:db8::1" : null;
        string? rangeEnd = network == null ? end ?? "2001:db8::9" : null;

        bool isValid = Parse(rangeStart, rangeEnd, network, out _, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(error, Does.Contain(notation));
            Assert.That(error, Does.Contain(field));
        });
    }

    /// <summary>
    /// Verifies the IPv6 loopback and unspecified addresses are not mistaken for IPv4-compatible addresses,
    /// although they share the leading zero bytes.
    /// </summary>
    [TestCase("::", "::1")]
    [TestCase("::1", "::1")]
    public void TryParse_WithIpv6LoopbackOrUnspecified_IsAccepted(string start, string end)
    {
        bool isValid = Parse(start, end, null, out _, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(error, Is.Null);
        });
    }

    /// <summary>Verifies every incomplete or contradicting combination of the three fields is rejected.</summary>
    [TestCase(null, null, null, "requires either 'ipStart' and 'ipEnd' or 'ipNetwork'")]
    [TestCase("10.1.0.5", null, null, "requires both 'ipStart' and 'ipEnd'")]
    [TestCase(null, "10.1.0.5", null, "requires both 'ipStart' and 'ipEnd'")]
    [TestCase("10.1.0.5", "10.1.0.9", "10.1.0.0/24", "must not combine 'ipNetwork'")]
    [TestCase("10.1.0.5", null, "10.1.0.0/24", "must not combine 'ipNetwork'")]
    [TestCase(null, "10.1.0.9", "10.1.0.0/24", "must not combine 'ipNetwork'")]
    public void TryParse_WithInvalidFieldCombination_IsRejected(string? start, string? end, string? network, string expectedReason)
    {
        bool isValid = Parse(start, end, network, out IPAddressRange? range, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(range, Is.Null);
            Assert.That(error, Does.Contain(expectedReason));
        });
    }

    /// <summary>Verifies empty and whitespace values count as not given, so they do not form a mixed block.</summary>
    [TestCase("", "")]
    [TestCase("  ", " ")]
    public void TryParse_WithBlankRangeFieldsAndNetwork_TreatsBlankAsNotGiven(string start, string end)
    {
        bool isValid = Parse(start, end, "10.1.0.0/24", out _, out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(error, Is.Null);
        });
    }

    /// <summary>
    /// Verifies every error starts with a lower case verb and carries no subject, because the validator
    /// prefixes the request path of the block.
    /// </summary>
    [TestCase(null, null, null)]
    [TestCase("10.1.0.9", "10.1.0.5", null)]
    [TestCase(null, null, "10.1.0.5/24")]
    public void TryParse_ErrorMessage_StartsWithLowerCaseVerb(string? start, string? end, string? network)
    {
        Parse(start, end, network, out _, out string? error);

        Assert.That(error, Does.Match("^(requires|must|has) "));
    }

    private static bool Parse(string? start, string? end, string? network, out IPAddressRange? range, out string? error)
    {
        IpRangeOrNetworkRequest ipBlock = new() { IpStart = start, IpEnd = end, IpNetwork = network };
        return IpRangeOrNetworkParser.TryParse(ipBlock, out range, out error);
    }
}
