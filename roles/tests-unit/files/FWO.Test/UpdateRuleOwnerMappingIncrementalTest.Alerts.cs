using FWO.Basics;
using FWO.Services;
using FWO.Services.EventMediator.Events;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// What the rule owner mapping reports while something is already going wrong: the alert list has to stay
    /// readable, and tidying up after a completed rebuild must not turn it into a failure. Shares the simulated
    /// API of <see cref="UpdateRuleOwnerMappingIncrementalTest"/>.
    /// </summary>
    public partial class UpdateRuleOwnerMappingIncrementalTest
    {
        private const string kFallbackAlertMarker = "fell back to a full reinitialize";

        [Test]
        public async Task RunAsync_ShouldLeaveOneStandingAlert_WhenTheBacklogFallbackRepeats()
        {
            // the fallback repeats for as long as whatever stopped the incremental processing persists, and
            // RaiseAlert recognizes a repeat by the exact description - so a backlog size in the text would
            // leave one open alert per run behind instead of one refreshed alert for the condition
            RuleOwnerMappingFake apiConnection = new();
            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            AddPendingRuleImports(apiConnection, 1, 4);
            await service.RunAsync();

            // a different number of imports the second time round, which is the normal case: the backlog is
            // whatever accumulated since the last run
            AddPendingRuleImports(apiConnection, 5, 9);
            await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.RaisedAlerts.Count(description => description.Contains(kFallbackAlertMarker)), Is.EqualTo(2),
                    "both fallbacks have to be reported, so the alert carries the timestamp of the latest one");
                Assert.That(apiConnection.OpenAlerts.Count(description => description.Contains(kFallbackAlertMarker)), Is.EqualTo(1),
                    "the older alert is only acknowledged while the description matches, so the list must not grow per run");
            });
        }

        [Test]
        public async Task RunAsync_ShouldReportSuccess_WhenDrainingTheCoveredBacklogFails()
        {
            // by the time the older imports are marked done, the mappings are written and the rebuild's own
            // import control is complete. Reporting the run as failed would send the caller after a state that
            // is already correct, and leave a saved configuration change outstanding for no reason
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(7, ImportType.RULE);
            apiConnection.FailBacklogDrainAfterFullReinitialize = true;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            bool result = await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "the rebuild itself succeeded, tidying the backlog is not part of that");
                Assert.That(apiConnection.ActivePairs, Is.Not.Empty, "the rebuilt mappings are in place");
                Assert.That(apiConnection.StoredRuns, Is.Not.Empty, "the run judged the state and belongs in the history");
                Assert.That(apiConnection.CompletedImports, Does.Not.Contain(7L),
                    "the import the drain could not reach stays pending, so the next run picks it up");
            });
        }

        /// <summary>
        /// Queues a range of pending rule imports, enough of them to force the backlog fallback.
        /// </summary>
        /// <param name="apiConnection">Simulated API to queue them on.</param>
        /// <param name="firstControlId">First control id to queue.</param>
        /// <param name="lastControlId">Last control id to queue.</param>
        private static void AddPendingRuleImports(RuleOwnerMappingFake apiConnection, long firstControlId, long lastControlId)
        {
            for (long controlId = firstControlId; controlId <= lastControlId; controlId++)
            {
                apiConnection.AddPendingImport(controlId, ImportType.RULE);
            }
        }
    }
}
