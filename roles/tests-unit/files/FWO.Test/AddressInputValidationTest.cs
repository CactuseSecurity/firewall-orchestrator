using FWO.Middleware.Server.Requests;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class AddressInputValidationTest
{
    [TestCase("127.1")]
    [TestCase("2130706433")]
    [TestCase("0x7f000001")]
    [TestCase("0x7f.0.0.1")]
    [TestCase("0300.0000.0002.0010")]
    [TestCase("192.0.2.256")]
    [TestCase("192..2.10")]
    [TestCase("192.0.2.10.1")]
    [TestCase("192.0.2.+10")]
    [TestCase("192.0.2.10 ")]
    [TestCase("2001:db8:::1")]
    [TestCase("fe80::1%3")]
    [TestCase("fe80::%3")]
    [TestCase("fe80::1%0")]
    [TestCase("fe80::1%eth0")]
    [TestCase("2001:db8::127.1")]
    [TestCase("2001:db8::0x7f.0.0.1")]
    [TestCase("2001:db8::0300.0000.0002.0010")]
    [TestCase("::ffff:192.000.002.010")]
    [TestCase("::192.000.002.010")]
    public void InvalidAddress_IsRejectedInNetworkAndEitherRangeBound(string input)
    {
        string hostMask = input.Contains(':') ? "/128" : "/32";
        foreach (string value in new[] { input, input + hostMask })
        {
            (string Network, string Start, string End, string Field)[] representations =
            [
                (value, "", "", "ipNetwork"),
                ("", value, "192.0.2.10", "ipStart"),
                ("", "192.0.2.10", value, "ipEnd")
            ];
            foreach (var representation in representations)
            {
                bool valid = FlowComplianceRequestValidator.TryValidateAndNormalizeIpInput(
                    representation.Network, representation.Start, representation.End, "address",
                    out _, out string? error);

                Assert.That(valid, Is.False, $"{representation.Field}: {value}");
                Assert.That(error, Does.Contain(representation.Field));
            }
        }
    }

    [TestCase("192.000.002.010", "192.0.2.10")]
    [TestCase("010.008.009.010", "10.8.9.10")]
    [TestCase("2001:db8::192.000.002.010", "2001:db8::c000:20a")]
    [TestCase("2001:0db8:0000:0000:0000:0000:0000:0010", "2001:db8::10")]
    public void ConventionalAddress_NormalizesNetworkAndRangeBounds(string input, string expected)
    {
        string hostMask = input.Contains(':') ? "/128" : "/32";
        foreach (string value in new[] { input, input + hostMask })
        {
            Assert.That(FlowComplianceRequestValidator.TryValidateAndNormalizeIpInput(
                value, "", "", "address", out var networkBounds, out string? networkError), Is.True, networkError);
            Assert.That(networkBounds, Is.EqualTo((expected, expected)));

            Assert.That(FlowComplianceRequestValidator.TryValidateAndNormalizeIpInput(
                "", value, value, "address", out var rangeBounds, out string? rangeError), Is.True, rangeError);
            Assert.That(rangeBounds, Is.EqualTo((expected, expected)));
        }
    }

    [Test]
    public void PaddedNetworkWithHostBits_SuggestsDecimalNetwork()
    {
        Assert.That(FlowComplianceRequestValidator.TryValidateAndNormalizeIpInput(
            "192.000.002.010/24", "", "", "address", out _, out string? error), Is.False);
        Assert.That(error, Does.Contain("192.0.2.0/24"));
    }
}
