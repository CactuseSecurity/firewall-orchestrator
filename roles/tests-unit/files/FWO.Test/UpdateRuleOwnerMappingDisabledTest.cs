using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using FWO.Services.EventMediator.Events;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the rule owner mapping while the mapping source is disabled.
    /// </summary>
    [TestFixture]
    internal class UpdateRuleOwnerMappingDisabledTest
    {
        private const long kNewImportControlId = 4711;

        [Test]
        public async Task RunAsync_RemovesAllMappings_WhenFullReinitializeIsRequested()
        {
            DisabledMappingApiConnection apiConnection = new()
            {
                ActiveRuleOwners = [new RuleOwner { RuleId = 1, OwnerId = 10 }]
            };
            UpdateRuleOwnerMappingDisabled service = new(apiConnection, new GlobalConfig());

            bool result = await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CreatedImportControls, Is.EqualTo(1));
                Assert.That(apiConnection.RemovalControlIds, Is.EqualTo(new List<long> { kNewImportControlId }));
                Assert.That(apiConnection.CompletedControlIds, Is.EqualTo(new List<long> { kNewImportControlId }));
            });
        }

        [Test]
        public async Task RunAsync_CreatesNoImportControl_WhenNoMappingIsLeft()
        {
            DisabledMappingApiConnection apiConnection = new();
            UpdateRuleOwnerMappingDisabled service = new(apiConnection, new GlobalConfig());

            bool result = await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CreatedImportControls, Is.Zero);
                Assert.That(apiConnection.RemovalControlIds, Is.Empty);
            });
        }

        [Test]
        public async Task RunAsync_DoesNothing_WhenTriggeredByTheScheduledRun()
        {
            DisabledMappingApiConnection apiConnection = new()
            {
                ActiveRuleOwners = [new RuleOwner { RuleId = 1, OwnerId = 10 }]
            };
            UpdateRuleOwnerMappingDisabled service = new(apiConnection, new GlobalConfig());

            bool result = await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(apiConnection.QueriesSent, Is.Zero);
            });
        }

        [Test]
        public async Task Dispatcher_RoutesToTheDisabledHandler_WhenNoMappingSourceIsConfigured()
        {
            DisabledMappingApiConnection apiConnection = new()
            {
                ActiveRuleOwners = [new RuleOwner { RuleId = 1, OwnerId = 10 }]
            };
            // a fresh installation leaves the config item at its default
            UpdateRuleOwnerMapping dispatcher = new(apiConnection, new GlobalConfig());

            bool result = await dispatcher.Run(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.RemovalControlIds, Is.EqualTo(new List<long> { kNewImportControlId }));
            });
        }

        private sealed class DisabledMappingApiConnection : SimulatedApiConnection
        {
            public List<RuleOwner> ActiveRuleOwners { get; set; } = [];
            public int CreatedImportControls { get; private set; }
            public int QueriesSent { get; private set; }
            public List<long> RemovalControlIds { get; } = [];
            public List<long> CompletedControlIds { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                ++QueriesSent;

                if (query == OwnerQueries.getActiveRuleOwners)
                {
                    return Task.FromResult((QueryResponseType)(object)ActiveRuleOwners.ToList());
                }

                if (query == ImportQueries.addImportForRuleOwner)
                {
                    ++CreatedImportControls;
                    InsertImportControl insertImportControl = new()
                    {
                        Returning = [new ImportControl { ControlId = kNewImportControlId }]
                    };
                    return Task.FromResult((QueryResponseType)(object)insertImportControl);
                }

                if (query == OwnerQueries.setAllActiveRuleOwnersRemoved)
                {
                    RemovalControlIds.Add(ReadControlId(variables, "controlId"));
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == ImportQueries.updateImportControlForRuleOwnerFull)
                {
                    CompletedControlIds.Add(ReadControlId(variables, "controlId"));
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == ImportQueries.getPendingRuleOwnerImports)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ImportControl>());
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            private static long ReadControlId(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    _ => throw new InvalidOperationException($"Missing long property '{propertyName}'.")
                };
            }
        }
    }
}
