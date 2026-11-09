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

        // 64 is the default depth limit of the JSON parser
        [TestCase(63)]
        [TestCase(64)]
        [TestCase(80)]
        [TestCase(JsonSecretRedactor.kMaxDepth)]
        public void Redact_MasksSecretNestedDeeperThanParserDefault(int nestingDepth)
        {
            string jsonText = WrapInArrays($"{{\"secret\":\"{kSecretMarker}\",\"id\":7}}", nestingDepth);

            string redacted = JsonSecretRedactor.Redact(jsonText);

            Assert.Multiple(() =>
            {
                Assert.That(redacted, Does.Not.Contain(kSecretMarker));
                Assert.That(redacted, Is.EqualTo(WrapInArrays($"{{\"secret\":\"{JsonSecretRedactor.kRedactedValue}\",\"id\":7}}", nestingDepth)));
            });
        }

        [Test]
        public void Redact_MasksSecretBeyondMaximumDepth()
        {
            string jsonText = WrapInArrays($"{{\"secret\":\"{kSecretMarker}\"}}", JsonSecretRedactor.kMaxDepth + 20);

            Assert.That(JsonSecretRedactor.Redact(jsonText), Does.Not.Contain(kSecretMarker));
        }

        [Test]
        public void Redact_MasksSubtreeBeyondMaximumDepthAsAWhole()
        {
            const int nestingDepth = 150;
            string jsonText = WrapInArrays("", nestingDepth);

            string expected = WrapInArrays($"\"{JsonSecretRedactor.kRedactedValue}\"", JsonSecretRedactor.kMaxDepth + 1);
            Assert.That(JsonSecretRedactor.Redact(jsonText), Is.EqualTo(expected));
        }

        [Test]
        public void Redact_MasksJsonWithDuplicatePropertiesAsAWhole()
        {
            string jsonText = $"{{\"name\":\"domain-a\",\"secret\":\"first\",\"secret\":\"{kSecretMarker}\"}}";

            Assert.That(JsonSecretRedactor.Redact(jsonText), Is.EqualTo(JsonSecretRedactor.kRedactedValue));
        }

        private static string WrapInArrays(string jsonText, int nestingDepth)
        {
            return string.Concat(Enumerable.Repeat("[", nestingDepth)) + jsonText + string.Concat(Enumerable.Repeat("]", nestingDepth));
        }
    }
}
