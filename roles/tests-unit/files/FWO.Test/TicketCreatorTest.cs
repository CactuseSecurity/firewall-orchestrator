using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class TicketCreatorTest
    {
        private sealed class TicketCreatorTestApiConn : SimulatedApiConnection
        {
            private long nextId = 100;
            private WfTicket lastCreatedTicket = new();
            private readonly string stateMatrix = JsonSerializer.Serialize(new GlobalStateMatrix
            {
                GlobalMatrix = new()
                {
                    [WorkflowPhases.request] = CreateMatrix(0, 1, 49, true),
                    [WorkflowPhases.approval] = CreateMatrix(49, 60, 99, false),
                    [WorkflowPhases.planning] = CreateMatrix(99, 110, 149, false),
                    [WorkflowPhases.verification] = CreateMatrix(149, 160, 199, false),
                    [WorkflowPhases.implementation] = CreateMatrix(49, 210, 249, false),
                    [WorkflowPhases.review] = CreateMatrix(249, 260, 299, false),
                    [WorkflowPhases.recertification] = CreateMatrix(299, 310, 349, false)
                }
            });

            public List<string> Queries { get; } = [];
            public List<object?> Variables { get; } = [];
            public WfTicket TicketById { get; set; } = new();
            public List<WfExtState> ExtStates { get; set; } = [];
            public WfTicketWriter? LastTicketWriter { get; private set; }

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                Variables.Add(variables);
                if (query == RequestQueries.getStates)
                {
                    return Task.FromResult((T)(object)new List<WfState>
                    {
                        new() { Id = 0 },
                        new() { Id = 1 },
                        new() { Id = 7 },
                        new() { Id = 49 },
                        new() { Id = 249 }
                    });
                }
                if (query == ConfigQueries.getConfigItemByKey)
                {
                    return Task.FromResult((T)(object)new List<GlobalStateMatrixHelper> { new() { ConfData = stateMatrix } });
                }
                if (query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((T)(object)Array.Empty<ConfigItem>());
                }
                if (query == DeviceQueries.getDeviceDetails)
                {
                    return Task.FromResult((T)(object)new List<Device> { new() { Id = 5, Name = "FW1" } });
                }
                if (query == OwnerQueries.getOwners)
                {
                    return Task.FromResult((T)(object)new List<FwoOwner>());
                }
                if (query == RequestQueries.getExtStates)
                {
                    return Task.FromResult((T)(object)ExtStates);
                }
                if (query == RequestQueries.newTicket)
                {
                    LastTicketWriter = GetVariable<WfTicketWriter>(variables, "requestTasks");
                    lastCreatedTicket = BuildCreatedTicket(variables, ++nextId, LastTicketWriter);
                    return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = lastCreatedTicket.Id }] });
                }
                if (query == RequestQueries.getTicketById)
                {
                    WfTicket ticket = TicketById.Id > 0 ? TicketById : lastCreatedTicket;
                    return Task.FromResult((T)(object)ticket);
                }
                if (query == RequestQueries.updateTicketState
                    || query == RequestQueries.updateRequestTaskState
                    || query == RequestQueries.updateImplementationTask
                    || query == RequestQueries.updateImplementationTaskState
                    || query == RequestQueries.updateRequestTaskAdditionalInfo
                    || query == RequestQueries.updateApproval)
                {
                    long id = GetVariable<long>(variables, "id");
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = id, NewIdLong = id });
                }
                if (query == RequestQueries.addCommentToReqTask
                    || query == RequestQueries.addCommentToImplTask
                    || query == RequestQueries.addCommentToTicket)
                {
                    return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = ++nextId }] });
                }
                if (query == RequestQueries.newComment)
                {
                    return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = ++nextId }] });
                }

                throw new AssertionException($"Unexpected query: {query}");
            }

            private static StateMatrix CreateMatrix(int input, int started, int end, bool active)
            {
                return new StateMatrix
                {
                    Matrix = new()
                    {
                        [0] = [0, 1, 7, 49],
                        [1] = [1, 7, 49],
                        [7] = [7, 49],
                        [49] = [49],
                        [249] = [249]
                    },
                    DerivedStates = new()
                    {
                        [0] = 0,
                        [1] = 1,
                        [7] = 7,
                        [49] = 49,
                        [249] = 249
                    },
                    LowestInputState = input,
                    LowestStartedState = started,
                    LowestEndState = end,
                    Active = active
                };
            }

            private static WfTicket BuildCreatedTicket(object? variables, long ticketId, WfTicketWriter? writer)
            {
                WfTicket ticket = new()
                {
                    Id = ticketId,
                    Title = GetVariable<string>(variables, "title"),
                    StateId = GetVariable<int>(variables, "state"),
                    Reason = GetVariable<string>(variables, "reason") ?? "",
                    Priority = GetVariable<int>(variables, "priority"),
                    Locked = GetVariable<bool>(variables, "locked"),
                    Requester = new UiUser { DbId = GetVariable<int>(variables, "requesterId"), Name = "Requester" }
                };
                long taskId = ticketId * 10;
                foreach (WfReqTaskWriter taskWriter in writer?.Tasks ?? [])
                {
                    WfReqTask task = new()
                    {
                        Id = ++taskId,
                        TicketId = ticketId,
                        Title = taskWriter.Title,
                        TaskNumber = taskWriter.TaskNumber,
                        StateId = taskWriter.StateId,
                        TaskType = taskWriter.TaskType,
                        RequestAction = taskWriter.RequestAction,
                        Reason = taskWriter.Reason,
                        AdditionalInfo = taskWriter.AdditionalInfo,
                        ManagementId = taskWriter.ManagementId,
                        Locked = taskWriter.Locked
                    };
                    task.Elements.AddRange(taskWriter.Elements.WfElementList.Select(element => new WfReqElement
                    {
                        Field = element.Field,
                        RequestAction = element.RequestAction,
                        DeviceId = element.DeviceId,
                        RuleUid = element.RuleUid
                    }));
                    ticket.Tasks.Add(task);
                }
                return ticket;
            }

            private static TValue GetVariable<TValue>(object? variables, string propertyName)
            {
                if (variables is IReadOnlyDictionary<string, object?> readOnlyDict
                    && readOnlyDict.TryGetValue(propertyName, out object? readOnlyValue))
                {
                    return readOnlyValue != null ? (TValue)readOnlyValue : default!;
                }
                if (variables is IDictionary<string, object?> dict
                    && dict.TryGetValue(propertyName, out object? dictValue))
                {
                    return dictValue != null ? (TValue)dictValue : default!;
                }
                PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
                object? value = property?.GetValue(variables);
                return value != null ? (TValue)value : default!;
            }
        }

        [Test]
        public void Constructor_UsesProvidedDisplayMessageCallback()
        {
            List<(string Title, string Message, bool ErrorFlag)> messages = [];
            TicketCreator ticketCreator = new(new SimulatedApiConnection(), new SimulatedUserConfig(), new ClaimsPrincipal(),
                new MiddlewareClient("http://localhost/"), WorkflowPhases.request, null,
                (_, title, message, errorFlag) => messages.Add((title, message, errorFlag)));
            WfHandler wfHandler = GetPrivateField<WfHandler>(ticketCreator, "wfHandler");

            wfHandler.DisplayMessage(null, "action", "done", false);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("action"));
                Assert.That(messages[0].Message, Is.EqualTo("done"));
                Assert.That(messages[0].ErrorFlag, Is.False);
            });
        }

        [Test]
        public async Task CreateTicket_CopiesRequestTasksOwnersApprovalsAndComments()
        {
            TicketCreatorTestApiConn apiConn = new();
            TicketCreator ticketCreator = CreateTicketCreator(apiConn);
            FwoOwner owner = new() { Id = 7, Name = "App" };
            WfReqTask reqTask = new()
            {
                Title = "Allow web",
                TaskNumber = 2,
                TaskType = WfTaskType.access.ToString(),
                Reason = "needed",
                ManagementId = 5,
                Comments = [new WfCommentDataHelper(new WfComment { CommentText = "task comment" })]
            };

            WfTicket ticket = await ticketCreator.CreateTicket(owner, [reqTask], "Request title", 7, "ticket reason");

            WfReqTaskWriter writtenTask = apiConn.LastTicketWriter!.Tasks.Single();
            Assert.Multiple(() =>
            {
                Assert.That(ticket.Id, Is.GreaterThan(0));
                Assert.That(ticket.Locked, Is.True);
                Assert.That(writtenTask.Title, Is.EqualTo("Allow web"));
                Assert.That(writtenTask.Locked, Is.True);
                Assert.That(writtenTask.StateId, Is.EqualTo(7));
                Assert.That(writtenTask.Owners.WfOwnerList.Single().OwnerId, Is.EqualTo(owner.Id));
                Assert.That(writtenTask.Approvals.WfApprovalList.Single().StateId, Is.EqualTo(7));
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.newComment));
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.addCommentToReqTask));
            });
        }

        [Test]
        public async Task CreateRequestNewInterfaceTicket_WritesRequestingOwnerAdditionalInfo()
        {
            TicketCreatorTestApiConn apiConn = new();
            TicketCreator ticketCreator = CreateTicketCreator(apiConn);
            FwoOwner owner = new() { Id = 7, Name = "Target" };
            FwoOwner requestingOwner = new() { Id = 9, Name = "Requester app" };

            long ticketId = await ticketCreator.CreateRequestNewInterfaceTicket(owner, requestingOwner, "if-test", "reason");

            WfReqTaskWriter writtenTask = apiConn.LastTicketWriter!.Tasks.Single();
            Assert.Multiple(() =>
            {
                Assert.That(ticketId, Is.GreaterThan(0));
                Assert.That(writtenTask.TaskType, Is.EqualTo(WfTaskType.new_interface.ToString()));
                Assert.That(writtenTask.Locked, Is.True);
                Assert.That(writtenTask.Owners.WfOwnerList.Single().OwnerId, Is.EqualTo(owner.Id));
                Assert.That(writtenTask.GetAddInfoIntValueOrZero(AdditionalInfoKeys.ReqOwner), Is.EqualTo(requestingOwner.Id));
            });
        }

        [Test]
        public async Task SetInterfaceId_UpdatesNewInterfaceRequestTaskAdditionalInfo()
        {
            TicketCreatorTestApiConn apiConn = new()
            {
                TicketById = new WfTicket
                {
                    Id = 42,
                    Tasks =
                    [
                        new WfReqTask { Id = 12, TicketId = 42, TaskType = WfTaskType.new_interface.ToString() }
                    ]
                }
            };
            TicketCreator ticketCreator = CreateTicketCreator(apiConn);

            await ticketCreator.SetInterfaceId(42, 4711);

            object? variables = apiConn.Variables[apiConn.Queries.IndexOf(RequestQueries.updateRequestTaskAdditionalInfo)];
            Assert.Multiple(() =>
            {
                Assert.That(GetVariable<long>(variables, "id"), Is.EqualTo(12));
                Assert.That(GetVariable<string>(variables, "additionalInfo"), Does.Contain($"\"{AdditionalInfoKeys.ConnId}\":\"4711\""));
            });
        }

        [Test]
        public async Task PromoteTicket_ReturnsFalseForUnmappedExternalState()
        {
            TicketCreatorTestApiConn apiConn = new()
            {
                TicketById = new WfTicket { Id = 42, StateId = 1 }
            };
            TicketCreator ticketCreator = CreateTicketCreator(apiConn);

            bool promoted = await ticketCreator.PromoteTicket(42, ExtStates.ExtReqRejected.ToString());

            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.False);
                Assert.That(apiConn.Queries, Does.Not.Contain(RequestQueries.updateTicketState));
            });
        }

        [Test]
        public async Task PromoteTicket_MapsExternalStateAndPersistsTicketState()
        {
            TicketCreatorTestApiConn apiConn = new()
            {
                TicketById = new WfTicket
                {
                    Id = 42,
                    StateId = 1,
                    Tasks = [new WfReqTask { Id = 12, TicketId = 42, StateId = 1, TaskType = WfTaskType.access.ToString() }]
                },
                ExtStates = [new WfExtState { Name = ExtStates.ExtReqRejected.ToString(), StateId = 49 }]
            };
            TicketCreator ticketCreator = CreateTicketCreator(apiConn);

            bool promoted = await ticketCreator.PromoteTicket(42, ExtStates.ExtReqRejected.ToString());

            object? variables = apiConn.Variables[apiConn.Queries.IndexOf(RequestQueries.updateTicketState)];
            Assert.Multiple(() =>
            {
                Assert.That(promoted, Is.True);
                Assert.That(GetVariable<long>(variables, "id"), Is.EqualTo(42));
                Assert.That(GetVariable<int>(variables, "state"), Is.EqualTo(49));
            });
        }

        [Test]
        public async Task PromoteNewInterfaceImplTasks_PromotesSelectedRequestTasks()
        {
            WfImplTask firstImplTask = CreateImplementationTask(21, 11);
            WfImplTask ignoredImplTask = CreateImplementationTask(22, 12);
            WfImplTask secondImplTask = CreateImplementationTask(23, 13);
            TicketCreatorTestApiConn apiConn = new()
            {
                TicketById = new WfTicket
                {
                    Id = 42,
                    StateId = 210,
                    Tasks =
                    [
                        CreateNewInterfaceTask(11, firstImplTask),
                        CreateNewInterfaceTask(12, ignoredImplTask),
                        CreateNewInterfaceTask(13, secondImplTask)
                    ]
                },
                ExtStates = [new WfExtState { Name = ExtStates.Rejected.ToString(), StateId = 249 }]
            };
            TicketCreator ticketCreator = CreateTicketCreator(apiConn);

            int promotedCount = await ticketCreator.PromoteNewInterfaceImplTasks(42, ExtStates.Rejected, [11, 13], "reject relevant");

            List<long> updatedImplTaskIds = apiConn.Queries
                .Select((query, index) => new { query, index })
                .Where(entry => entry.query == RequestQueries.updateImplementationTaskState)
                .Where(entry => GetVariable<int>(apiConn.Variables[entry.index], "state") == 249)
                .Select(entry => GetVariable<long>(apiConn.Variables[entry.index], "id"))
                .ToList();
            List<long> updatedRequestTaskIds = apiConn.Queries
                .Select((query, index) => new { query, index })
                .Where(entry => entry.query == RequestQueries.updateRequestTaskState)
                .Select(entry => GetVariable<long>(apiConn.Variables[entry.index], "id"))
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(promotedCount, Is.EqualTo(2));
                Assert.That(updatedImplTaskIds, Is.EqualTo(new List<long> { 21, 23 }));
                Assert.That(updatedRequestTaskIds, Is.EqualTo(new List<long> { 11, 13 }));
                Assert.That(apiConn.Queries.Count(query => query == RequestQueries.addCommentToImplTask), Is.EqualTo(2));
            });
        }

        [Test]
        public async Task CreateUnusedRuleDeleteTicket_CreatesRuleDeleteTasksAndTicketComment()
        {
            TicketCreatorTestApiConn apiConn = new();
            TicketCreator ticketCreator = CreateTicketCreator(apiConn);

            await ticketCreator.CreateUnusedRuleDeleteTicket(5, ["rule-1", "rule-2"], "cleanup");

            List<WfReqTaskWriter> tasks = apiConn.LastTicketWriter!.Tasks;
            Assert.Multiple(() =>
            {
                Assert.That(tasks, Has.Count.EqualTo(2));
                Assert.That(tasks.Select(task => task.TaskType), Is.All.EqualTo(WfTaskType.rule_delete.ToString()));
                Assert.That(tasks.Select(task => task.Locked), Is.All.True);
                Assert.That(tasks.Select(task => task.Elements.WfElementList.Single().RuleUid), Is.EqualTo(new[] { "rule-1", "rule-2" }));
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.newComment));
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.addCommentToTicket));
            });
        }

        [Test]
        public async Task CreateDecertRuleDeleteTicket_UsesConfiguredTicketAndTaskValues()
        {
            TicketCreatorTestApiConn apiConn = new();
            TicketCreator ticketCreator = CreateTicketCreator(apiConn);
            DateTime deadline = new(2026, 5, 21);

            await ticketCreator.CreateDecertRuleDeleteTicket(5, ["rule-1"], "cleanup", deadline);

            object? variables = apiConn.Variables[apiConn.Queries.IndexOf(RequestQueries.newTicket)];
            WfReqTaskWriter writtenTask = apiConn.LastTicketWriter!.Tasks.Single();
            Assert.Multiple(() =>
            {
                Assert.That(GetVariable<string>(variables, "title"), Is.EqualTo("Delete ticket FW1"));
                Assert.That(GetVariable<string>(variables, "reason"), Is.EqualTo("Delete reason cleanup"));
                Assert.That(GetVariable<int>(variables, "state"), Is.EqualTo(7));
                Assert.That(GetVariable<int>(variables, "priority"), Is.EqualTo(5));
                Assert.That(GetVariable<DateTime?>(variables, "deadline"), Is.EqualTo(deadline));
                Assert.That(writtenTask.Title, Is.EqualTo("Delete rule rule-1"));
                Assert.That(writtenTask.Reason, Is.EqualTo("Delete task reason"));
            });
        }

        private static TValue GetPrivateField<TValue>(object instance, string fieldName)
        {
            FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null ? (TValue)field.GetValue(instance)! : throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        private static TicketCreator CreateTicketCreator(TicketCreatorTestApiConn apiConn)
        {
            UserConfig userConfig = new(new SimulatedGlobalConfig(), apiConn, new UiUser { DbId = 3, Name = "Requester", Language = "English" }, false)
            {
                ModReqTicketTitle = "Interface ticket",
                ModReqTaskTitle = "Interface task",
                RecDeleteRuleInitState = 7,
                RecDeleteRuleTicketTitle = "Delete ticket",
                RecDeleteRuleTicketReason = "Delete reason",
                RecDeleteRuleReqTaskTitle = "Delete rule",
                RecDeleteRuleReqTaskReason = "Delete task reason",
                RecDeleteRuleTicketPriority = 5
            };
            return new TicketCreator(apiConn, userConfig, new ClaimsPrincipal(), null!,
                WorkflowPhases.request, null, DefaultInit.DoNothing);
        }

        private static WfReqTask CreateNewInterfaceTask(long id, WfImplTask implTask)
        {
            return new WfReqTask
            {
                Id = id,
                TicketId = 42,
                StateId = 210,
                TaskType = WfTaskType.new_interface.ToString(),
                ImplementationTasks = { implTask }
            };
        }

        private static WfImplTask CreateImplementationTask(long id, long requestTaskId)
        {
            return new WfImplTask
            {
                Id = id,
                TicketId = 42,
                ReqTaskId = requestTaskId,
                StateId = 210,
                TaskType = WfTaskType.new_interface.ToString()
            };
        }

        private static TValue GetVariable<TValue>(object? variables, string propertyName)
        {
            if (variables is IReadOnlyDictionary<string, object?> readOnlyDict
                && readOnlyDict.TryGetValue(propertyName, out object? readOnlyValue))
            {
                return readOnlyValue != null ? (TValue)readOnlyValue : default!;
            }
            if (variables is IDictionary<string, object?> dict
                && dict.TryGetValue(propertyName, out object? dictValue))
            {
                return dictValue != null ? (TValue)dictValue : default!;
            }
            PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
            object? value = property?.GetValue(variables);
            return value != null ? (TValue)value : default!;
        }
    }
}
