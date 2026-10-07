using FWO.Middleware.Server.Requests;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class AddressInputValidationTest
{
    [TestCase("192.000.002.010", "192.0.2.10")]
    [TestCase("2001:0db8:0:0:0:0:0:10", "2001:db8::10")]
    [TestCase("::", "::")]
    [TestCase("::1", "::1")]
    public void IpHost_NormalizesConventionalAddresses(string input, string expected)
    {
        AddressInput inputModel = new() { IpHost = input };

        bool valid = AddressInputNormalizer.TryValidateAndNormalize(inputModel, "address", out NormalizedAddressBounds bounds, out string? error);

        Assert.That(valid, Is.True, error);
        Assert.That(bounds, Is.EqualTo(new NormalizedAddressBounds(expected, expected)));
    }

    [TestCase("0.0.0.0/0", "0.0.0.0", "255.255.255.255")]
    [TestCase("192.0.2.10/32", "192.0.2.10", "192.0.2.10")]
    [TestCase("::/0", "::", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")]
    [TestCase("2001:db8::10/128", "2001:db8::10", "2001:db8::10")]
    public void IpNetwork_NormalizesCanonicalCidr(string input, string expectedStart, string expectedEnd)
    {
        AddressInput inputModel = new() { IpNetwork = input };

        bool valid = AddressInputNormalizer.TryValidateAndNormalize(inputModel, "address", out NormalizedAddressBounds bounds, out string? error);

        Assert.That(valid, Is.True, error);
        Assert.That(bounds, Is.EqualTo(new NormalizedAddressBounds(expectedStart, expectedEnd)));
    }

    [TestCase("10.0.0.0/+24")]
    [TestCase("10.0.0.0/-1")]
    [TestCase("10.0.0.0/ 24")]
    [TestCase("10.0.0.0/24 ")]
    [TestCase("10.0.0.0/24/24")]
    public void IpNetwork_RejectsNoncanonicalOrNondigitPrefixes(string value)
    {
        bool valid = AddressInputNormalizer.TryValidateAndNormalize(
            new AddressInput { IpNetwork = value }, "address", out _, out string? error);

        Assert.That(valid, Is.False);
        Assert.That(error, Does.Contain("ipNetwork"));
    }

    [TestCase("10.0.0.1/32")]
    [TestCase("fe80::1%3")]
    [TestCase("::ffff:192.0.2.10")]
    [TestCase("::192.0.2.10")]
    public void IpHost_RejectsMasksScopesAndIpv4EncodedIpv6(string value)
    {
        bool valid = AddressInputNormalizer.TryValidateAndNormalize(
            new AddressInput { IpHost = value }, "address", out _, out string? error);

        Assert.That(valid, Is.False);
        Assert.That(error, Does.Contain("ipHost"));
    }

    [Test]
    public void IpRange_AcceptsEqualEndpointsAndNormalizesLeadingZeros()
    {
        AddressInput input = new() { IpRange = ["192.000.002.010", "192.000.002.010"] };

        bool valid = AddressInputNormalizer.TryValidateAndNormalize(input, "address", out NormalizedAddressBounds bounds, out string? error);

        Assert.That(valid, Is.True, error);
        Assert.That(bounds, Is.EqualTo(new NormalizedAddressBounds("192.0.2.10", "192.0.2.10")));
    }

    [TestCase("192.0.2.10", "192.0.2.20")]
    [TestCase("2001:db8::10", "2001:db8::20")]
    public void IpRange_AcceptsOrderedMultiAddressRanges(string start, string end)
    {
        AddressInput input = new() { IpRange = [start, end] };

        bool valid = AddressInputNormalizer.TryValidateAndNormalize(input, "address", out NormalizedAddressBounds bounds, out string? error);

        Assert.That(valid, Is.True, error);
        Assert.That(bounds, Is.EqualTo(new NormalizedAddressBounds(start, end)));
    }

    [TestCaseSource(nameof(InvalidInputs))]
    public void InvalidRepresentation_IsRejected(AddressInput input, string expectedError)
    {
        bool valid = AddressInputNormalizer.TryValidateAndNormalize(input, "address", out _, out string? error);

        Assert.That(valid, Is.False);
        Assert.That(error, Does.Contain(expectedError));
    }

    private static IEnumerable<TestCaseData> InvalidInputs()
    {
        yield return new(new AddressInput(), "exactly one");
        yield return new(new AddressInput { IpHost = "10.0.0.1", IpNetwork = "10.0.0.0/24" }, "exactly one");
        yield return new(new AddressInput { IpRange = [] }, "exactly one");
        yield return new(new AddressInput { IpRange = ["10.0.0.1"] }, "exactly two");
        yield return new(new AddressInput { IpRange = ["10.0.0.1", "10.0.0.2", "10.0.0.3"] }, "exactly two");
        yield return new(new AddressInput { IpRange = ["", "10.0.0.1"] }, "ipRange[0]");
        yield return new(new AddressInput { IpRange = ["10.0.0.1", "2001:db8::1"] }, "same address family");
        yield return new(new AddressInput { IpRange = ["10.0.0.2", "10.0.0.1"] }, "lower to its upper");
        yield return new(new AddressInput { IpRange = ["10.0.0.1/32", "10.0.0.2"] }, "ipRange[0]");
    }

    public sealed class AddressInput : IAddressInput
    {
        public string IpHost { get; set; } = string.Empty;

        public string IpNetwork { get; set; } = string.Empty;

        public List<string>? IpRange { get; set; }
    }
}
