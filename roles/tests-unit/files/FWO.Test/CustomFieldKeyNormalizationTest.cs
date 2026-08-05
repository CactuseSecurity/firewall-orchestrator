using FWO.Basics;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class CustomFieldKeyNormalizationTest
    {
        [Test]
        public void NormalizeCustomFieldKeys_ReadsJsonArray()
        {
            Assert.That(CustomFieldResolver.NormalizeCustomFieldKeys(GlobalConst.kDefaultChangeIdKeys),
                Is.EqualTo(new List<string> { "field-2", "ChangeID" }));
        }

        [Test]
        public void NormalizeCustomFieldKeys_DropsBlankEntriesAndTrims()
        {
            Assert.That(CustomFieldResolver.NormalizeCustomFieldKeys("[\" field-2 \",\"\",\"  \",\"ChangeID\"]"),
                Is.EqualTo(new List<string> { "field-2", "ChangeID" }));
        }

        [Test]
        public void NormalizeCustomFieldKeys_ReadsJsonString()
        {
            Assert.That(CustomFieldResolver.NormalizeCustomFieldKeys("\"Datum-Regelpruefung\""),
                Is.EqualTo(new List<string> { "Datum-Regelpruefung" }));
        }

        [Test]
        public void NormalizeCustomFieldKeys_KeepsLegacyPlainTextKey()
        {
            Assert.That(CustomFieldResolver.NormalizeCustomFieldKeys(" Datum-Regelpruefung "),
                Is.EqualTo(new List<string> { "Datum-Regelpruefung" }));
        }

        [Test]
        public void NormalizeCustomFieldKeys_FallsBackToRawKey_ForMalformedArray()
        {
            Assert.That(CustomFieldResolver.NormalizeCustomFieldKeys("[\"field-2\",]"),
                Is.EqualTo(new List<string> { "[\"field-2\",]" }));
        }

        [Test]
        public void NormalizeCustomFieldKeys_ReturnsEmpty_ForBlankOrEmptyList()
        {
            Assert.That(CustomFieldResolver.NormalizeCustomFieldKeys(null), Is.Empty);
            Assert.That(CustomFieldResolver.NormalizeCustomFieldKeys(""), Is.Empty);
            Assert.That(CustomFieldResolver.NormalizeCustomFieldKeys("   "), Is.Empty);
            Assert.That(CustomFieldResolver.NormalizeCustomFieldKeys("[]"), Is.Empty);
            Assert.That(CustomFieldResolver.NormalizeCustomFieldKeys("[\"\",\"  \"]"), Is.Empty);
        }

        [Test]
        public void ExtractCustomFieldValue_NormalizedKeys_MatchRawSettingResult()
        {
            Rule rule = new() { CustomFields = "{'Datum-Regelpruefung':'CHG-7'}" };

            string? fromSetting = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, "Datum-Regelpruefung", out _);
            string? fromKeys = CustomFieldResolver.ExtractCustomFieldValue<string>(
                rule, CustomFieldResolver.NormalizeCustomFieldKeys("Datum-Regelpruefung"), out _);

            Assert.That(fromSetting, Is.EqualTo("CHG-7"));
            Assert.That(fromKeys, Is.EqualTo("CHG-7"));
        }

        [Test]
        public void ExtractCustomFieldValue_EmptyKeys_ReturnsDefaultWithoutError()
        {
            Rule rule = new() { CustomFields = "{'field-2':'CHG-7'}" };

            string? result = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, new List<string>(), out string? errorMessage);

            Assert.That(result, Is.Null);
            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public void ExtractCustomFieldValue_UnreadableCustomFields_StillReportsError()
        {
            Rule rule = new() { CustomFields = "not-json" };

            string? result = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, GlobalConst.kDefaultChangeIdKeys, out string? errorMessage);

            Assert.That(result, Is.Null);
            Assert.That(errorMessage, Does.Contain("Error while resolving custom fields"));
        }

        [Test]
        public void ExtractCustomFieldValue_MatchesConfiguredKeyRegardlessOfCasing()
        {
            Rule camelCaseRule = new() { CustomFields = "{'ChangeId':'CHG-7'}" };
            Rule lowerCaseRule = new() { CustomFields = "{'changeid':'CHG-8'}" };

            Assert.That(CustomFieldResolver.ExtractCustomFieldValue<string>(camelCaseRule, GlobalConst.kDefaultChangeIdKeys, out _),
                Is.EqualTo("CHG-7"));
            Assert.That(CustomFieldResolver.ExtractCustomFieldValue<string>(lowerCaseRule, GlobalConst.kDefaultChangeIdKeys, out _),
                Is.EqualTo("CHG-8"));
        }

        [Test]
        public void ExtractCustomFieldValue_KeysDifferingOnlyInCasing_KeepFirstWithoutThrowing()
        {
            Rule rule = new() { CustomFields = "{'ChangeID':'first','changeid':'second'}" };

            string? result = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, GlobalConst.kDefaultChangeIdKeys, out string? errorMessage);

            Assert.That(result, Is.EqualTo("first"));
            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public void ExtractCustomFieldValue_ConfiguredKeyOrderWinsOverPayloadOrder()
        {
            Rule rule = new() { CustomFields = "{'changeid':'fallback','FIELD-2':'preferred'}" };

            Assert.That(CustomFieldResolver.ExtractCustomFieldValue<string>(rule, GlobalConst.kDefaultChangeIdKeys, out _),
                Is.EqualTo("preferred"));
        }

        [Test]
        public void KeyCache_ReturnsSameInstanceForUnchangedSetting()
        {
            CustomFieldKeyCache cache = new();

            IReadOnlyList<string> first = cache.GetKeys(GlobalConst.kDefaultChangeIdKeys);
            IReadOnlyList<string> second = cache.GetKeys(GlobalConst.kDefaultChangeIdKeys);

            Assert.That(second, Is.SameAs(first));
            Assert.That(first, Is.EqualTo(new List<string> { "field-2", "ChangeID" }));
        }

        [Test]
        public void KeyCache_RenormalizesAfterSettingChanged()
        {
            CustomFieldKeyCache cache = new();

            IReadOnlyList<string> first = cache.GetKeys(GlobalConst.kDefaultChangeIdKeys);
            IReadOnlyList<string> second = cache.GetKeys("[\"ticket-id\"]");

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second, Is.EqualTo(new List<string> { "ticket-id" }));
        }

        [Test]
        public void KeyCache_HandlesNullSetting()
        {
            CustomFieldKeyCache cache = new();

            Assert.That(cache.GetKeys(null), Is.Empty);
        }
    }
}
