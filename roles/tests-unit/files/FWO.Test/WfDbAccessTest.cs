using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class WfDbAccessTest
    {
        private sealed class WfDbAccessTestApiConn : SimulatedApiConnection
        {
            public List<long> RegisteredTicketIds { get; set; } = [];
            public bool FindRuleUidHasMatch { get; set; }

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == RequestQueries.getOwnerTicketIds)
                {
                    List<TicketId> ids = RegisteredTicketIds.ConvertAll(id => new TicketId { Id = id });
                    return Task.FromResult((T)(object)ids);
                }
                if (query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((T)(object)Array.Empty<ConfigItem>());
                }
                if (query == RuleQueries.getRuleByUid)
                {
                    List<Rule> rules = FindRuleUidHasMatch ? [new Rule()] : [];
                    return Task.FromResult((T)(object)rules);
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        [Test]
        public async Task FilterWrongOwnersOut_FiltersTickets_AndFlagsNotEditable()
        {
            WfDbAccessTestApiConn apiConn = new() { RegisteredTicketIds = [1] };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false);

            WfTicket ticket1 = new() { Id = 1, Requester = new UiUser { DbId = 200 } };
            WfTicket ticket2 = new() { Id = 2, Requester = new UiUser { DbId = 201 } };
            WfTicket ticket3 = new() { Id = 3, Requester = new UiUser { DbId = 202 } };
            ticket3.Tasks.Add(new WfReqTask
            {
                Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 7 } }]
            });
            List<WfTicket> tickets = [ticket1, ticket2, ticket3];
            List<int> ownerIds = [7];

            MethodInfo? filterMethod = typeof(WfDbAccess).GetMethod("FilterWrongOwnersOut", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(filterMethod, Is.Not.Null);
            Task<List<WfTicket>> filterTask = (Task<List<WfTicket>>)filterMethod!.Invoke(dbAccess, new object[] { tickets, ownerIds })!;
            List<WfTicket> filtered = await filterTask;

            Assert.That(filtered.Select(t => t.Id), Is.EquivalentTo(new long[] { 1, 3 }));
            Assert.That(ticket2.Editable, Is.False);
        }

        [Test]
        public async Task FilterWrongOwnersOut_ReturnsEmpty_WhenOwnerIdsEmpty()
        {
            WfDbAccessTestApiConn apiConn = new();
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false);

            List<WfTicket> tickets = [new WfTicket { Id = 1 }];

            MethodInfo? filterMethod = typeof(WfDbAccess).GetMethod("FilterWrongOwnersOut", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(filterMethod, Is.Not.Null);
            Task<List<WfTicket>> filterTask = (Task<List<WfTicket>>)filterMethod!.Invoke(dbAccess, new object[] { tickets, new List<int>() })!;
            List<WfTicket> filtered = await filterTask;

            Assert.That(filtered, Is.Empty);
        }

        [Test]
        public async Task FilterWrongOwnersOut_AllowsRequesterTicket()
        {
            WfDbAccessTestApiConn apiConn = new();
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false);

            WfTicket ticket = new() { Id = 1, Requester = new UiUser { DbId = 42 } };
            List<WfTicket> tickets = [ticket];

            MethodInfo? filterMethod = typeof(WfDbAccess).GetMethod("FilterWrongOwnersOut", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(filterMethod, Is.Not.Null);
            Task<List<WfTicket>> filterTask = (Task<List<WfTicket>>)filterMethod!.Invoke(dbAccess, new object[] { tickets, new List<int> { 7 } })!;
            List<WfTicket> filtered = await filterTask;

            Assert.That(filtered, Has.Count.EqualTo(1));
            Assert.That(filtered[0].Id, Is.EqualTo(1));
        }

        [Test]
        public async Task FindRuleUid_ReturnsTrue_WhenRuleFound()
        {
            WfDbAccessTestApiConn apiConn = new() { FindRuleUidHasMatch = true };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false);

            bool found = await dbAccess.FindRuleUid(1, "uid");

            Assert.That(found, Is.True);
        }

        [Test]
        public async Task FindRuleUid_ReturnsFalse_WhenRuleMissing()
        {
            WfDbAccessTestApiConn apiConn = new() { FindRuleUidHasMatch = false };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false);

            bool found = await dbAccess.FindRuleUid(1, "uid");

            Assert.That(found, Is.False);
        }

        [Test]
        public void BuildReqTaskUpdateVariables_DoesNotIncludeCreationOnlyFields()
        {
            WfReqTask reqTask = new()
            {
                Title = "Access request",
                TaskNumber = 3,
                StateId = 0,
                TaskType = WfTaskType.access.ToString(),
                RequestAction = RequestAction.create.ToString(),
                RuleAction = 1,
                Tracking = 1,
                Reason = "test",
                AdditionalInfo = "{}",
                FreeText = "text",
                ManagementId = 5
            };

            MethodInfo? buildMethod = typeof(WfDbAccess).GetMethod("BuildReqTaskUpdateVariables", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(buildMethod, Is.Not.Null);

            Dictionary<string, object?> variables = (Dictionary<string, object?>)buildMethod!.Invoke(null, [reqTask])!;

            Assert.That(variables.ContainsKey("taskType"), Is.False);
            Assert.That(variables.ContainsKey("taskNumber"), Is.False);
            Assert.That(variables["title"], Is.EqualTo("Access request"));
            Assert.That(variables["state"], Is.EqualTo(0));
            Assert.That(variables["managementId"], Is.EqualTo(5));
        }

        [Test]
        public void BuildReqTaskInsertVariables_IncludesCreationOnlyFields()
        {
            WfReqTask reqTask = new()
            {
                Title = "Access request",
                TaskNumber = 3,
                TaskType = WfTaskType.access.ToString(),
                Locked = true
            };

            MethodInfo? buildMethod = typeof(WfDbAccess).GetMethod("BuildReqTaskInsertVariables", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(buildMethod, Is.Not.Null);

            Dictionary<string, object?> variables = (Dictionary<string, object?>)buildMethod!.Invoke(null, [reqTask])!;

            Assert.That(variables["taskNumber"], Is.EqualTo(3));
            Assert.That(variables["taskType"], Is.EqualTo(WfTaskType.access.ToString()));
            Assert.That(variables["locked"], Is.True);
        }

        [Test]
        public void BuildReqElementVariables_ClearsManualPortFieldsForFlowServiceReference()
        {
            WfReqElement element = new()
            {
                Field = ElemFieldType.service.ToString(),
                FlowServiceObjectId = 5,
                Name = "https",
                Port = 0,
                PortEnd = null,
                ProtoId = 0
            };
            MethodInfo? buildMethod = typeof(WfDbAccess).GetMethod("BuildReqElementVariables", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(buildMethod, Is.Not.Null);

            Dictionary<string, object?> variables = (Dictionary<string, object?>)buildMethod!.Invoke(null, [element])!;

            Assert.Multiple(() =>
            {
                Assert.That(variables["flowSvcObjId"], Is.EqualTo(5));
                Assert.That(variables["port"], Is.Null);
                Assert.That(variables["portEnd"], Is.Null);
                Assert.That(variables["proto"], Is.Null);
            });
        }

        [Test]
        public void BuildReqElementVariables_KeepsResolvedPortFieldsForFlowServiceReference()
        {
            WfReqElement element = new()
            {
                Field = ElemFieldType.service.ToString(),
                FlowServiceObjectId = 5,
                Name = "https",
                Port = 443,
                PortEnd = null,
                ProtoId = 6
            };
            MethodInfo? buildMethod = typeof(WfDbAccess).GetMethod("BuildReqElementVariables", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(buildMethod, Is.Not.Null);

            Dictionary<string, object?> variables = (Dictionary<string, object?>)buildMethod!.Invoke(null, [element])!;

            Assert.Multiple(() =>
            {
                Assert.That(variables["flowSvcObjId"], Is.EqualTo(5));
                Assert.That(variables["port"], Is.EqualTo(443));
                Assert.That(variables["portEnd"], Is.Null);
                Assert.That(variables["proto"], Is.EqualTo(6));
            });
        }

        [Test]
        public void BuildImplTaskUpdateVariables_DoesNotIncludeTaskType()
        {
            WfImplTask implTask = new()
            {
                Title = "Implementation task",
                ReqTaskId = 11,
                TaskNumber = 2,
                StateId = 4,
                TaskType = WfTaskType.group_create.ToString(),
                DeviceId = 7,
                ImplAction = RequestAction.create.ToString(),
                RuleAction = 1,
                Tracking = 1,
                FreeText = "impl text"
            };

            MethodInfo? buildMethod = typeof(WfDbAccess).GetMethod("BuildImplTaskUpdateVariables", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(buildMethod, Is.Not.Null);

            Dictionary<string, object?> variables = (Dictionary<string, object?>)buildMethod!.Invoke(null, [implTask])!;

            Assert.That(variables.ContainsKey("taskType"), Is.False);
            Assert.That(variables["title"], Is.EqualTo("Implementation task"));
            Assert.That(variables["reqTaskId"], Is.EqualTo((long)11));
            Assert.That(variables["state"], Is.EqualTo(4));
            Assert.That(variables["device"], Is.EqualTo(7));
        }
    }
}
