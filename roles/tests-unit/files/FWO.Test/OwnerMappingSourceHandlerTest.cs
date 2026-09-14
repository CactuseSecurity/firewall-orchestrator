using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the shared editor state of the rule owner mapping source settings.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class OwnerMappingSourceHandlerTest
    {
        [Test]
        public void OwnerMappingSources_OfferDisabledButNotManual()
        {
            OwnerMappingSourceHandler handler = new();

            Assert.Multiple(() =>
            {
                Assert.That(handler.OwnerMappingSources, Does.Contain(OwnerMappingSourceStm.Disabled));
                Assert.That(handler.OwnerMappingSources, Does.Not.Contain(OwnerMappingSourceStm.Manual));
            });
        }

        [Test]
        public void Init_SelectsDisabled_WhenConfigHoldsNoMappingSource()
        {
            OwnerMappingSourceHandler handler = new();

            // a fresh installation has never written the config item
            ConfigData configData = new();
            handler.Init(configData);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SelectedSource, Is.EqualTo(OwnerMappingSourceStm.Disabled));
                Assert.That(handler.OwnerKeys, Is.Empty);
                Assert.That(handler.ModelledMarker, Is.EqualTo(configData.ModModelledMarker));
            });
        }

        [Test]
        public void Init_ReadsConfiguredSourceAndKeys()
        {
            OwnerMappingSourceHandler handler = new();
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\",\"owner\"]",
                ModModelledMarker = "MODELLED"
            };

            handler.Init(configData);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SelectedSource, Is.EqualTo(OwnerMappingSourceStm.CustomField));
                Assert.That(handler.OwnerKeys, Is.EqualTo(new List<string> { "app-id", "owner" }));
                Assert.That(handler.ModelledMarker, Is.EqualTo("MODELLED"));
            });
        }

        [Test]
        public void Init_ReadsLegacySingleOwnerKey()
        {
            OwnerMappingSourceHandler handler = new();

            handler.Init(new ConfigData { CustomFieldOwnerKey = " LegacyKey " });

            Assert.That(handler.OwnerKeys, Is.EqualTo(new List<string> { "LegacyKey" }));
        }

        [Test]
        public void Init_ReturnsEmptyKeys_ForMalformedOwnerKeyJson()
        {
            OwnerMappingSourceHandler handler = new();

            handler.Init(new ConfigData { CustomFieldOwnerKey = "[\"app-id\",]" });

            Assert.That(handler.OwnerKeys, Is.Empty);
        }

        [Test]
        public void Init_DropsPendingEdits()
        {
            OwnerMappingSourceHandler handler = new();
            handler.ActiveOwnerKey = "typed";
            handler.OwnerKeysToAdd.Add("queued");
            handler.OwnerKeysToDelete.Add("obsolete");

            handler.Init(new ConfigData());

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActiveOwnerKey, Is.Empty);
                Assert.That(handler.OwnerKeysToAdd, Is.Empty);
                Assert.That(handler.OwnerKeysToDelete, Is.Empty);
            });
        }

        [Test]
        public void Validate_Succeeds_ForDisabledWithoutAnyOwnerKey()
        {
            OwnerMappingSourceHandler handler = new();
            handler.Init(new ConfigData());

            // the other importer settings must remain savable without any owner mapping configuration
            Assert.That(handler.Validate(), Is.Null);
        }

        [Test]
        public void Validate_ReportsMissingSource()
        {
            OwnerMappingSourceHandler handler = new();
            handler.SelectedSource = null;

            Assert.That(handler.Validate(), Is.EqualTo(OwnerMappingSourceHandler.kNoSourceSelectedError));
        }

        [Test]
        public void Validate_ReportsMissingOwnerKey_ForCustomFieldMapping()
        {
            OwnerMappingSourceHandler handler = new();
            handler.SelectedSource = OwnerMappingSourceStm.CustomField;

            Assert.That(handler.Validate(), Is.EqualTo(OwnerMappingSourceHandler.kNoOwnerKeyError));
        }

        [Test]
        public void Validate_AppliesPendingOwnerKeys()
        {
            OwnerMappingSourceHandler handler = new();
            handler.Init(new ConfigData
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"obsolete\"]"
            });
            handler.OwnerKeysToAdd.Add("app-id");
            handler.OwnerKeysToDelete.Add("obsolete");

            Assert.Multiple(() =>
            {
                Assert.That(handler.Validate(), Is.Null);
                Assert.That(handler.OwnerKeys, Is.EqualTo(new List<string> { "app-id" }));
                Assert.That(handler.OwnerKeysToAdd, Is.Empty);
                Assert.That(handler.OwnerKeysToDelete, Is.Empty);
            });
        }

        [Test]
        public void AddOwnerKey_TrimsAndRejectsDuplicateKeys()
        {
            OwnerMappingSourceHandler handler = new();
            handler.Init(new ConfigData { CustomFieldOwnerKey = "[\"app-id\"]" });

            handler.ActiveOwnerKey = " owner ";
            handler.AddOwnerKey();

            Assert.Multiple(() =>
            {
                Assert.That(handler.OwnerKeysToAdd, Is.EqualTo(new List<string> { "owner" }));
                Assert.That(handler.ActiveOwnerKey, Is.Empty);
            });

            handler.ActiveOwnerKey = "app-id";
            handler.AddOwnerKey();

            Assert.That(handler.OwnerKeysToAdd, Has.Count.EqualTo(1));
        }

        [Test]
        public void ApplyTo_WritesSettingsAndRequestsRebuild_WhenSourceChangedToDisabled()
        {
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\"]"
            };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);
            handler.SelectedSource = OwnerMappingSourceStm.Disabled;

            bool rebuildNeeded = handler.ApplyTo(configData);

            Assert.Multiple(() =>
            {
                Assert.That(rebuildNeeded, Is.True);
                Assert.That(configData.OwnerSoruceMappingID, Is.EqualTo((int)OwnerMappingSourceStm.Disabled));
            });
        }

        [Test]
        public void ApplyTo_RequestsNoRebuild_WhenNothingChanged()
        {
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\"]",
                ModModelledMarker = "MODELLED"
            };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);

            Assert.That(handler.ApplyTo(configData), Is.False);
        }

        [Test]
        public void ApplyTo_RequestsRebuild_WhenCustomFieldKeysChanged()
        {
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\"]"
            };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);
            handler.OwnerKeysToAdd.Add("owner");
            handler.Validate();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ApplyTo(configData), Is.True);
                Assert.That(configData.CustomFieldOwnerKey, Is.EqualTo("[\"app-id\",\"owner\"]"));
            });
        }

        [Test]
        public void ApplyTo_RequestsRebuild_WhenNameFieldMarkerChanged()
        {
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.NameField,
                ModModelledMarker = "OLD"
            };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);
            handler.ModelledMarker = "NEW";

            Assert.Multiple(() =>
            {
                Assert.That(handler.ApplyTo(configData), Is.True);
                Assert.That(configData.ModModelledMarker, Is.EqualTo("NEW"));
            });
        }

        [Test]
        public void ApplyTo_RequestsNoRebuild_WhenOnlyUnusedSettingOfOtherSourceChanged()
        {
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.IpBased,
                ModModelledMarker = "OLD"
            };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);
            handler.ModelledMarker = "NEW";

            // the marker is only evaluated by the name field mapping
            Assert.That(handler.ApplyTo(configData), Is.False);
        }
    }
}
