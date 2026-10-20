using FWO.Basics;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class JsonSecretRedactorTest
    {
        private const string kSecretMarker = "secret-marker";

        [Test]
        public void Redact_MasksNestedSecretAndKeepsOtherValues()
        {
            string jsonText = $"{{\"name\":\"domain-a\",\"import_credential\":{{\"id\":7,\"user\":\"api\",\"secret\":\"{kSecretMarker}\"}}}}";

            string redacted = JsonSecretRedactor.Redact(jsonText);

            Assert.Multiple(() =>
            {
                Assert.That(redacted, Does.Not.Contain(kSecretMarker));
                Assert.That(redacted, Does.Contain($"\"secret\":\"{JsonSecretRedactor.kRedactedValue}\""));
                Assert.That(redacted, Does.Contain("\"name\":\"domain-a\""));
                Assert.That(redacted, Does.Contain("\"id\":7"));
                Assert.That(redacted, Does.Contain("\"user\":\"api\""));
            });
        }

        [TestCase("secret")]
        [TestCase("Secret")]
        [TestCase("password")]
        [TestCase("PASSWORD")]
        [TestCase("private_key")]
        public void Redact_MasksSecretPropertyNamesIgnoringCase(string propertyName)
        {
            string jsonText = $"{{\"{propertyName}\":\"{kSecretMarker}\"}}";

            Assert.That(JsonSecretRedactor.Redact(jsonText), Does.Not.Contain(kSecretMarker));
        }

        [Test]
        public void Redact_MasksSecretsInsideArrays()
        {
            string jsonText = $"[{{\"secret\":\"{kSecretMarker}\"}},{{\"devices\":[{{\"password\":\"{kSecretMarker}\"}}]}}]";

            Assert.That(JsonSecretRedactor.Redact(jsonText), Does.Not.Contain(kSecretMarker));
        }

        [Test]
        public void Redact_MasksSecretGivenAsObjectValue()
        {
            string jsonText = $"{{\"secret\":{{\"value\":\"{kSecretMarker}\"}}}}";

            Assert.That(JsonSecretRedactor.Redact(jsonText), Is.EqualTo($"{{\"secret\":\"{JsonSecretRedactor.kRedactedValue}\"}}"));
        }

        [Test]
        public void Redact_KeepsNullSecret()
        {
            const string jsonText = "{\"secret\":null}";

            Assert.That(JsonSecretRedactor.Redact(jsonText), Is.EqualTo(jsonText));
        }

        [TestCase("{\"ticketId\": 5}")]
        [TestCase("Ran into exception: login failed")]
        [TestCase("")]
        [TestCase("null")]
        [TestCase("\"plain json string\"")]
        [TestCase("{\"secret\": ")]
        public void Redact_ReturnsTextWithoutSecretUnchanged(string text)
        {
            Assert.That(JsonSecretRedactor.Redact(text), Is.EqualTo(text));
        }

        [Test]
        public void Redact_StopsAtMaximumDepthWithoutFailing()
        {
            const int nestingDepth = 150;
            string jsonText = string.Concat(Enumerable.Repeat("[", nestingDepth)) + string.Concat(Enumerable.Repeat("]", nestingDepth));

            Assert.That(JsonSecretRedactor.Redact(jsonText), Is.EqualTo(jsonText));
        }
    }
}
