using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Labelling;
using FWO.Data.Report;
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
        private static readonly List<string> kOnlyRuleLabelRead = [LabellingQueries.getAllActiveRuleAppLabels];
        private const long kFormerKeyLabelId = 55;
        private static readonly List<(long, long)> kFormerKeyLabelOnRuleOne = [(1, kFormerKeyLabelId)];
        private static readonly List<string> kClosedFormerKeys = ["App", "Application"];
        private static readonly List<string> kNewKeyOnly = ["Application"];
        private const int kOwnImportManagement = 3;
        private const int kSubManagement = 4;
        private const int kSuperManagement = 5;
        private const int kManagementWithoutImport = 6;
        private const long kOwnImport = 900;
        private const long kSuperImport = 950;
        private static readonly List<(long?, bool)> kStampedEvents = [(kOwnImport, false), (kSuperImport, false), (kImportControlId, false)];

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

        /// <summary>
        /// Without App key no App label is written; the rules are only read for App labels of a former key to close.
        /// </summary>
        [Test]
        public async Task EmptyAppKey_SwitchesAppLabelsOff()
        {
            AppLabelTestApiConnection api = new();
            GlobalConfig globalConfig = new() { AppLabelKey = " " };

            int changes = await new AppLabelHandler(api, " ").SyncAppServers(5, () => Task.FromResult(1L));
            await new RuleAppLabelSync(api, globalConfig).SyncAll(kImportControlId);

            Assert.Multiple(() =>
            {
                Assert.That(changes, Is.Zero);
                Assert.That(api.Calls.Select(call => call.query), Is.EqualTo(kOnlyRuleLabelRead));
                Assert.That(api.AddedRuleLabels.Count + api.ClosedRuleLabels.Count + api.ChangeEvents.Count, Is.Zero);
            });
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
        public async Task RuleAppLabelChanges_AreStampedWithTheRelevantImportOfTheirManagement()
        {
            AppLabelTestApiConnection api = new();
            api.LabelIdsByAppId["APP-1"] = 10;
            api.ActiveRuleOwnerApps.Add(Mapping(1, "APP-1"));
            api.ActiveRuleOwnerApps.Add(Mapping(2, "APP-1"));
            api.ActiveRuleOwnerApps.Add(Mapping(3, "APP-1"));
            api.ActiveRuleAppLabels.Add(new() { RuleId = 4, LabelId = 10 });
            api.RuleManagements.Add(new() { RuleId = 1, ManagementId = kOwnImportManagement });
            api.RuleManagements.Add(new() { RuleId = 2, ManagementId = kSubManagement });
            api.RuleManagements.Add(new() { RuleId = 3, ManagementId = kManagementWithoutImport });
            api.RuleManagements.Add(new() { RuleId = 4, ManagementId = kOwnImportManagement });
            api.RelevantImports.Add(ManagementWithImport(kOwnImportManagement, kOwnImport, null));
            api.RelevantImports.Add(ManagementWithImport(kSubManagement, kOwnImport - 5, kSuperManagement));
            api.RelevantImports.Add(ManagementWithImport(kSuperManagement, kSuperImport, null));
            api.RelevantImports.Add(ManagementWithImport(kManagementWithoutImport, null, null));
            GlobalConfig globalConfig = new() { AppLabelKey = "App" };

            await new RuleAppLabelSync(api, globalConfig).SyncAll(kImportControlId);

            Assert.Multiple(() =>
            {
                Assert.That(api.ChangeEvents, Is.EquivalentTo(kStampedEvents));
                Assert.That(api.CallsOf(LabellingQueries.addLabelChangeEvent), Is.EqualTo(kStampedEvents.Count));
                Assert.That(api.AddedRuleLabels, Has.Count.EqualTo(3));
                Assert.That(api.ClosedRuleLabels, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task RuleAppLabelChanges_AreStampedWithTheMappingRunIfTheImportsCannotBeLoaded()
        {
            AppLabelTestApiConnection api = new();
            api.ActiveRuleOwnerApps.Add(Mapping(1, "APP-1"));
            api.RuleManagements.Add(new() { RuleId = 1, ManagementId = kOwnImportManagement });
            api.FailImportReads = true;
            GlobalConfig globalConfig = new() { AppLabelKey = "App" };

            await new RuleAppLabelSync(api, globalConfig).SyncAll(kImportControlId);

            Assert.That(api.ChangeEvents, Is.EqualTo(kImportEvent));
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

        /// <summary>
        /// A changed App key makes the next sync compare all rules again, which closes the App labels of the former key:
        /// only App labels are written to the labels of rules as a whole.
        /// </summary>
        [Test]
        public async Task ChangedAppKey_SyncsAllRulesAgainAndClosesTheLabelsOfTheFormerKey()
        {
            AppLabelTestApiConnection api = new();
            api.ActiveRuleOwnerApps.Add(Mapping(1, "APP-1"));
            api.ActiveRuleAppLabels.Add(new RuleLabelRow { RuleId = 1, LabelId = kFormerKeyLabelId });

            await new RuleAppLabelSync(api, new GlobalConfig { AppLabelKey = "App" }).Sync(kRuleTwo, kImportControlId);
            await new RuleAppLabelSync(api, new GlobalConfig { AppLabelKey = "App" }).Sync(kRuleTwo, kImportControlId + 1);
            await new RuleAppLabelSync(api, new GlobalConfig { AppLabelKey = "Application" }).Sync(kRuleTwo, kImportControlId + 2);

            Assert.Multiple(() =>
            {
                Assert.That(api.CallsOf(LabellingQueries.getAllActiveRuleOwnerApps), Is.EqualTo(2), "full sync first and after the key changed");
                Assert.That(api.CallsOf(LabellingQueries.getActiveRuleOwnerApps), Is.EqualTo(1));
                Assert.That(api.ClosedRuleLabels.Select(closed => (closed.ruleId, closed.labelId)), Does.Contain((1L, kFormerKeyLabelId)));
                Assert.That(LabellingQueries.getAllActiveRuleAppLabels, Does.Not.Contain("key_name"));
                Assert.That(LabellingQueries.getActiveRuleAppLabels, Does.Not.Contain("key_name"));
            });
        }

        /// <summary>
        /// Switching App labels off (empty key) closes the App labels of the rules instead of leaving them active.
        /// </summary>
        [Test]
        public async Task SwitchedOffAppLabels_CloseTheAppLabelsOfTheRules()
        {
            AppLabelTestApiConnection api = new();
            api.ActiveRuleOwnerApps.Add(Mapping(1, "APP-1"));
            api.ActiveRuleAppLabels.Add(new RuleLabelRow { RuleId = 1, LabelId = kFormerKeyLabelId });

            await new RuleAppLabelSync(api, new GlobalConfig { AppLabelKey = "" }).SyncAll(kImportControlId);

            Assert.Multiple(() =>
            {
                Assert.That(api.ClosedRuleLabels.Select(closed => (closed.ruleId, closed.labelId)), Is.EqualTo(kFormerKeyLabelOnRuleOne));
                Assert.That(api.AddedRuleLabels, Is.Empty);
                Assert.That(api.CallsOf(LabellingQueries.getAllActiveRuleOwnerApps), Is.Zero, "no App label is wanted without key");
                Assert.That(api.UpsertedKeys, Is.Empty);
            });
        }

        /// <summary>
        /// A changed App key closes the App labels of the app servers under the former key and gives the app servers of all
        /// active owners the App label of the new key; an unchanged key changes nothing, an empty one only closes.
        /// </summary>
        [Test]
        public async Task ChangedAppKey_MovesTheAppLabelsOfTheAppServers()
        {
            AppLabelTestApiConnection api = new();
            api.Owners.Add(new OwnerAppServerAppLabels { AppIdExternal = "APP-1", AppServers = [new() { Id = 1 }, new() { Id = 2 }] });
            api.OwnersWithNetworks.Add(new FwoOwner { Id = 5, Name = "Owner", Active = true, OwnerNetworks = [new()] });
            api.OwnersWithNetworks.Add(new FwoOwner { Id = 6, Name = "Inactive", Active = false, OwnerNetworks = [new()] });
            AppLabelKeyChange keyChange = new(api);

            bool unchanged = await keyChange.Apply("App", " App ", NoLog);
            int callsWhileUnchanged = api.Calls.Count;
            bool changed = await keyChange.Apply("App", "Application", NoLog);
            int addedByChange = api.AddedServerLabels.Count;
            bool switchedOff = await keyChange.Apply("Application", "", NoLog);

            Assert.Multiple(() =>
            {
                Assert.That(unchanged, Is.False);
                Assert.That(callsWhileUnchanged, Is.Zero);
                Assert.That(changed && switchedOff, Is.True);
                Assert.That(api.ClosedKeys, Is.EqualTo(kClosedFormerKeys));
                Assert.That(api.UpsertedKeys, Is.EqualTo(kNewKeyOnly));
                Assert.That(addedByChange, Is.EqualTo(2), "both app servers of the active owner");
                Assert.That(api.AddedServerLabels, Has.Count.EqualTo(addedByChange), "switching off adds nothing");
                Assert.That(api.ChangeEvents.TrueForAll(changeEvent => changeEvent.importControlId == null && changeEvent.originIsUi), Is.True);
            });
        }

        private static Task NoLog(int severity, string level, string description) => Task.CompletedTask;

        private static List<(long, long, long)> Assignments(params (long ruleId, long labelId)[] assignments)
        {
            return [.. assignments.Select(assignment => (assignment.ruleId, assignment.labelId, AppLabelTestApiConnection.kEventId))];
        }

        private static ManagementReport ManagementWithImport(int managementId, long? importId, int? superManagerId)
        {
            ManagementReport management = new() { Id = managementId, SuperManagerId = superManagerId };
            management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId = importId;
            return management;
        }

        private static RuleOwnerApp Mapping(long ruleId, string? appId)
        {
            return new() { RuleId = ruleId, Owner = new() { AppIdExternal = appId } };
        }
    }
}
