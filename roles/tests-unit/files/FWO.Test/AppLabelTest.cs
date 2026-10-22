using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Labelling;
using FWO.Services.Labelling;
using FWO.Services.Modelling;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// App labels: the configured App key, the App label of rules from the rule owner mapping and of app servers
    /// from their owner.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class AppLabelTest
    {
        private const long kImportControlId = 4711;
        private static readonly List<string?> kAppIdsWithGaps = ["APP-1", " APP-1 ", "", null, "APP-2"];
        private static readonly List<string> kAppOneAndTwo = ["APP-1", "APP-2"];
        private static readonly List<long> kAffectedRules = [2, 2];
        private static readonly List<string> kAppKeyOnly = ["App"];
        private static readonly List<(long, long)> kAppLabelOnServersOneAndTwo = [(1, 10), (2, 10)];
        private static readonly List<(long, long)> kFormerAppLabelOnServerTwo = [(2, 11)];
        private static readonly List<long> kLabelTen = [10];
        private static readonly List<long> kRuleTwo = [2];
        private static readonly List<(long?, bool)> kImportEvent = [(kImportControlId, false)];
        private static readonly List<(long?, bool)> kUiEvent = [(null, true)];

        [SetUp]
        public void RequestFullSync()
        {
            RuleAppLabelSync.RequestFullSync();
        }

        [Test]
        public async Task AppLabelIds_AreCreatedOnceWithTheFlagsOfTheAppKey()
        {
            AppLabelTestApiConnection api = new();
            AppLabelHandler handler = new(api, " App ");

            Dictionary<string, long> labelIds = await handler.GetAppLabelIds(kAppIdsWithGaps);
            await handler.GetAppLabelIds(kAppIdsWithGaps);

            Assert.Multiple(() =>
            {
                Assert.That(labelIds.Keys, Is.EquivalentTo(kAppOneAndTwo));
                Assert.That(api.UpsertedKeys, Is.EqualTo(kAppKeyOnly));
                Assert.That(api.UpsertedAppIds, Is.EqualTo(kAppOneAndTwo));
                Assert.That(LabellingQueries.upsertAppLabelKey, Does.Contain("update_columns: [key_source_internal, key_is_owner_scoped, allow_multiple_values]"));
                Assert.That(LabellingQueries.upsertAppLabelKey, Does.Contain("key_source_internal: false, key_is_owner_scoped: false, allow_multiple_values: true"));
                Assert.That(handler.IsAppKey("app"), Is.True);
                Assert.That(handler.IsAppKey("Stage"), Is.False);
            });
        }

        [Test]
        public async Task AppServers_GetTheAppLabelOfTheirOwner()
        {
            AppLabelTestApiConnection api = new();
            api.LabelIdsByAppId["APP-1"] = 10;
            api.Owners.Add(new OwnerAppServerAppLabels
            {
                AppIdExternal = "APP-1",
                AppServers =
                [
                    new() { Id = 1 },
                    new() { Id = 2, AppLabels = [new() { LabelId = 11 }] },
                    new() { Id = 3, AppLabels = [new() { LabelId = 10 }] }
                ]
            });
            AppLabelHandler handler = new(api, "App");
            int eventsRequested = 0;

            int changes = await handler.SyncAppServers(5, () => { eventsRequested++; return Task.FromResult(AppLabelTestApiConnection.kEventId); });

            Assert.Multiple(() =>
            {
                Assert.That(changes, Is.EqualTo(3));
                Assert.That(api.AddedServerLabels.Select(label => (label.ownerNetworkId, label.labelId)), Is.EquivalentTo(kAppLabelOnServersOneAndTwo));
                Assert.That(api.ClosedServerLabels.Select(label => (label.ownerNetworkId, label.labelId)), Is.EqualTo(kFormerAppLabelOnServerTwo));
                Assert.That(eventsRequested, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task AppServers_LoseTheAppLabelOfAnOwnerWithoutAppId()
        {
            AppLabelTestApiConnection api = new();
            api.Owners.Add(new OwnerAppServerAppLabels { AppIdExternal = " ", AppServers = [new() { Id = 1, AppLabels = [new() { LabelId = 10 }] }] });

            await new AppLabelHandler(api, "App").SyncAppServers(5, () => Task.FromResult(AppLabelTestApiConnection.kEventId));

            Assert.That(api.ClosedServerLabels.Select(label => label.labelId), Is.EqualTo(kLabelTen));
            Assert.That(api.AddedServerLabels, Is.Empty);
        }

        [Test]
        public async Task EmptyAppKey_SwitchesAppLabelsOff()
        {
            AppLabelTestApiConnection api = new();
            GlobalConfig globalConfig = new() { AppLabelKey = " " };

            int changes = await new AppLabelHandler(api, " ").SyncAppServers(5, () => Task.FromResult(1L));
            await new RuleAppLabelSync(api, globalConfig).SyncAll(kImportControlId);

            Assert.That(changes, Is.Zero);
            Assert.That(api.Calls, Is.Empty);
        }

        [Test]
        public async Task Rules_GetTheAppLabelsOfTheirMappedOwners()
        {
            AppLabelTestApiConnection api = new();
            api.LabelIdsByAppId["APP-1"] = 10;
            api.ActiveRuleOwnerApps.Add(Mapping(1, "APP-1"));
            api.ActiveRuleOwnerApps.Add(Mapping(1, "APP-1"));
            api.ActiveRuleOwnerApps.Add(Mapping(2, "APP-2"));
            api.ActiveRuleOwnerApps.Add(Mapping(3, null));
            api.ActiveRuleOwnerApps.Add(Mapping(4, "APP-1"));
            api.ActiveRuleAppLabels.Add(new() { RuleId = 2, LabelId = 99 });
            api.ActiveRuleAppLabels.Add(new() { RuleId = 4, LabelId = 10 });
            GlobalConfig globalConfig = new() { AppLabelKey = "App", OwnerSoruceMappingID = (int)OwnerMappingSourceStm.CustomField };

            await new RuleAppLabelSync(api, globalConfig).SyncAll(kImportControlId);

            long appTwo = api.LabelIdsByAppId["APP-2"];
            Assert.Multiple(() =>
            {
                Assert.That(AppLabelTestApiConnection.Value(api.LastVariables(LabellingQueries.getAllActiveRuleOwnerApps), "mappingSourceId"),
                    Is.EqualTo((short)OwnerMappingSourceStm.CustomField));
                Assert.That(api.AddedRuleLabels, Is.EquivalentTo(Assignments((1, 10), (2, appTwo))));
                Assert.That(api.ClosedRuleLabels, Is.EqualTo(Assignments((2, 99))));
                Assert.That(api.ChangeEvents, Is.EqualTo(kImportEvent));
            });
        }

        [Test]
        public async Task Rules_AreSyncedFullyOnceThenOnlyWhereMappingsChanged()
        {
            AppLabelTestApiConnection api = new();
            api.ActiveRuleOwnerApps.Add(Mapping(1, "APP-1"));
            api.ActiveRuleOwnerApps.Add(Mapping(2, "APP-2"));
            GlobalConfig globalConfig = new() { AppLabelKey = "App" };

            await new RuleAppLabelSync(api, globalConfig).Sync(kAffectedRules, kImportControlId);
            await new RuleAppLabelSync(api, globalConfig).Sync(kAffectedRules, kImportControlId + 1);

            Assert.Multiple(() =>
            {
                Assert.That(api.CallsOf(LabellingQueries.getAllActiveRuleOwnerApps), Is.EqualTo(1));
                Assert.That(api.CallsOf(LabellingQueries.getActiveRuleOwnerApps), Is.EqualTo(1));
                Assert.That((IEnumerable<long>)AppLabelTestApiConnection.Value(api.LastVariables(LabellingQueries.getActiveRuleOwnerApps), "ruleIds")!,
                    Is.EqualTo(kRuleTwo));
                Assert.That(api.AddedRuleLabels, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task FailedSync_IsLoggedAndRepeatedFully()
        {
            AppLabelTestApiConnection api = new() { FailReads = true };
            GlobalConfig globalConfig = new() { AppLabelKey = "App" };

            await new RuleAppLabelSync(api, globalConfig).SyncAll(kImportControlId);
            api.FailReads = false;
            await new RuleAppLabelSync(api, globalConfig).Sync(kAffectedRules, kImportControlId + 1);

            Assert.That(api.CallsOf(LabellingQueries.getAllActiveRuleOwnerApps), Is.EqualTo(2));
            Assert.That(api.CallsOf(LabellingQueries.getActiveRuleOwnerApps), Is.Zero);
        }

        [Test]
        public async Task AppServersSavedInTheUi_GetTheAppLabelOfTheirOwner()
        {
            AppLabelTestApiConnection api = new();
            api.Owners.Add(new OwnerAppServerAppLabels { AppIdExternal = "APP-1", AppServers = [new() { Id = 1 }, new() { Id = 2 }] });

            await AppServerHelper.AssignOwnerAppLabel(api, new SimulatedUserConfig { AppLabelKey = "App" }, 5);
            await AppServerHelper.AssignOwnerAppLabel(api, new SimulatedUserConfig { AppLabelKey = "App" }, null);

            Assert.Multiple(() =>
            {
                Assert.That(api.AddedServerLabels, Has.Count.EqualTo(2));
                Assert.That(api.ChangeEvents, Is.EqualTo(kUiEvent));
            });
        }

        private static List<(long, long, long)> Assignments(params (long ruleId, long labelId)[] assignments)
        {
            return [.. assignments.Select(assignment => (assignment.ruleId, assignment.labelId, AppLabelTestApiConnection.kEventId))];
        }

        private static RuleOwnerApp Mapping(long ruleId, string? appId)
        {
            return new() { RuleId = ruleId, Owner = new() { AppIdExternal = appId } };
        }
    }
}
