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
            handler.SelectSource(null);

            Assert.That(handler.Validate(), Is.EqualTo(OwnerMappingSourceHandler.kNoSourceSelectedError));
        }

        [Test]
        public void Validate_ReportsMissingOwnerKey_ForCustomFieldMapping()
        {
            OwnerMappingSourceHandler handler = new();
            handler.SelectSource(OwnerMappingSourceStm.CustomField);

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
        public void Validate_KeepsPendingOwnerKeys_WhenCustomFieldMappingLosesItsLastKey()
        {
            OwnerMappingSourceHandler handler = new();
            handler.Init(new ConfigData
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\"]"
            });
            handler.OwnerKeysToDelete.Add("app-id");

            Assert.Multiple(() =>
            {
                Assert.That(handler.Validate(), Is.EqualTo(OwnerMappingSourceHandler.kNoOwnerKeyError));
                // a rejected save must not touch the editor state, so the stored keys stay visible and the deletion revertible
                Assert.That(handler.OwnerKeys, Is.EqualTo(new List<string> { "app-id" }));
                Assert.That(handler.OwnerKeysToDelete, Is.EqualTo(new List<string> { "app-id" }));
            });

            handler.OwnerKeysToAdd.Add("replacement");

            Assert.Multiple(() =>
            {
                Assert.That(handler.Validate(), Is.Null);
                // the deletion retained by the rejected attempt is applied once the settings become valid
                Assert.That(handler.OwnerKeys, Is.EqualTo(new List<string> { "replacement" }));
                Assert.That(handler.OwnerKeysToAdd, Is.Empty);
                Assert.That(handler.OwnerKeysToDelete, Is.Empty);
            });
        }

        [Test]
        public void SelectSource_KeepsTheQueuedOwnerKeyEdits()
        {
            OwnerMappingSourceHandler handler = new();
            handler.Init(new ConfigData
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\"]"
            });
            handler.OwnerKeysToDelete.Add("app-id");
            handler.OwnerKeysToAdd.Add("queued");
            handler.ActiveOwnerKey = "typed";

            handler.SelectSource(OwnerMappingSourceStm.NameField);
            handler.SelectSource(OwnerMappingSourceStm.CustomField);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SelectedSource, Is.EqualTo(OwnerMappingSourceStm.CustomField));
                // looking at another source and coming back must not throw away work the editor showed as queued
                Assert.That(handler.OwnerKeysToAdd, Is.EqualTo(new List<string> { "queued" }));
                Assert.That(handler.OwnerKeysToDelete, Is.EqualTo(new List<string> { "app-id" }));
                Assert.That(handler.ActiveOwnerKey, Is.Empty);
            });
        }

        [Test]
        public void Validate_KeepsOwnerKeysUnchanged_WhileTheCustomFieldSectionIsNotDisplayed()
        {
            OwnerMappingSourceHandler handler = new();
            handler.Init(new ConfigData
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\"]"
            });
            handler.OwnerKeysToDelete.Add("app-id");
            handler.SelectSource(OwnerMappingSourceStm.Disabled);

            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\"]"
            };
            Assert.That(handler.Validate(), Is.Null);
            handler.ApplyTo(configData);

            Assert.Multiple(() =>
            {
                // edits queued in the no longer displayed custom field section must not reach the configuration
                Assert.That(handler.OwnerKeys, Is.EqualTo(new List<string> { "app-id" }));
                Assert.That(configData.CustomFieldOwnerKey, Is.EqualTo("[\"app-id\"]"));
                // but they stay queued, so the deletion is still offered when the section is displayed again
                Assert.That(handler.OwnerKeysToDelete, Is.EqualTo(new List<string> { "app-id" }));
            });
        }

        [Test]
        public void Validate_RemovesEveryOccurrenceOfADeletedOwnerKey()
        {
            OwnerMappingSourceHandler handler = new();
            // a version before the editor rejected duplicates could store the same key twice
            handler.Init(new ConfigData
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\",\"app-id\",\"owner\"]"
            });
            handler.OwnerKeysToDelete.Add("app-id");

            Assert.That(handler.Validate(), Is.Null);
            // the editor marks every row carrying the key as deleted, so none of them may survive the save
            Assert.That(handler.OwnerKeys, Is.EqualTo(new List<string> { "owner" }));
        }

        [Test]
        public void ApplyTo_KeepsStoredMarker_WhenNameFieldSectionIsNotDisplayed()
        {
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\"]",
                ModModelledMarker = "FWOC"
            };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);

            // the marker is edited in the name field section, then that source is abandoned again
            handler.SelectSource(OwnerMappingSourceStm.NameField);
            handler.ModelledMarker = "XYZ";
            handler.SelectSource(OwnerMappingSourceStm.CustomField);

            Assert.That(handler.Validate(), Is.Null);

            Assert.Multiple(() =>
            {
                // saving must neither store the abandoned marker nor request a rebuild for a change it did not write
                Assert.That(handler.ApplyTo(configData), Is.False);
                Assert.That(configData.ModModelledMarker, Is.EqualTo("FWOC"));
            });
        }

        [Test]
        public void ApplyTo_KeepsStoredOwnerKeys_WhenCustomFieldSectionIsNotDisplayed()
        {
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.NameField,
                CustomFieldOwnerKey = "LegacyKey",
                ModModelledMarker = "FWOC"
            };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);
            handler.ModelledMarker = "MODELLED";

            Assert.That(handler.Validate(), Is.Null);
            handler.ApplyTo(configData);

            // saving the name field marker must not touch the setting of a section the user never opened
            Assert.That(configData.CustomFieldOwnerKey, Is.EqualTo("LegacyKey"));
        }

        [Test]
        public void ApplyTo_StillRequestsRebuild_WhenTheFirstSaveAttemptFailed()
        {
            // the settings pages hand their long lived configuration to every attempt, the first one changed it
            ConfigData configData = new();
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);
            handler.SelectSource(OwnerMappingSourceStm.CustomField);
            handler.OwnerKeysToAdd.Add("app-id");

            Assert.That(handler.Validate(), Is.Null);
            Assert.That(handler.ApplyTo(configData), Is.True);

            // the write threw, nothing was stored, the admin presses save again
            Assert.That(handler.Validate(), Is.Null);
            Assert.Multiple(() =>
            {
                Assert.That(handler.ApplyTo(configData), Is.True);
                Assert.That(configData.OwnerSoruceMappingID, Is.EqualTo((int)OwnerMappingSourceStm.CustomField));
            });
        }

        [Test]
        public void ApplyTo_RequestsNoRebuild_WhenTheSavedSettingsWereTakenOver()
        {
            ConfigData configData = new();
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);
            handler.SelectSource(OwnerMappingSourceStm.CustomField);
            handler.OwnerKeysToAdd.Add("app-id");

            Assert.That(handler.Validate(), Is.Null);
            Assert.That(handler.ApplyTo(configData), Is.True);
            handler.TakeOverStoredSettings(configData);
            handler.ConfirmRuleOwnerRebuild();

            // saving the unchanged settings again must not rebuild the rule owner mappings a second time
            Assert.That(handler.Validate(), Is.Null);
            Assert.That(handler.ApplyTo(configData), Is.False);
        }

        [Test]
        public void ApplyTo_StillRequestsRebuild_WhenTheRebuildOfTheSavedSettingsFailed()
        {
            ConfigData configData = new() { OwnerSoruceMappingID = (int)OwnerMappingSourceStm.IpBased };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);
            handler.SelectSource(OwnerMappingSourceStm.CustomField);
            handler.OwnerKeysToAdd.Add("app-id");

            // the settings are stored, but the rebuild they require reports no success, so it is not confirmed
            Assert.That(handler.Validate(), Is.Null);
            Assert.That(handler.ApplyTo(configData), Is.True);
            handler.TakeOverStoredSettings(configData);

            // the admin removes the cause and saves again: the rebuild must not be reported as done in between
            Assert.That(handler.Validate(), Is.Null);
            Assert.That(handler.ApplyTo(configData), Is.True);

            handler.ConfirmRuleOwnerRebuild();
            Assert.That(handler.Validate(), Is.Null);
            Assert.That(handler.ApplyTo(configData), Is.False);
        }

        [Test]
        public void DiscardEdits_ShowsTheStoredSettings_AfterAFailedSave()
        {
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\"]",
                ModModelledMarker = "FWOC"
            };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);
            handler.SelectSource(OwnerMappingSourceStm.NameField);
            handler.ModelledMarker = "XYZ";
            handler.OwnerKeysToDelete.Add("app-id");

            // the failed attempt left its values in the configuration, they must not be mistaken for stored ones
            handler.ApplyTo(configData);
            handler.DiscardEdits();

            Assert.Multiple(() =>
            {
                Assert.That(handler.SelectedSource, Is.EqualTo(OwnerMappingSourceStm.CustomField));
                Assert.That(handler.OwnerKeys, Is.EqualTo(new List<string> { "app-id" }));
                Assert.That(handler.OwnerKeysToDelete, Is.Empty);
                Assert.That(handler.ModelledMarker, Is.EqualTo("FWOC"));
            });

            Assert.That(handler.Validate(), Is.Null);
            handler.ApplyTo(configData);

            Assert.Multiple(() =>
            {
                // the whole configuration is written, so the marker of the failed attempt must not survive in it
                Assert.That(configData.ModModelledMarker, Is.EqualTo("FWOC"));
                Assert.That(configData.OwnerSoruceMappingID, Is.EqualTo((int)OwnerMappingSourceStm.CustomField));
                Assert.That(configData.CustomFieldOwnerKey, Is.EqualTo("[\"app-id\"]"));
            });
        }

        [Test]
        public void ApplyTo_RestoresStoredOwnerKeys_WhichAFailedSaveAttemptLeftInTheConfiguration()
        {
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\"]",
                ModModelledMarker = "FWOC"
            };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);
            handler.OwnerKeysToAdd.Add("owner");

            // the first attempt wrote the added key into the configuration, the database write then failed
            Assert.That(handler.Validate(), Is.Null);
            handler.ApplyTo(configData);
            handler.DiscardEdits();

            // the admin cancels, switches to the name field mapping and saves that instead
            handler.SelectSource(OwnerMappingSourceStm.NameField);
            handler.ModelledMarker = "MODELLED";
            Assert.That(handler.Validate(), Is.Null);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ApplyTo(configData), Is.True);
                Assert.That(configData.CustomFieldOwnerKey, Is.EqualTo("[\"app-id\"]"));
                Assert.That(configData.ModModelledMarker, Is.EqualTo("MODELLED"));
            });
        }

        [Test]
        public void AddOwnerKey_TrimsAndRejectsDuplicateKeys()
        {
            OwnerMappingSourceHandler handler = new();
            handler.Init(new ConfigData { CustomFieldOwnerKey = "[\"app-id\"]" });

            handler.ActiveOwnerKey = " owner ";

            Assert.That(handler.AddOwnerKey(), Is.Null);
            Assert.Multiple(() =>
            {
                Assert.That(handler.OwnerKeysToAdd, Is.EqualTo(new List<string> { "owner" }));
                Assert.That(handler.ActiveOwnerKey, Is.Empty);
            });

            handler.ActiveOwnerKey = "app-id";

            // a rejected key reports its own cause so the editor shows the matching message
            Assert.That(handler.AddOwnerKey(), Is.EqualTo(OwnerMappingSourceHandler.kDuplicateOwnerKeyError));
            Assert.Multiple(() =>
            {
                Assert.That(handler.OwnerKeysToAdd, Has.Count.EqualTo(1));
                Assert.That(handler.ActiveOwnerKey, Is.EqualTo("app-id"));
            });
        }

        [Test]
        public void AddOwnerKey_RevertsThePendingDeletionOfAConfiguredKey()
        {
            OwnerMappingSourceHandler handler = new();
            handler.Init(new ConfigData
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\"]"
            });
            handler.OwnerKeysToDelete.Add("app-id");
            handler.ActiveOwnerKey = " app-id ";

            // a key which is only marked for removal is not a duplicate, re-adding it takes the removal back
            Assert.That(handler.AddOwnerKey(), Is.Null);
            Assert.Multiple(() =>
            {
                Assert.That(handler.OwnerKeysToDelete, Is.Empty);
                // the key must not be listed a second time as a pending addition
                Assert.That(handler.OwnerKeysToAdd, Is.Empty);
                Assert.That(handler.ActiveOwnerKey, Is.Empty);
            });

            Assert.That(handler.Validate(), Is.Null);
            Assert.That(handler.OwnerKeys, Is.EqualTo(new List<string> { "app-id" }));
        }

        [Test]
        public void AddOwnerKey_RejectsBlankKey()
        {
            OwnerMappingSourceHandler handler = new();
            handler.Init(new ConfigData());
            handler.ActiveOwnerKey = "   ";

            // a blank key must not be reported as a duplicate
            Assert.That(handler.AddOwnerKey(), Is.EqualTo(OwnerMappingSourceHandler.kNoOwnerKeyError));
            Assert.That(handler.OwnerKeysToAdd, Is.Empty);
        }

        [Test]
        public void ApplyTo_KeepsUnreadableOwnerKeys_WhenSourceDoesNotUseThem()
        {
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.IpBased,
                CustomFieldOwnerKey = "[\"app-id\",]"
            };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);

            handler.ApplyTo(configData);

            // saving an unrelated setting must not destroy the stored value the admin never saw
            Assert.That(configData.CustomFieldOwnerKey, Is.EqualTo("[\"app-id\",]"));
        }

        [Test]
        public void ApplyTo_WritesNewOwnerKeys_AfterUnreadableValueWasReplaced()
        {
            ConfigData configData = new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField,
                CustomFieldOwnerKey = "[\"app-id\",]"
            };
            OwnerMappingSourceHandler handler = new();
            handler.Init(configData);
            handler.OwnerKeysToAdd.Add("owner");

            Assert.That(handler.Validate(), Is.Null);
            handler.ApplyTo(configData);

            Assert.That(configData.CustomFieldOwnerKey, Is.EqualTo("[\"owner\"]"));
        }

        [Test]
        public void ApplyTo_Throws_WhenNoSourceIsSelected()
        {
            OwnerMappingSourceHandler handler = new();
            handler.Init(new ConfigData());
            handler.SelectSource(null);

            Assert.Throws<InvalidOperationException>(() => handler.ApplyTo(new ConfigData()));
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
            handler.SelectSource(OwnerMappingSourceStm.Disabled);

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

            Assert.Multiple(() =>
            {
                // the marker is only evaluated and only written by the name field mapping
                Assert.That(handler.ApplyTo(configData), Is.False);
                Assert.That(configData.ModModelledMarker, Is.EqualTo("OLD"));
            });
        }
    }
}
