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
        private static readonly List<AppLabelOwner> kOwnersWithGaps =
            [new(1, "One", "APP-1"), new(1, "One", " APP-1 "), new(3, "Three", ""), new(4, "Four", null), new(2, "Two", "APP-2")];
        private static readonly List<int> kOwnersOneAndTwo = [1, 2];
        private static readonly List<string> kFormattedValues = ["Billing (APP-1)", "APP-2"];
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
        private const long kOtherWriterLabelId = 77;
        private const string kImportSource = "import";
        private static readonly List<(long, long)> kAppOneOnRuleOne = [(1, 10)];
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

            Dictionary<int, long> labelIds = await handler.GetAppLabelIds(kOwnersWithGaps);
            await handler.GetAppLabelIds(kOwnersWithGaps);

            Assert.Multiple(() =>
            {
                Assert.That(labelIds.Keys, Is.EquivalentTo(kOwnersOneAndTwo));
                Assert.That(api.UpsertedKeys, Is.EqualTo(kAppKeyOnly));
                Assert.That(api.UpsertedAppIds, Is.EqualTo(kAppOneAndTwo));
                Assert.That(api.UpsertedOwnerIds, Is.EqualTo(kOwnersOneAndTwo));
                Assert.That(LabellingQueries.upsertAppLabelValues, Does.Contain("update_columns: [value, owner_id]"));
                Assert.That(LabellingQueries.upsertAppLabelKey, Does.Contain("update_columns: [key_source_internal, key_is_owner_scoped, allow_multiple_values]"));
                Assert.That(LabellingQueries.upsertAppLabelKey, Does.Contain("key_source_internal: false, key_is_owner_scoped: false, allow_multiple_values: true"));
                Assert.That(handler.IsAppKey("app"), Is.True);
                Assert.That(handler.IsAppKey("Stage"), Is.False);
            });
        }

        /// <summary>
        /// The values have the configured format; an owner without name gets the bare app id, an invalid format falls back
        /// to the default.
        /// </summary>
        [Test]
        public async Task AppLabelValues_HaveTheConfiguredFormat()
        {
            AppLabelTestApiConnection api = new();
            AppLabelHandler handler = new(api, "App", "{name} ({id})");
            List<AppLabelOwner> owners = [new(1, " Billing ", "APP-1"), new(2, " ", "APP-2")];

            Dictionary<int, long> labelIds = await handler.GetAppLabelIds(owners);

            Assert.Multiple(() =>
            {
                Assert.That(api.UpsertedAppIds, Is.EqualTo(kFormattedValues));
                Assert.That(labelIds[1], Is.EqualTo(api.LabelIdsByAppId["Billing (APP-1)"]));
                Assert.That(new AppLabelHandler(api, "App", "{name}").ValueFormat.Format, Is.EqualTo(LabelValueFormat.kDefault));
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
        /// the sync reads all its own labels of rules, of any key.
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
        /// The sync reads, writes and closes only its own labels of rules: labels of other writers stay active, also one
        /// with the App label the sync adds, while its own label of a former App key is closed.
        /// </summary>
        [Test]
        public async Task RuleLabelsOfOtherWriters_AreKeptBySync()
        {
            AppLabelTestApiConnection api = new();
            api.LabelIdsByAppId["APP-1"] = 10;
            api.ActiveRuleOwnerApps.Add(Mapping(1, "APP-1"));
            api.ActiveRuleAppLabels.Add(new RuleLabelRow { RuleId = 1, LabelId = kFormerKeyLabelId });
            api.OtherWritersRuleLabels.Add((new RuleLabelRow { RuleId = 1, LabelId = 10 }, kImportSource));
            api.OtherWritersRuleLabels.Add((new RuleLabelRow { RuleId = 2, LabelId = kOtherWriterLabelId }, kImportSource));

            await new RuleAppLabelSync(api, new GlobalConfig { AppLabelKey = "App" }).SyncAll(kImportControlId);

            Assert.Multiple(() =>
            {
                Assert.That(api.ClosedRuleLabels.Select(closed => (closed.ruleId, closed.labelId)), Is.EqualTo(kFormerKeyLabelOnRuleOne));
                Assert.That(api.AddedRuleLabels.Select(added => (added.ruleId, added.labelId)), Is.EqualTo(kAppOneOnRuleOne));
                Assert.That(api.AddedRuleLabelSources, Is.All.EqualTo(RuleLabelSource.kAppSync));
                Assert.That(api.OtherWritersRuleLabels, Has.Count.EqualTo(2), "labels of other writers stay active");
                Assert.That(AppLabelTestApiConnection.Value(api.LastVariables(LabellingQueries.getAllActiveRuleAppLabels), "labelSource"),
                    Is.EqualTo(RuleLabelSource.kAppSync));
                Assert.That(AppLabelTestApiConnection.Value(api.LastVariables(LabellingQueries.removeRuleLabelAssignments), "labelSource"),
                    Is.EqualTo(RuleLabelSource.kAppSync));
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

            bool unchanged = await keyChange.Apply(new("App", null), new(" App ", " {id} "), NoLog);
            int callsWhileUnchanged = api.Calls.Count;
            bool changed = await keyChange.Apply(new("App", null), new("Application", null), NoLog);
            int addedByChange = api.AddedServerLabels.Count;
            bool switchedOff = await keyChange.Apply(new("Application", null), new("", null), NoLog);

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

        /// <summary>
        /// A changed value format keeps the App key, but gives the app servers of all active owners the App label in the
        /// new format, which closes the label in the former format.
        /// </summary>
        [Test]
        public async Task ChangedValueFormat_MovesTheAppLabelsOfTheAppServers()
        {
            AppLabelTestApiConnection api = new();
            api.LabelIdsByAppId["APP-1"] = 10;
            api.Owners.Add(new OwnerAppServerAppLabels
            {
                Name = "Billing",
                AppIdExternal = "APP-1",
                AppServers = [new() { Id = 1, AppLabels = [new() { LabelId = 10 }] }]
            });
            api.OwnersWithNetworks.Add(new FwoOwner { Id = 5, Name = "Billing", Active = true, OwnerNetworks = [new()] });

            bool changed = await new AppLabelKeyChange(api).Apply(new("App", "{id}"), new("App", "{name} ({id})"), NoLog);

            Assert.Multiple(() =>
            {
                Assert.That(changed, Is.True);
                Assert.That(api.ClosedKeys, Is.Empty, "the key stays");
                Assert.That(api.UpsertedAppIds, Is.EqualTo(new List<string> { "Billing (APP-1)" }));
                Assert.That(api.ClosedServerLabels.Select(label => (label.ownerNetworkId, label.labelId)), Is.EqualTo(new List<(long, long)> { (1, 10) }));
                Assert.That(api.AddedServerLabels.Select(label => label.labelId), Is.EqualTo(new List<long> { api.LabelIdsByAppId["Billing (APP-1)"] }));
            });
        }

        /// <summary>A changed value format makes the next sync compare all rules again, like a changed App key.</summary>
        [Test]
        public async Task ChangedValueFormat_SyncsAllRulesAgain()
        {
            AppLabelTestApiConnection api = new();
            api.LabelIdsByAppId["APP-1"] = 10;
            api.ActiveRuleOwnerApps.Add(Mapping(1, "APP-1"));
            api.ActiveRuleAppLabels.Add(new RuleLabelRow { RuleId = 1, LabelId = 10 });

            await new RuleAppLabelSync(api, new GlobalConfig { AppLabelKey = "App" }).Sync(kRuleTwo, kImportControlId);
            await new RuleAppLabelSync(api, new GlobalConfig { AppLabelKey = "App", AppLabelValueFormat = "{name} ({id})" }).Sync(kRuleTwo, kImportControlId + 1);

            long formattedLabel = api.LabelIdsByAppId["Owner APP-1 (APP-1)"];
            Assert.Multiple(() =>
            {
                Assert.That(api.CallsOf(LabellingQueries.getAllActiveRuleOwnerApps), Is.EqualTo(2), "full sync first and after the format changed");
                Assert.That(api.ClosedRuleLabels.Select(closed => (closed.ruleId, closed.labelId)), Is.EqualTo(new List<(long, long)> { (1, 10) }));
                Assert.That(api.AddedRuleLabels.Select(added => (added.ruleId, added.labelId)), Is.EqualTo(new List<(long, long)> { (1, formattedLabel) }));
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
            int ownerId = appId == null ? 0 : int.Parse(appId[(appId.LastIndexOf('-') + 1)..]);
            return new() { RuleId = ruleId, Owner = new() { Id = ownerId, Name = $"Owner {appId}", AppIdExternal = appId } };
        }
    }
}
