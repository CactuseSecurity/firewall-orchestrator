using FWO.Basics;
using NUnit.Framework;
using System.Net;

namespace FWO.Test
{
    /// <summary>
    /// Tests the text form of client addresses passed from the UI to the middleware.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ClientAddressFormatTest
    {
        private const long kScopeId = 3;
        private static readonly IPAddress kLinkLocalAddress = IPAddress.Parse("fe80::1");

        [Test]
        public void ToText_ReturnsEmptyTextForAnUnknownAddress()
        {
            Assert.That(ClientAddressFormat.ToText(null), Is.Empty);
        }

        [TestCase("192.0.2.10", "192.0.2.10")]
        [TestCase("2001:db8::1", "2001:db8::1")]
        [TestCase("::ffff:192.0.2.10", "192.0.2.10")]
        public void ToText_WritesOneNotationPerClient(string address, string expectedText)
        {
            Assert.That(ClientAddressFormat.ToText(IPAddress.Parse(address)), Is.EqualTo(expectedText));
        }

        [Test]
        public void ToText_DropsTheScopeId()
        {
            IPAddress scopedAddress = new(kLinkLocalAddress.GetAddressBytes(), kScopeId);

            Assert.That(ClientAddressFormat.ToText(scopedAddress), Is.EqualTo("fe80::1"));
        }

        [Test]
        public void Describe_NamesAnUnknownAddress()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ClientAddressFormat.Describe(null), Is.EqualTo(ClientAddressFormat.kUnknown));
                Assert.That(ClientAddressFormat.Describe(IPAddress.Parse("192.0.2.10")), Is.EqualTo("192.0.2.10"));
            });
        }

        [TestCase("192.0.2.10", "192.0.2.10")]
        [TestCase("2001:db8::1", "2001:db8::1")]
        [TestCase("2001:DB8::1", "2001:db8::1")]
        [TestCase("::1", "::1")]
        [TestCase("::ffff:192.0.2.10", "192.0.2.10")]
        public void TryParse_AcceptsASingleCanonicalAddress(string text, string expectedAddress)
        {
            bool parsed = ClientAddressFormat.TryParse(text, out IPAddress? address);

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(address, Is.EqualTo(IPAddress.Parse(expectedAddress)));
            });
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" 192.0.2.10")]
        [TestCase("192.0.2.10 ")]
        [TestCase("192.0.2.10\n")]
        [TestCase("192.0.2.10, 192.0.2.11")]
        [TestCase("192.0.2.10,192.0.2.11")]
        [TestCase("192.0.2.10:443")]
        [TestCase("192.0.2.10/32")]
        [TestCase("[2001:db8::1]")]
        [TestCase("[2001:db8::1]:443")]
        [TestCase("fe80::1%3")]
        [TestCase("fe80::1%eth0")]
        [TestCase("1")]
        [TestCase("127.1")]
        [TestCase("010.0.0.1")]
        [TestCase("0x7f.0.0.1")]
        [TestCase("2001:db8:0:0:0:0:0:1")]
        [TestCase("example.org")]
        [TestCase("192.0.2.10<script>")]
        public void TryParse_RefusesEverythingElse(string? text)
        {
            bool parsed = ClientAddressFormat.TryParse(text, out IPAddress? address);

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.False);
                Assert.That(address, Is.Null);
            });
        }

        [Test]
        public void TryParse_RefusesTextBeyondTheLengthLimit()
        {
            string longText = new('1', ClientAddressFormat.kMaxLength + 1);

            Assert.That(ClientAddressFormat.TryParse(longText, out _), Is.False);
        }

        [TestCase("192.0.2.10")]
        [TestCase("2001:db8::1")]
        [TestCase("::ffff:192.0.2.10")]
        public void TryParse_AcceptsWhatToTextWrites(string address)
        {
            string text = ClientAddressFormat.ToText(IPAddress.Parse(address));

            Assert.That(ClientAddressFormat.TryParse(text, out _), Is.True);
        }
    }
}
