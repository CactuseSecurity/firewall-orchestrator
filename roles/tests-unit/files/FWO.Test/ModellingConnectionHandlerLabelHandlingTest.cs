using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Labelling;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Modelling;
using NUnit.Framework;
using System.Collections;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Labels as source and destination of connections: AND-groups, selectable labels, saving with history, display.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionHandlerLabelHandlingTest
    {
        private const long kEventId = 77;
        private const string kPayload = "<script>x</script>\" onmouseover=\"y' ";
        private static readonly FwoOwner Application = new() { Id = 1, Name = "App1", ExtAppId = "APP-1" };
        private static readonly LabelValue kProd = Label(1, "Stage", "Prod");
        private static readonly LabelValue kTest = Label(2, "Stage", "Test");
        private static readonly LabelValue kLinux = Label(3, "OS", "Linux");
        private static readonly List<LabelValue> kSelectableCandidates =
        [
            kProd, Label(4, "approle", "AR1"), Label(5, "AppZone", "AZ1"), Label(6, "App", "APP-1"), Label(7, "App", "APP-2")
        ];
        private static readonly List<long> kSelectableForModeller = [1, 6];
        private static readonly List<long> kSelectableForAdmin = [1, 6, 7];
        private static readonly List<int> kOwnershipOfApp1 = [1];
        private static readonly List<string> kAdminRoles = [Roles.Admin];
        private static readonly List<(long, int)> kClosedProdGroup = [(1, 0)];
        private static readonly List<(long, int)> kAddedGroups = [(2, 1), (3, 1)];
        private static readonly List<long> kProdAndLinux = [1, 3];
        private static readonly List<(int, string?)> kModellerEvent = [(5, "uid=modeller")];
        private static readonly List<(long, int)> kAddedLinux = [(3, 1)];
        private static readonly List<(long, int)> kSavedSource = [(3, 0), (2, 1), (3, 1)];
        private static readonly List<long> kAllStoredSource = [1, 3, 2];

        [Test]
        public void StoredLabels_AreGroupedByAndGroup()
        {
            ModellingConnectionHandler handler = CreateHandler(new LabelConnApiConnection(), StoredConnection());

            Assert.Multiple(() =>
            {
                Assert.That(handler.SrcLabelGroups, Has.Count.EqualTo(2));
                Assert.That(handler.SrcLabelGroups[0].Select(label => label.LabelId), Is.EqualTo(kProdAndLinux));
                Assert.That(ModellingConnectionHandler.DisplayLabelGroups(handler.SrcLabelGroups), Is.EqualTo("(Stage: Prod ∩ OS: Linux) ∪ (Stage: Test)"));
                Assert.That(handler.SrcFilledInWork(), Is.True);
                Assert.That(handler.DstFilledInWork(), Is.False);
            });
        }

        [Test]
        public void AddAndRemoveLabels_KeepTheGroupNumbers()
        {
            ModellingConnectionHandler handler = CreateHandler(new LabelConnApiConnection(), StoredConnection());

            handler.AddLabel(true, 1, kLinux);
            handler.AddLabel(true, 1, kLinux);
            handler.AddLabel(true, -1, kProd);
            handler.RemoveLabel(true, 0, handler.SrcLabelGroups[0][0]);
            handler.RemoveLabel(true, 0, handler.SrcLabelGroups[0][0]);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SrcLabelGroups, Has.Count.EqualTo(2));
                Assert.That(handler.SrcLabelGroups[0].Select(label => (label.LabelId, label.AndGroup)), Is.EqualTo(kAddedGroups));
                Assert.That(handler.SrcLabelGroups[1].Single().AndGroup, Is.EqualTo(2));
            });
        }

        [Test]
        public void SelectableLabels_LeaveOutGroupKeysAndForeignAppLabels()
        {
            LabelConnApiConnection api = new();
            api.Labels.AddRange(kSelectableCandidates);
            ModellingConnectionHandler modeller = CreateHandler(api, new ModellingConnection { Id = 5 }, new SimulatedUserConfig { AppLabelKey = "App" });
            modeller.AllApps = [Application, new FwoOwner { Id = 2, ExtAppId = "APP-2" }];
            SimulatedUserConfig adminConfig = new() { AppLabelKey = "App" };
            adminConfig.User.Roles = kAdminRoles;
            ModellingConnectionHandler admin = CreateHandler(api, new ModellingConnection { Id = 5 }, adminConfig);

            modeller.LoadSelectableLabels().GetAwaiter().GetResult();
            admin.LoadSelectableLabels().GetAwaiter().GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(modeller.SelectableLabels.Select(label => label.Id), Is.EqualTo(kSelectableForModeller));
                Assert.That(admin.SelectableLabels.Select(label => label.Id), Is.EqualTo(kSelectableForAdmin));
            });
        }

        [Test]
        public async Task SaveLabels_ClosesRemovedAndAddsNewAssignmentsWithOneEvent()
        {
            LabelConnApiConnection api = new();
            ModellingConnectionHandler handler = CreateHandler(api, StoredConnection());
            handler.RemoveLabel(true, 0, handler.SrcLabelGroups[0][0]);
            handler.AddLabel(true, 1, kLinux);

            await handler.SaveLabels(5);
            await handler.SaveLabels(5);

            Assert.Multiple(() =>
            {
                Assert.That(api.Events, Is.EqualTo(kModellerEvent));
                Assert.That(api.Closed.Select(label => (label.labelId, label.andGroup)), Is.EqualTo(kClosedProdGroup));
                Assert.That(api.Added.Select(label => (label.labelId, label.andGroup)), Is.EqualTo(kAddedLinux));
                Assert.That(api.Closed.Concat(api.Added).All(label => label.side == "source" && label.eventId == kEventId), Is.True);
                Assert.That(api.HistoryEntries, Is.EqualTo(2));
                Assert.That(handler.ActConn.SourceLabels.Select(label => (label.LabelId, label.AndGroup)), Is.EquivalentTo(kSavedSource));
            });
        }

        [Test]
        public async Task SaveLabels_WritesNothingWithoutChanges()
        {
            LabelConnApiConnection api = new();
            ModellingConnectionHandler handler = CreateHandler(api, StoredConnection());

            await handler.SaveLabels(5);

            Assert.That(api.Events, Is.Empty);
            Assert.That(api.Added.Concat(api.Closed), Is.Empty);
        }

        [Test]
        public async Task SaveLabels_KeepsNoOwnLabelsOnASideFromAnInterface()
        {
            LabelConnApiConnection api = new();
            ModellingConnection interf = new() { Id = 9, IsInterface = true, SourceLabels = [Assigned(kTest, 0)] };
            ModellingConnectionHandler handler = CreateHandler(api, StoredConnection());

            handler.InterfaceToConn(interf);
            await handler.SaveLabels(5);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SrcReadOnly, Is.True);
                Assert.That(ModellingConnectionHandler.DisplayLabelGroups(handler.SrcLabelGroups), Is.EqualTo("Stage: Test"));
                Assert.That(api.Closed.Select(label => label.labelId), Is.EquivalentTo(kAllStoredSource));
                Assert.That(api.Added, Is.Empty);
            });
        }

        [Test]
        public void LabelEditing_NeedsTheSwitchAndWriteAccess()
        {
            ModellingConnectionHandler off = CreateHandler(new LabelConnApiConnection(), StoredConnection(), new SimulatedUserConfig { LabelBasedModelling = false });
            ModellingConnectionHandler on = CreateHandler(new LabelConnApiConnection(), StoredConnection());

            Assert.That(off.LabelEditingEnabled, Is.False);
            Assert.That(on.LabelEditingEnabled, Is.True);
        }

        [Test]
        public void ConnectionOverview_ShowsEncodedLabels()
        {
            ModellingConnection connection = new() { Id = 5, SourceLabels = [Assigned(Label(8, kPayload, kPayload), 0)], DestinationLabels = [Assigned(kProd, 0)] };

            string source = ModellingHandlerBase.GetSrcNames(connection, new SimulatedUserConfig()).Single();
            string destination = ModellingHandlerBase.GetDstNames(connection, new SimulatedUserConfig()).Single();

            Assert.Multiple(() =>
            {
                Assert.That(source, Does.Contain("&lt;script&gt;"));
                Assert.That(source, Does.Not.Contain("<script>"));
                Assert.That(source, Does.Not.Contain("\" onmouseover"));
                Assert.That(source, Does.Not.Contain("y'"));
                Assert.That(destination, Does.Contain(Icons.Label).And.Contain("Stage: Prod"));
            });
        }

        private static ModellingConnection StoredConnection()
        {
            return new()
            {
                Id = 5,
                SourceLabels = [Assigned(kProd, 0), Assigned(kLinux, 0), Assigned(kTest, 1)]
            };
        }

        private static ModellingConnectionHandler CreateHandler(SimulatedApiConnection api, ModellingConnection connection, SimulatedUserConfig? userConfig = null)
        {
            userConfig ??= new SimulatedUserConfig { LabelBasedModelling = true };
            userConfig.User.Dn = "uid=modeller";
            userConfig.User.Ownerships = kOwnershipOfApp1;
            return new ModellingConnectionHandler(api, userConfig, Application, [connection], connection, false, false, DefaultInit.DoNothing, DefaultInit.DoNothing, true);
        }

        private static LabelValue Label(long id, string key, string value)
        {
            return new() { Id = id, Value = value, Key = new() { Name = key } };
        }

        private static ConnectionLabel Assigned(LabelValue label, int andGroup)
        {
            return new() { LabelId = label.Id, AndGroup = andGroup, Label = label };
        }

        private sealed class LabelConnApiConnection : SimulatedApiConnection
        {
            public List<LabelValue> Labels { get; } = [];
            public List<(int connectionId, string? actor)> Events { get; } = [];
            public List<(string side, long labelId, int andGroup, long eventId)> Added { get; } = [];
            public List<(string side, long labelId, int andGroup, long eventId)> Closed { get; } = [];
            public int HistoryEntries { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                object answer = new ReturnId { AffectedRows = 1 };
                if (query == LabellingQueries.getModellingLabels)
                {
                    answer = Labels;
                }
                else if (query == LabellingQueries.addConnectionLabelChangeEvent)
                {
                    Events.Add(((int)Value(variables, "connectionId")!, (string?)Value(variables, "actor")));
                    answer = new ReturnId { Id = kEventId };
                }
                else if (query == ModellingQueries.addHistoryEntry)
                {
                    HistoryEntries++;
                    answer = new ReturnIdWrapper { ReturnIds = [new ReturnId { NewId = 1 }] };
                }
                else
                {
                    Record(query, variables);
                }
                return Task.FromResult((QueryResponseType)answer);
            }

            private void Record(string query, object? variables)
            {
                string side = query == LabellingQueries.addConnectionSourceLabels || query == LabellingQueries.removeConnectionSourceLabels ? "source" : "destination";
                if (query == LabellingQueries.addConnectionSourceLabels || query == LabellingQueries.addConnectionDestinationLabels)
                {
                    foreach (object label in (IEnumerable)Value(variables, "labels")!)
                    {
                        Added.Add((side, (long)Value(label, "label_id")!, (int)Value(label, "and_group")!, (long)Value(label, "created_event_id")!));
                    }
                }
                else if (query == LabellingQueries.removeConnectionSourceLabels || query == LabellingQueries.removeConnectionDestinationLabels)
                {
                    long eventId = (long)Value(variables, "removedEventId")!;
                    foreach (object assignment in (IEnumerable)Value(variables, "assignments")!)
                    {
                        Closed.Add((side, (long)Value(Value(assignment, "label_id"), "_eq")!, (int)Value(Value(assignment, "and_group"), "_eq")!, eventId));
                    }
                }
                else
                {
                    throw new NotImplementedException($"Query not implemented in label connection test api: {query}");
                }
            }

            private static object? Value(object? variables, string propertyName)
            {
                return variables?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(variables);
            }
        }
    }
}
