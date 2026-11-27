using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Services;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// A change of an owner made in the UI is recorded as a completed owner import that the rule owner mapping has not
    /// processed yet, so that the middleware maps the rules of the owner again.
    /// </summary>
    [TestFixture]
    internal class OwnerChangeRecorderTest
    {
        private const long kControlId = 815;
        private const int kOwnerId = 42;

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public List<(string query, object? variables)> Calls { get; } = [];
            public bool ReturnNoControl { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null,
                QueryChunkingOptions? chunkingOptions = null)
            {
                Calls.Add((query, variables));
                if (query != ImportQueries.addImportForOwner)
                {
                    return Task.FromResult((QueryResponseType)(object)new ImportControl());
                }
                InsertImportControl inserted = new();
                if (!ReturnNoControl)
                {
                    inserted.Returning.Add(new ImportControl { ControlId = kControlId });
                }
                return Task.FromResult((QueryResponseType)(object)inserted);
            }
        }

        [Test]
        public async Task RecordChange_AddsAnOwnerImportWithAChangelogEntryLeftForTheMapping()
        {
            RecordingApiConnection api = new();

            long controlId = await new OwnerChangeRecorder(api).RecordChange(kOwnerId, "CMDB");

            object? import = api.Calls.Single(call => call.query == ImportQueries.addImportForOwner).variables;
            object? changelog = api.Calls.Single(call => call.query == OwnerQueries.updateChangelogOwner).variables;
            object? completion = api.Calls.Single(call => call.query == ImportQueries.updateImportControlForRuleOwnerFull).variables;
            Assert.Multiple(() =>
            {
                Assert.That(controlId, Is.EqualTo(kControlId));
                Assert.That(AppLabelTestApiConnection.Value(import, "importTypeId"), Is.EqualTo(ImportType.OWNER));
                Assert.That(AppLabelTestApiConnection.Value(changelog, "control_id"), Is.EqualTo(kControlId));
                Assert.That(AppLabelTestApiConnection.Value(changelog, "old_owner_id"), Is.EqualTo((long)kOwnerId));
                Assert.That(AppLabelTestApiConnection.Value(changelog, "new_owner_id"), Is.EqualTo((long)kOwnerId));
                Assert.That(AppLabelTestApiConnection.Value(changelog, "change_action"), Is.EqualTo(ChangelogActionType.CHANGE));
                Assert.That(AppLabelTestApiConnection.Value(changelog, "source_id"), Is.EqualTo("CMDB"));
                Assert.That(AppLabelTestApiConnection.Value(completion, "controlId"), Is.EqualTo(kControlId));
                Assert.That(AppLabelTestApiConnection.Value(completion, "successful"), Is.EqualTo(true));
                Assert.That(AppLabelTestApiConnection.Value(completion, "rule_owner_mapping_done"), Is.EqualTo(false));
            });
        }

        [Test]
        public void RecordChange_FailsWithoutImportControl()
        {
            RecordingApiConnection api = new() { ReturnNoControl = true };

            Assert.ThrowsAsync<InvalidOperationException>(() => new OwnerChangeRecorder(api).RecordChange(kOwnerId, null));
            Assert.That(api.Calls.Exists(call => call.query == OwnerQueries.updateChangelogOwner), Is.False);
        }
    }
}
