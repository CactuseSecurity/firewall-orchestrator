using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Workflow;
using GraphQL;
using GraphQL.Client.Serializer.Newtonsoft;
using Newtonsoft.Json;
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
            public List<WfTicket> Tickets { get; set; } = [];
            public WfTicket Ticket { get; set; } = new();
            public bool FindRuleUidHasMatch { get; set; }
            public long NewTicketId { get; set; } = 101;
            public long UpdatedTicketId { get; set; } = 101;
            public long UpdatedApprovalId { get; set; } = 301;
            public bool ThrowOnNewTicket { get; set; }
            public bool ReturnNullNewTicketIds { get; set; }
            public long NewCommentId { get; set; } = 601;
            public long NewImplTaskId { get; set; } = 401;
            public long UpdatedImplTaskId { get; set; } = 402;
            public long DeletedImplTaskId { get; set; } = 403;
            public bool ReturnNullNewImplTaskIds { get; set; }
            public long NewImplElementId { get; set; } = 501;
            public long UpdatedImplElementId { get; set; } = 502;
            public long DeletedImplElementId { get; set; } = 503;
            public bool ReturnNullNewImplElementIds { get; set; }
            public long NewReqTaskId { get; set; } = 201;
            public long UpdatedReqTaskId { get; set; } = 202;
            public long UpdatedReqTaskAdditionalInfoId { get; set; } = 203;
            public long DeletedReqTaskId { get; set; } = 204;
            public bool ReturnNullNewReqTaskIds { get; set; }
            public long NewReqElementId { get; set; } = 301;
            public long UpdatedReqElementId { get; set; } = 302;
            public long DeletedReqElementId { get; set; } = 303;
            public bool ReturnNullNewReqElementIds { get; set; }
            public int NewReqTaskCallCount { get; private set; }
            public int UpdateReqTaskCallCount { get; private set; }
            public int UpdateReqTaskStateCallCount { get; private set; }
            public int UpdateReqTaskAdditionalInfoCallCount { get; private set; }
            public int DeleteReqTaskCallCount { get; private set; }
            public int NewReqElementCallCount { get; private set; }
            public int UpdateReqElementCallCount { get; private set; }
            public int DeleteReqElementCallCount { get; private set; }
            public int NewImplTaskCallCount { get; private set; }
            public int UpdateImplTaskCallCount { get; private set; }
            public int UpdateImplTaskStateCallCount { get; private set; }
            public int DeleteImplTaskCallCount { get; private set; }
            public int NewImplElementCallCount { get; private set; }
            public int UpdateImplElementCallCount { get; private set; }
            public int DeleteImplElementCallCount { get; private set; }
            public int NewCommentCallCount { get; private set; }
            public int AssignImplCommentCallCount { get; private set; }
            public int UpdateTicketStateCallCount { get; private set; }
            public int AddHistoryEntryCallCount { get; private set; }
            public object? LastHistoryVariables { get; private set; }
            public long NewApprovalId { get; set; } = 301;
            public int GetTicketByIdCallCount { get; private set; }
            public bool ThrowOnGetTicketById { get; set; }
            public bool ThrowOnAddHistoryEntry { get; set; }

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == RequestQueries.getOwnerTicketIds)
                {
                    List<TicketId> ids = RegisteredTicketIds.ConvertAll(id => new TicketId { Id = id });
                    return Task.FromResult((T)(object)ids);
                }
                if (query == ModellingQueries.addHistoryEntry)
                {
                    AddHistoryEntryCallCount++;
                    LastHistoryVariables = variables;
                    if (ThrowOnAddHistoryEntry)
                    {
                        throw new InvalidOperationException("field 'insert_change_history' not found in type: 'mutation_root'");
                    }
                    return Task.FromResult((T)(object)new ReturnIdWrapper());
                }
                if (query == RequestQueries.getTicketById)
                {
                    GetTicketByIdCallCount++;
                    if (ThrowOnGetTicketById)
                    {
                        throw new InvalidOperationException("ticket could not be read");
                    }
                    return Task.FromResult((T)(object)Ticket);
                }
                if (query == RequestQueries.newTicket)
                {
                    if (ThrowOnNewTicket)
                    {
                        throw new InvalidOperationException("ticket creation failed");
                    }

                    if (ReturnNullNewTicketIds)
                    {
                        return Task.FromResult((T)(object)new ReturnIdWrapper
                        {
                            ReturnIds = null!
                        });
                    }

                    return Task.FromResult((T)(object)new ReturnIdWrapper
                    {
                        ReturnIds = new ReturnId[] { new ReturnId { NewIdLong = NewTicketId } }
                    });
                }
                if (query == RequestQueries.updateTicket)
                {
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = UpdatedTicketId });
                }
                if (query == RequestQueries.updateApproval)
                {
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = UpdatedApprovalId });
                }
                if (query == RequestQueries.newApproval)
                {
                    return Task.FromResult((T)(object)new ReturnIdWrapper
                    {
                        ReturnIds = new ReturnId[] { new ReturnId { NewIdLong = NewApprovalId } }
                    });
                }
                if (query == RequestQueries.updateTicketState)
                {
                    UpdateTicketStateCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = UpdatedTicketId });
                }
                if (query == RequestQueries.newImplementationTask)
                {
                    NewImplTaskCallCount++;
                    if (ReturnNullNewImplTaskIds)
                    {
                        return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = null! });
                    }

                    return Task.FromResult((T)(object)new ReturnIdWrapper
                    {
                        ReturnIds = new ReturnId[] { new ReturnId { NewIdLong = NewImplTaskId } }
                    });
                }
                if (query == RequestQueries.updateImplementationTask)
                {
                    UpdateImplTaskCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = UpdatedImplTaskId });
                }
                if (query == RequestQueries.updateImplementationTaskState)
                {
                    UpdateImplTaskStateCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = UpdatedImplTaskId });
                }
                if (query == RequestQueries.deleteImplementationTask)
                {
                    DeleteImplTaskCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { DeletedIdLong = DeletedImplTaskId });
                }
                if (query == RequestQueries.newImplementationElement)
                {
                    NewImplElementCallCount++;
                    if (ReturnNullNewImplElementIds)
                    {
                        return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = null! });
                    }

                    return Task.FromResult((T)(object)new ReturnIdWrapper
                    {
                        ReturnIds = new ReturnId[] { new ReturnId { NewIdLong = NewImplElementId } }
                    });
                }
                if (query == RequestQueries.updateImplementationElement)
                {
                    UpdateImplElementCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = UpdatedImplElementId });
                }
                if (query == RequestQueries.deleteImplementationElement)
                {
                    DeleteImplElementCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { DeletedIdLong = DeletedImplElementId });
                }
                if (query == RequestQueries.newComment)
                {
                    NewCommentCallCount++;
                    return Task.FromResult((T)(object)new ReturnIdWrapper
                    {
                        ReturnIds = new ReturnId[] { new ReturnId { NewIdLong = NewCommentId } }
                    });
                }
                if (query == RequestQueries.addCommentToImplTask)
                {
                    AssignImplCommentCallCount++;
                    return Task.FromResult((T)(object)new ReturnIdWrapper
                    {
                        ReturnIds = []
                    });
                }
                if (query == RequestQueries.newRequestTask)
                {
                    NewReqTaskCallCount++;
                    if (ReturnNullNewReqTaskIds)
                    {
                        return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = null! });
                    }

                    return Task.FromResult((T)(object)new ReturnIdWrapper
                    {
                        ReturnIds = new ReturnId[] { new ReturnId { NewIdLong = NewReqTaskId } }
                    });
                }
                if (query == RequestQueries.updateRequestTask)
                {
                    UpdateReqTaskCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = UpdatedReqTaskId });
                }
                if (query == RequestQueries.updateRequestTaskState)
                {
                    UpdateReqTaskStateCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = UpdatedReqTaskId });
                }
                if (query == RequestQueries.updateRequestTaskAdditionalInfo)
                {
                    UpdateReqTaskAdditionalInfoCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = UpdatedReqTaskAdditionalInfoId });
                }
                if (query == RequestQueries.deleteRequestTask)
                {
                    DeleteReqTaskCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { DeletedIdLong = DeletedReqTaskId });
                }
                if (query == RequestQueries.newRequestElement)
                {
                    NewReqElementCallCount++;
                    if (ReturnNullNewReqElementIds)
                    {
                        return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = null! });
                    }

                    return Task.FromResult((T)(object)new ReturnIdWrapper
                    {
                        ReturnIds = new ReturnId[] { new ReturnId { NewIdLong = NewReqElementId } }
                    });
                }
                if (query == RequestQueries.updateRequestElement)
                {
                    UpdateReqElementCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = UpdatedReqElementId });
                }
                if (query == RequestQueries.deleteRequestElement)
                {
                    DeleteReqElementCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { DeletedIdLong = DeletedReqElementId });
                }
                if (query == RequestQueries.getTickets || query == RequestQueries.getFullTickets || query == RequestQueries.getTicketsByParameters)
                {
                    return Task.FromResult((T)(object)Tickets);
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
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

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
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

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
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

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
        public async Task AddTicketToDb_DoesNotRejectWorkflowActionFailures()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                NewTicketId = 101,
                Ticket = new WfTicket
                {
                    Id = 101,
                    StateId = 1,
                    Requester = new UiUser { DbId = 42 }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>
            {
                new()
                {
                    Id = 1,
                    Name = "requested",
                    Actions = new List<WfStateActionDataHelper>
                    {
                        new()
                        {
                            SortOrder = 1,
                            Action = new WfStateAction
                            {
                                Name = "broken add approval",
                                ActionType = StateActionTypes.AddApproval.ToString(),
                                Scope = WfObjectScopes.Ticket.ToString(),
                                Event = StateActionEvents.OnSet.ToString(),
                                ExternalParams = "{invalid"
                            }
                        }
                    }
                }
            });
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfTicket ticket = new()
            {
                Id = 0,
                StateId = 1,
                Requester = new UiUser { DbId = 42 },
                Tasks = new List<WfReqTask>()
            };

            WfTicket result = await dbAccess.AddTicketToDb(ticket);

            Assert.That(result.Id, Is.EqualTo(101));
            Assert.That(result.StateId, Is.EqualTo(1));
        }

        [Test]
        public async Task AddTicketToDb_DoesNotWrapTicketCreationFailures()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                ThrowOnNewTicket = true
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfTicket ticket = new()
            {
                Id = 0,
                StateId = 1,
                Requester = new UiUser { DbId = 42 },
                Tasks = new List<WfReqTask>()
            };

            WfTicket result = await dbAccess.AddTicketToDb(ticket);

            Assert.That(result, Is.SameAs(ticket));
        }

        [Test]
        public async Task AddTicketToDb_ReturnsOriginalTicket_WhenInsertReturnsNoIds()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                ReturnNullNewTicketIds = true
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfTicket ticket = new()
            {
                Id = 0,
                StateId = 1,
                Requester = new UiUser { DbId = 42 },
                Tasks = new List<WfReqTask>()
            };

            WfTicket result = await dbAccess.AddTicketToDb(ticket);

            Assert.That(result, Is.SameAs(ticket));
        }

        [Test]
        public async Task AddTicketToDb_TriggersTaskActionsForCreatedTicket()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                NewTicketId = 101,
                Ticket = new WfTicket
                {
                    Id = 101,
                    StateId = 1,
                    Requester = new UiUser { DbId = 42 },
                    Tasks = new List<WfReqTask>
                    {
                        new WfReqTask
                        {
                            Id = 5,
                            TicketId = 101,
                            StateId = 2
                        }
                    }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>
            {
                new() { Id = 1, Name = "requested" },
                new() { Id = 2, Name = "approval" }
            });
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfTicket result = await dbAccess.AddTicketToDb(new WfTicket
            {
                Id = 0,
                StateId = 1,
                Requester = new UiUser { DbId = 42 },
                Tasks = new List<WfReqTask>()
            });

            Assert.That(result.Id, Is.EqualTo(101));
            Assert.That(result.Tasks, Has.Count.EqualTo(1));
            Assert.That(result.Tasks[0].StateId, Is.EqualTo(2));
        }

        [Test]
        public async Task AddTicketToDb_ProcessesAllCreatedRequestTasksWhenTaskActionsPromoteState()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                NewTicketId = 101,
                UpdatedTicketId = 101,
                Ticket = new WfTicket
                {
                    Id = 101,
                    StateId = 48,
                    Requester = new UiUser { DbId = 42 },
                    Tasks = new List<WfReqTask>
                    {
                        new WfReqTask
                        {
                            Id = 11,
                            TicketId = 101,
                            StateId = 48,
                            TaskType = WfTaskType.group_create.ToString()
                        },
                        new WfReqTask
                        {
                            Id = 12,
                            TicketId = 101,
                            StateId = 48,
                            TaskType = WfTaskType.group_create.ToString()
                        },
                        new WfReqTask
                        {
                            Id = 13,
                            TicketId = 101,
                            StateId = 48,
                            TaskType = WfTaskType.group_create.ToString()
                        }
                    }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            StateMatrix requestTaskMatrix = CreateStateMatrix(48, 60, 100);
            WfHandler wfHandler = new()
            {
                MasterStateMatrix = requestTaskMatrix,
                ActStateMatrix = requestTaskMatrix
            };
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);
            SetWorkflowContext(wfHandler, dbAccess);
            SetRequestTaskStateMatrix(wfHandler, requestTaskMatrix);
            await actionHandler.Init(new List<WfState>
            {
                CreatePromotingState(48),
                new() { Id = 60, Name = "in work" },
                new() { Id = 100, Name = "done" }
            });

            WfTicket result = await dbAccess.AddTicketToDb(CreatePromotingTicket());

            Assert.Multiple(() =>
            {
                Assert.That(result.Id, Is.EqualTo(101));
                Assert.That(result.Tasks, Has.Count.EqualTo(3));
                Assert.That(result.Tasks.All(task => task.StateId == 100), Is.True);
                Assert.That(apiConn.UpdateReqTaskStateCallCount, Is.EqualTo(3));
                Assert.That(apiConn.UpdateTicketStateCallCount, Is.EqualTo(3));
            });
        }

        [Test]
        public async Task UpdateTicketInDb_ReturnsTicket_WhenUpdateIdMatches()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>
            {
                new() { Id = 1, Name = "requested" }
            });
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfTicket ticket = new()
            {
                Id = 101,
                StateId = 1
            };
            ticket.MarkCreatedStateChanged(1);

            WfTicket result = await dbAccess.UpdateTicketInDb(ticket);

            Assert.That(result, Is.SameAs(ticket));
        }

        [Test]
        public async Task UpdateTicketInDb_ReportsMismatchWhenReturnedIdDiffers()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 999
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>
            {
                new() { Id = 1, Name = "requested" }
            });
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfTicket ticket = new()
            {
                Id = 101,
                StateId = 1
            };
            ticket.MarkCreatedStateChanged(1);

            WfTicket result = await dbAccess.UpdateTicketInDb(ticket);

            Assert.That(result, Is.SameAs(ticket));
        }

        [Test]
        public async Task UpdateTicketInDb_LogsPostRequestChangeByDifferentUser()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Title = "Old title",
                    StateId = 2,
                    Requester = new UiUser { DbId = 7 }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>());
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.approval);
            WfTicket ticket = new()
            {
                Id = 101,
                Title = "Corrected title",
                StateId = 2
            };

            await dbAccess.UpdateTicketInDb(ticket);

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(apiConn.LastHistoryVariables, Is.Not.Null);
            Assert.That(HistoryStringValue(apiConn, "changeSource"), Is.EqualTo(GlobalConst.kModuleWorkflow));
            Assert.That(HistoryCriticalFlag(apiConn), Is.True);
        }

        [Test]
        public async Task UpdateTicketInDb_LogsRequesterChangeAsNonCritical()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Title = "Old title",
                    StateId = 2,
                    Requester = new UiUser { DbId = 42 }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>());
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.approval);
            WfTicket ticket = new()
            {
                Id = 101,
                Title = "Corrected title",
                StateId = 2
            };

            await dbAccess.UpdateTicketInDb(ticket);

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryCriticalFlag(apiConn), Is.False);
        }

        [Test]
        public async Task UpdateTicketInDb_LogsOtherUserChangeDuringRequestPhase()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Title = "Original title",
                    Requester = new UiUser { DbId = 7 }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>());
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            await dbAccess.UpdateTicketInDb(new WfTicket { Id = 101, Title = "Corrected title" });

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task UpdateTicketInDb_DoesNotLogStateOnlyChange()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Title = "Same title",
                    StateId = 1,
                    Requester = new UiUser { DbId = 7 }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>());
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.approval);

            await dbAccess.UpdateTicketInDb(new WfTicket { Id = 101, Title = "Same title", StateId = 2 });

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.Zero);
        }

        [Test]
        public async Task UpdateTicketInDb_LogsMiddlewareChangeAsNonCritical()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Title = "Original title",
                    Requester = new UiUser { DbId = 7 }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>());
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.approval, false);

            await dbAccess.UpdateTicketInDb(new WfTicket { Id = 101, Title = "Corrected title" });

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryCriticalFlag(apiConn), Is.False);
        }

        [Test]
        public async Task UpdateReqTaskInDb_LogsContentChangeByOtherUser()
        {
            WfReqTask previousTask = new() { Id = 100, TicketId = 101, Title = "Original task" };
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedReqTaskId = 100,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Requester = new UiUser { DbId = 7 },
                    Tasks = new List<WfReqTask> { previousTask }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>());
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);
            WfReqTask changedTask = new() { Id = 100, TicketId = 101, Title = "Corrected task" };

            await dbAccess.UpdateReqTaskInDb(changedTask);

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task UpdateTicketStateInDb_LogsStandardChangeAsNonCritical()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                Ticket = new WfTicket
                {
                    Id = 101,
                    StateId = 1,
                    Requester = new UiUser { DbId = 7 }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>());
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.approval);
            WfTicket changedTicket = new() { Id = 101, StateId = 2 };

            await dbAccess.UpdateTicketStateInDb(changedTicket, false);

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryCriticalFlag(apiConn), Is.False);
        }

        [Test]
        public async Task UpdateApprovalInDb_LogsStandardChangeAsNonCritical()
        {
            WfApproval previousApproval = new() { Id = 301, TaskId = 100, StateId = 1 };
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedApprovalId = 301,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Requester = new UiUser { DbId = 7 },
                    Tasks = new List<WfReqTask>
                    {
                        new() { Id = 100, Approvals = new List<WfApproval> { previousApproval } }
                    }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>());
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.approval);
            WfApproval changedApproval = new() { Id = 301, TaskId = 100, StateId = 2 };

            await dbAccess.UpdateApprovalInDb(changedApproval, 101, apiConn.Ticket.Requester, false);

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryCriticalFlag(apiConn), Is.False);
        }

        [Test]
        public async Task UpdateImplTaskInDb_LogsContentChangeByOtherUser()
        {
            WfImplTask previousImplTask = new() { Id = 200, TicketId = 101, Title = "Original implementation" };
            WfReqTask previousReqTask = new()
            {
                Id = 100,
                TicketId = 101,
                ImplementationTasks = new List<WfImplTask> { previousImplTask }
            };
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedImplTaskId = 200,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Requester = new UiUser { DbId = 7 },
                    Tasks = new List<WfReqTask> { previousReqTask }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>());
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.implementation);
            WfImplTask changedTask = new() { Id = 200, TicketId = 101, Title = "Corrected implementation" };
            WfReqTask reqTask = new() { Id = 100, TicketId = 101 };

            await dbAccess.UpdateImplTaskInDb(changedTask, reqTask);

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddReqTaskToDb_LogsInsertWithNewTaskId()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                NewReqTaskId = 201,
                Ticket = new WfTicket { Id = 101, Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.request);
            WfReqTask reqTask = new() { Id = 0, TicketId = 101, Title = "New task" };

            await dbAccess.AddReqTaskToDb(reqTask);

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryStringValue(apiConn, "changeType"), Is.EqualTo(((int)ModellingTypes.ChangeType.Insert).ToString()));
            Assert.That(HistoryStringValue(apiConn, "objectType"), Is.EqualTo(((int)ChangeHistoryObjectType.RequestTask).ToString()));
            // the logged object id has to be the id assigned by the insert, not the transient zero
            Assert.That(HistoryStringValue(apiConn, "objectId"), Is.EqualTo("201"));
        }

        [Test]
        public async Task DeleteReqTaskFromDb_LogsDeleteWithPreviousContent()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                DeletedReqTaskId = 100,
                Ticket = new WfTicket { Id = 101, Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.request);

            await dbAccess.DeleteReqTaskFromDb(new WfReqTask { Id = 100, TicketId = 101, Title = "Removed task" });

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryStringValue(apiConn, "changeType"), Is.EqualTo(((int)ModellingTypes.ChangeType.Delete).ToString()));
            Assert.That(HistoryRawValue(apiConn, "newData"), Is.EqualTo("null"));
        }

        [Test]
        public async Task AddImplTaskToDb_LogsInsertWithNewTaskId()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                NewImplTaskId = 200,
                Ticket = new WfTicket { Id = 101, Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.implementation);

            await dbAccess.AddImplTaskToDb(new WfImplTask { Id = 0, TicketId = 101, Title = "New implementation" });

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryStringValue(apiConn, "objectType"), Is.EqualTo(((int)ChangeHistoryObjectType.ImplementationTask).ToString()));
            Assert.That(HistoryStringValue(apiConn, "objectId"), Is.EqualTo("200"));
        }

        [Test]
        public async Task DeleteImplTaskFromDb_LogsDeleteWithPreviousContent()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                DeletedImplTaskId = 200,
                Ticket = new WfTicket { Id = 101, Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.implementation);

            await dbAccess.DeleteImplTaskFromDb(new WfImplTask { Id = 200, TicketId = 101, Title = "Removed implementation" });

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryStringValue(apiConn, "changeType"), Is.EqualTo(((int)ModellingTypes.ChangeType.Delete).ToString()));
            Assert.That(HistoryRawValue(apiConn, "newData"), Is.EqualTo("null"));
        }

        [Test]
        public async Task AddApprovalToDb_LogsInsertForApproval()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                Ticket = new WfTicket { Id = 101, Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.approval);

            await dbAccess.AddApprovalToDb(new WfApproval { Id = 0, TaskId = 100, StateId = 1 }, 101, apiConn.Ticket.Requester);

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryStringValue(apiConn, "objectType"), Is.EqualTo(((int)ChangeHistoryObjectType.Approval).ToString()));
        }

        [Test]
        public async Task UpdateTicketInDb_DoesNotLogWhenPreviousTicketCannotBeRead()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                ThrowOnGetTicketById = true,
                Ticket = new WfTicket { Id = 101, Title = "Old title", Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.approval);

            await dbAccess.UpdateTicketInDb(new WfTicket { Id = 101, Title = "Corrected title" });

            // a failed read must not be recorded as an empty previous state
            Assert.That(apiConn.AddHistoryEntryCallCount, Is.Zero);
        }

        [Test]
        public async Task UpdateTicketStateInDb_DoesNotLogWhenPreviousTicketCannotBeRead()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                ThrowOnGetTicketById = true,
                Ticket = new WfTicket { Id = 101, StateId = 1, Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.approval);

            await dbAccess.UpdateTicketStateInDb(new WfTicket { Id = 101, StateId = 2 }, false);

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.Zero);
            Assert.That(apiConn.UpdateTicketStateCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task UpdateTicketStateInDb_ReusesSuppliedPreviousTicketWithoutReadingItAgain()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                Ticket = new WfTicket { Id = 101, StateId = 1, Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.approval);
            WfTicket? storedTicket = await dbAccess.LoadPreviousTicket(101);
            int readsAfterLoad = apiConn.GetTicketByIdCallCount;

            await dbAccess.UpdateTicketStateInDb(new WfTicket { Id = 101, StateId = 2 }, false, storedTicket);

            Assert.That(apiConn.GetTicketByIdCallCount, Is.EqualTo(readsAfterLoad));
            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task UpdateTicketInDb_TagsEntryWithWorkflowModule()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                Ticket = new WfTicket { Id = 101, Title = "Old title", Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.approval);

            await dbAccess.UpdateTicketInDb(new WfTicket { Id = 101, Title = "Corrected title" });

            // module, not change_source, is what separates the two producers of this table
            Assert.That(HistoryStringValue(apiConn, "module"), Is.EqualTo(GlobalConst.kModuleWorkflow));
            Assert.That(HistoryStringValue(apiConn, "changeSource"), Is.EqualTo(GlobalConst.kModuleWorkflow));
        }

        [Test]
        public async Task LoadPreviousTicket_ReturnsNullWhenTicketCannotBeRead()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                ThrowOnGetTicketById = true,
                Ticket = new WfTicket { Id = 101 }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.request);

            Assert.That(await dbAccess.LoadPreviousTicket(101), Is.Null);
        }

        [Test]
        public async Task UpdateTicketStateInDb_RunsStateActionsWhenHistoryInsertIsRejected()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                ThrowOnAddHistoryEntry = true,
                Ticket = new WfTicket { Id = 101, StateId = 1, Requester = new UiUser { DbId = 7 } }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>());
            bool errorShown = false;
            WfDbAccess dbAccess = new((_, _, _, isError) => errorShown |= isError, userConfig, apiConn,
                actionHandler, false, WorkflowPhases.approval);

            await dbAccess.UpdateTicketStateInDb(new WfTicket { Id = 101, StateId = 2 }, false);

            // a rejected history insert must neither surface as an error nor abort the state change
            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(apiConn.UpdateTicketStateCallCount, Is.EqualTo(1));
            Assert.That(errorShown, Is.False);
        }

        [Test]
        public async Task AddReqTaskToDb_ReturnsZero_WhenInsertReturnsNoIds()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                ReturnNullNewReqTaskIds = true
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfReqTask reqTask = new()
            {
                Id = 0,
                TicketId = 77,
                StateId = 1,
                TaskNumber = 1,
                TaskType = WfTaskType.access.ToString(),
                Elements = new List<WfReqElement>(),
                Approvals = new List<WfApproval>(),
                Owners = new List<FwoOwnerDataHelper>()
            };

            long newId = await dbAccess.AddReqTaskToDb(reqTask);

            Assert.That(newId, Is.EqualTo(0));
            Assert.That(apiConn.NewReqTaskCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddReqTaskToDb_StoresInsertedTaskId()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                NewReqTaskId = 201
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>
            {
                new() { Id = 1, Name = "requested" }
            });
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfReqTask reqTask = new()
            {
                Id = 0,
                TicketId = 77,
                StateId = 1,
                TaskNumber = 1,
                TaskType = WfTaskType.access.ToString(),
                Elements = new List<WfReqElement>(),
                Approvals = new List<WfApproval>(),
                Owners = new List<FwoOwnerDataHelper>()
            };

            long newId = await dbAccess.AddReqTaskToDb(reqTask);

            Assert.That(newId, Is.EqualTo(201));
            Assert.That(reqTask.Id, Is.EqualTo(201));
            Assert.That(apiConn.NewReqTaskCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task UpdateReqTaskInDb_SkipsLockedTask()
        {
            WfDbAccessTestApiConn apiConn = new();
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfReqTask reqTask = new()
            {
                Id = 100,
                Locked = true
            };

            await dbAccess.UpdateReqTaskInDb(reqTask);

            Assert.That(apiConn.UpdateReqTaskCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task UpdateReqTaskInDb_UpdatesNestedRequestElements()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedReqTaskId = 100,
                UpdatedReqElementId = 22,
                DeletedReqElementId = 11,
                NewReqElementId = 301
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>
            {
                new() { Id = 1, Name = "requested" }
            });
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfReqTask reqTask = new()
            {
                Id = 100,
                TicketId = 77,
                StateId = 1,
                Elements = new List<WfReqElement>
                {
                    new() { Id = 0, Field = ElemFieldType.service.ToString(), RequestAction = RequestAction.create.ToString(), FlowServiceObjectId = 7 },
                    new() { Id = 22, Field = ElemFieldType.rule.ToString(), RequestAction = RequestAction.modify.ToString(), RuleUid = "abc" }
                },
                RemovedElements = new List<WfReqElement>
                {
                    new() { Id = 11 }
                },
                Owners = new List<FwoOwnerDataHelper>()
            };

            await dbAccess.UpdateReqTaskInDb(reqTask);

            Assert.That(apiConn.UpdateReqTaskCallCount, Is.EqualTo(1));
            Assert.That(apiConn.NewReqElementCallCount, Is.EqualTo(1));
            Assert.That(apiConn.UpdateReqElementCallCount, Is.EqualTo(1));
            Assert.That(apiConn.DeleteReqElementCallCount, Is.EqualTo(1));
            Assert.That(reqTask.Elements[0].Id, Is.EqualTo(301));
            Assert.That(reqTask.Elements[1].Id, Is.EqualTo(22));
        }

        [Test]
        public async Task UpdateReqTaskInDb_ReportsMismatchWhenReturnedIdDiffers()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedReqTaskId = 999
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfReqTask reqTask = new()
            {
                Id = 100,
                TicketId = 77,
                StateId = 1,
                Elements = new List<WfReqElement>(),
                Owners = new List<FwoOwnerDataHelper>()
            };

            await dbAccess.UpdateReqTaskInDb(reqTask);

            Assert.That(apiConn.UpdateReqTaskCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task UpdateReqTaskAdditionalInfo_ReportsMismatchWhenReturnedIdDiffers()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedReqTaskAdditionalInfoId = 999
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfReqTask reqTask = new()
            {
                Id = 100,
                AdditionalInfo = "{}"
            };

            await dbAccess.UpdateReqTaskAdditionalInfo(reqTask);

            Assert.That(apiConn.UpdateReqTaskAdditionalInfoCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task DeleteReqTaskFromDb_ReportsMismatchWhenReturnedIdDiffers()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                DeletedReqTaskId = 999
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfReqTask reqTask = new()
            {
                Id = 100
            };

            await dbAccess.DeleteReqTaskFromDb(reqTask);

            Assert.That(apiConn.DeleteReqTaskCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddReqElementToDb_ReturnsZero_WhenInsertReturnsNoIds()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                ReturnNullNewReqElementIds = true
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfReqElement element = new()
            {
                TaskId = 100,
                Field = ElemFieldType.service.ToString(),
                RequestAction = RequestAction.create.ToString(),
                FlowServiceObjectId = 7
            };

            MethodInfo? method = typeof(WfDbAccess).GetMethod("AddReqElementToDb", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            long result = (long)await (Task<long>)method!.Invoke(dbAccess, new object[] { element })!;

            Assert.That(result, Is.EqualTo(0));
            Assert.That(apiConn.NewReqElementCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task UpdateReqElementInDb_ReportsMismatchWhenReturnedIdDiffers()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedReqElementId = 999
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfReqElement element = new()
            {
                Id = 100,
                Field = ElemFieldType.service.ToString(),
                RequestAction = RequestAction.modify.ToString(),
                FlowServiceObjectId = 7
            };

            MethodInfo? method = typeof(WfDbAccess).GetMethod("UpdateReqElementInDb", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            await (Task)method!.Invoke(dbAccess, new object[] { element })!;

            Assert.That(apiConn.UpdateReqElementCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task DeleteReqElementFromDb_ReportsMismatchWhenReturnedIdDiffers()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                DeletedReqElementId = 999
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            MethodInfo? method = typeof(WfDbAccess).GetMethod("DeleteReqElementFromDb", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            await (Task)method!.Invoke(dbAccess, new object[] { 100L })!;

            Assert.That(apiConn.DeleteReqElementCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddImplTaskToDb_ReturnsZero_WhenInsertReturnsNoIds()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                ReturnNullNewImplTaskIds = true
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfImplTask implTask = new()
            {
                Id = 0,
                ReqTaskId = 77,
                StateId = 1,
                TaskNumber = 1,
                TaskType = WfTaskType.access.ToString(),
                ImplElements = new List<WfImplElement>(),
                RemovedElements = new List<WfImplElement>()
            };

            long newId = await dbAccess.AddImplTaskToDb(implTask);

            Assert.That(newId, Is.EqualTo(0));
            Assert.That(apiConn.NewImplTaskCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddImplTaskToDb_StoresInsertedTaskId()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                NewImplTaskId = 401
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>
            {
                new() { Id = 1, Name = "requested" }
            });
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfImplTask implTask = new()
            {
                Id = 0,
                ReqTaskId = 77,
                StateId = 1,
                TaskNumber = 1,
                TaskType = WfTaskType.access.ToString(),
                ImplElements = new List<WfImplElement>(),
                RemovedElements = new List<WfImplElement>()
            };

            long newId = await dbAccess.AddImplTaskToDb(implTask);

            Assert.That(newId, Is.EqualTo(401));
            Assert.That(implTask.Id, Is.EqualTo(401));
            Assert.That(apiConn.NewImplTaskCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddImplTaskToDb_PersistsInsertedComments()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                NewImplTaskId = 401,
                NewCommentId = 777
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>
            {
                new() { Id = 1, Name = "requested" }
            });
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfImplTask implTask = new()
            {
                Id = 0,
                ReqTaskId = 77,
                StateId = 1,
                TaskNumber = 1,
                TaskType = WfTaskType.access.ToString(),
                Comments = new List<WfCommentDataHelper>
                {
                    new(new WfComment { CommentText = "comment" })
                },
                ImplElements = new List<WfImplElement>(),
                RemovedElements = new List<WfImplElement>()
            };

            long newId = await dbAccess.AddImplTaskToDb(implTask);

            Assert.That(newId, Is.EqualTo(401));
            Assert.That(implTask.Comments[0].Comment.Id, Is.EqualTo(777));
            Assert.That(apiConn.NewCommentCallCount, Is.EqualTo(1));
            Assert.That(apiConn.AssignImplCommentCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task UpdateImplTaskInDb_UpdatesNestedImplementationElements()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedImplTaskId = 100,
                UpdatedImplElementId = 52,
                DeletedImplElementId = 51,
                NewImplElementId = 501
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            await actionHandler.Init(new List<WfState>
            {
                new() { Id = 1, Name = "requested" }
            });
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfImplTask implTask = new()
            {
                Id = 100,
                ReqTaskId = 77,
                StateId = 1,
                ImplElements = new List<WfImplElement>
                {
                    new() { Id = 0, Field = ElemFieldType.service.ToString(), ImplAction = RequestAction.create.ToString(), FlowServiceObjectId = 7 },
                    new() { Id = 52, Field = ElemFieldType.rule.ToString(), ImplAction = RequestAction.modify.ToString(), RuleUid = "abc" }
                },
                RemovedElements = new List<WfImplElement>
                {
                    new() { Id = 51 }
                }
            };
            WfReqTask reqTask = new()
            {
                Id = 77,
                Owners = new List<FwoOwnerDataHelper>()
            };

            await dbAccess.UpdateImplTaskInDb(implTask, reqTask);

            Assert.That(apiConn.UpdateImplTaskCallCount, Is.EqualTo(1));
            Assert.That(apiConn.NewImplElementCallCount, Is.EqualTo(1));
            Assert.That(apiConn.UpdateImplElementCallCount, Is.EqualTo(1));
            Assert.That(apiConn.DeleteImplElementCallCount, Is.EqualTo(1));
            Assert.That(implTask.ImplElements[0].Id, Is.EqualTo(501));
            Assert.That(implTask.ImplElements[1].Id, Is.EqualTo(52));
        }

        [Test]
        public async Task UpdateImplTaskInDb_ReportsMismatchWhenReturnedIdDiffers()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedImplTaskId = 999
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfImplTask implTask = new()
            {
                Id = 100,
                ReqTaskId = 77,
                StateId = 1,
                ImplElements = new List<WfImplElement>(),
                Comments = new List<WfCommentDataHelper>(),
                RemovedElements = new List<WfImplElement>()
            };
            WfReqTask reqTask = new()
            {
                Id = 77,
                Owners = new List<FwoOwnerDataHelper>()
            };

            await dbAccess.UpdateImplTaskInDb(implTask, reqTask);

            Assert.That(apiConn.UpdateImplTaskCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task DeleteImplTaskFromDb_ReportsMismatchWhenReturnedIdDiffers()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                DeletedImplTaskId = 999
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfImplTask implTask = new()
            {
                Id = 100
            };

            await dbAccess.DeleteImplTaskFromDb(implTask);

            Assert.That(apiConn.DeleteImplTaskCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddImplElementToDb_ReturnsZero_WhenInsertReturnsNoIds()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                ReturnNullNewImplElementIds = true
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfImplElement element = new()
            {
                ImplTaskId = 100,
                Field = ElemFieldType.service.ToString(),
                ImplAction = RequestAction.create.ToString(),
                FlowServiceObjectId = 7
            };

            MethodInfo? method = typeof(WfDbAccess).GetMethod("AddImplElementToDb", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            long result = (long)await (Task<long>)method!.Invoke(dbAccess, new object[] { element })!;

            Assert.That(result, Is.EqualTo(0));
            Assert.That(apiConn.NewImplElementCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task UpdateImplElementInDb_ReportsMismatchWhenReturnedIdDiffers()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedImplElementId = 999
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfImplElement element = new()
            {
                Id = 100,
                Field = ElemFieldType.service.ToString(),
                ImplAction = RequestAction.modify.ToString(),
                FlowServiceObjectId = 7
            };

            MethodInfo? method = typeof(WfDbAccess).GetMethod("UpdateImplElementInDb", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            await (Task)method!.Invoke(dbAccess, new object[] { element })!;

            Assert.That(apiConn.UpdateImplElementCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task DeleteImplElementFromDb_ReportsMismatchWhenReturnedIdDiffers()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                DeletedImplElementId = 999
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            MethodInfo? method = typeof(WfDbAccess).GetMethod("DeleteImplElementFromDb", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            await (Task)method!.Invoke(dbAccess, new object[] { 100L })!;

            Assert.That(apiConn.DeleteImplElementCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task FilterWrongOwnersOut_DropsNonOwnerVisibleTickets()
        {
            WfDbAccessTestApiConn apiConn = new() { RegisteredTicketIds = [1] };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfTicket ticket = new() { Id = 2, Requester = new UiUser { DbId = 201 } };
            List<WfTicket> tickets = [ticket];

            MethodInfo? filterMethod = typeof(WfDbAccess).GetMethod("FilterWrongOwnersOut", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(filterMethod, Is.Not.Null);
            Task<List<WfTicket>> filterTask = (Task<List<WfTicket>>)filterMethod!.Invoke(dbAccess, new object[] { tickets, new List<int> { 7 } })!;
            List<WfTicket> filtered = await filterTask;

            Assert.That(filtered, Is.Empty);
        }

        [Test]
        public async Task FetchTicket_ReturnsNull_WhenTicketIsNotOwnerVisible()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                RegisteredTicketIds = [1],
                Ticket = new WfTicket
                {
                    Id = 2,
                    Requester = new UiUser { DbId = 201 }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            userConfig.ReqOwnerBased = true;
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfTicket? ticket = await dbAccess.FetchTicket(2, [7], _ => true);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task FetchTicket_ReturnsNull_WhenTicketFilterRejectsTicket()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 2,
                    Requester = new UiUser { DbId = 201 }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfTicket? ticket = await dbAccess.FetchTicket(2, null, _ => false);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task FetchTickets_AppliesOwnerFilterBeforeVisibilityFilter()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                RegisteredTicketIds = [1],
                Tickets =
                [
                    new WfTicket { Id = 1, Requester = new UiUser { DbId = 200 } },
                    new WfTicket { Id = 2, Requester = new UiUser { DbId = 201 } }
                ]
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            userConfig.ReqOwnerBased = true;
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);
            StateMatrix matrix = new() { LowestInputState = 0, LowestEndState = 10 };

            List<WfTicket> tickets = await dbAccess.FetchTickets(matrix, [7], false, false, _ => true);

            Assert.That(tickets, Has.Count.EqualTo(1));
            Assert.That(tickets[0].Id, Is.EqualTo(1));
        }

        [Test]
        public async Task FetchTickets_DoesNotApplyOwnerFilteringForAdmin()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                RegisteredTicketIds = [],
                Tickets =
                [
                    new WfTicket { Id = 1, Requester = new UiUser { DbId = 200 } }
                ]
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            userConfig.ReqOwnerBased = true;
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, true, WorkflowPhases.request);
            StateMatrix matrix = new() { LowestInputState = 0, LowestEndState = 10 };

            List<WfTicket> tickets = await dbAccess.FetchTickets(matrix, [7], false, false, _ => true);

            Assert.That(tickets, Has.Count.EqualTo(1));
            Assert.That(tickets[0].Id, Is.EqualTo(1));
            Assert.That(tickets[0].Editable, Is.True);
        }

        [Test]
        public async Task FetchTicket_ReturnsNull_WhenOwnerListIsEmpty()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 2,
                    Requester = new UiUser { DbId = 201 }
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            userConfig.ReqOwnerBased = true;
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfTicket? ticket = await dbAccess.FetchTicket(2, [], _ => true);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task GetTicket_UpdatesCidrsAndResetsStateTracking()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 2,
                    StateId = 5,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 11,
                            StateId = 7,
                            Elements =
                            [
                                new WfReqElement
                                {
                                    Id = 21,
                                    IpString = "10.0.0.1"
                                }
                            ]
                        }
                    ]
                }
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            WfTicket ticket = await dbAccess.GetTicket(2);

            Assert.Multiple(() =>
            {
                Assert.That(ticket.Id, Is.EqualTo(2));
                Assert.That(ticket.StateChanged(), Is.False);
                Assert.That(ticket.Tasks[0].StateChanged(), Is.False);
                Assert.That(ticket.Tasks[0].Elements[0].Cidr, Is.Not.Null);
            });
        }

        [Test]
        public async Task FetchTickets_WithFullTicketsUpdatesCidrsAndResetsStateTracking()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                Tickets =
                [
                    new WfTicket
                    {
                        Id = 1,
                        StateId = 5,
                        Tasks =
                        [
                            new WfReqTask
                            {
                                Id = 11,
                                StateId = 7,
                                Elements =
                                [
                                    new WfReqElement
                                    {
                                        Id = 21,
                                        IpString = "10.0.0.2"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);
            StateMatrix matrix = new() { LowestInputState = 0, LowestEndState = 10 };

            List<WfTicket> tickets = await dbAccess.FetchTickets(matrix, null, false, true, null);

            Assert.Multiple(() =>
            {
                Assert.That(tickets, Has.Count.EqualTo(1));
                Assert.That(tickets[0].StateChanged(), Is.False);
                Assert.That(tickets[0].Tasks[0].StateChanged(), Is.False);
                Assert.That(tickets[0].Tasks[0].Elements[0].Cidr, Is.Not.Null);
            });
        }

        [Test]
        public async Task GetTicketsByParameters_AppliesTicketFilter()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                Tickets =
                [
                    new WfTicket { Id = 1 },
                    new WfTicket { Id = 2 }
                ]
            };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

            List<WfTicket> tickets = await dbAccess.GetTicketsByParameters(
                WfTaskType.access.ToString(),
                0,
                10,
                DateTime.UtcNow.AddDays(-7),
                DateTime.UtcNow,
                ticket => ticket.Id == 2);

            Assert.That(tickets, Has.Count.EqualTo(1));
            Assert.That(tickets[0].Id, Is.EqualTo(2));
        }

        [Test]
        public void GetTicketsByParameters_UsesLowerBoundForCutoffDate()
        {
            Assert.That(RequestQueries.getTicketsByParameters, Does.Contain("date_created: { _gte: $createdFrom, _lte: $createdUntil }"));
            Assert.That(RequestQueries.getTicketsByParameters, Does.Contain("owner_responsibles"));
        }

        [Test]
        public async Task FindRuleUid_ReturnsTrue_WhenRuleFound()
        {
            WfDbAccessTestApiConn apiConn = new() { FindRuleUidHasMatch = true };
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConn, 100, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConn, wfHandler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

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
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false, WorkflowPhases.request);

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

        [Test]
        public async Task LogWorkflowChange_WritesPayloadThatSurvivesTheTransportSerializer()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                Ticket = new WfTicket { Id = 101, Title = "Old title", Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.approval);

            await dbAccess.UpdateTicketInDb(new WfTicket { Id = 101, Title = "Corrected title" });

            // the variables are serialized by the same serializer GraphQlApiConnection uses, so a payload
            // type Json.NET cannot handle would reach the database as wrapper metadata instead of data
            GraphQLRequest request = new() { Query = ModellingQueries.addHistoryEntry, Variables = apiConn.LastHistoryVariables };
            string payload = new NewtonsoftJsonSerializer().SerializeToString(request);
            Assert.Multiple(() =>
            {
                Assert.That(payload, Does.Contain("Old title"));
                Assert.That(payload, Does.Contain("Corrected title"));
                Assert.That(payload, Does.Not.Contain("ValueKind"));
            });
        }

        [Test]
        public async Task UpdateTicketStateInDb_RecordsCompletionDateWithoutStateChange()
        {
            DateTime completion = new(2026, 9, 5, 10, 30, 0, DateTimeKind.Unspecified);
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedTicketId = 101,
                Ticket = new WfTicket { Id = 101, StateId = 60, Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.approval);

            await dbAccess.UpdateTicketStateInDb(new WfTicket { Id = 101, StateId = 60, CompletionDate = completion }, false);

            // the mutation writes closed, deadline and priority, so a change to one of them is a change
            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryRawValue(apiConn, "newData"), Does.Contain("CompletionDate"));
            Assert.That(HistoryRawValue(apiConn, "oldData"), Does.Contain("null"));
        }

        [Test]
        public async Task UpdateReqTaskStateInDb_RecordsStopTimeWithoutStateChange()
        {
            DateTime stop = new(2026, 9, 5, 11, 0, 0, DateTimeKind.Unspecified);
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedReqTaskId = 100,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Requester = new UiUser { DbId = 7 },
                    Tasks = { new WfReqTask { Id = 100, TicketId = 101, StateId = 60 } }
                }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.implementation);

            await dbAccess.UpdateReqTaskStateInDb(new WfReqTask { Id = 100, TicketId = 101, StateId = 60, Stop = stop }, false);

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryRawValue(apiConn, "newData"), Does.Contain("Stop"));
        }

        [Test]
        public async Task UpdateImplTaskStateInDb_RecordsStartTimeWithoutStateChange()
        {
            DateTime start = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Unspecified);
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedImplTaskId = 200,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Requester = new UiUser { DbId = 7 },
                    Tasks = { new WfReqTask { Id = 100, TicketId = 101, ImplementationTasks = { new WfImplTask { Id = 200, TicketId = 101, StateId = 60 } } } }
                }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.implementation);

            await dbAccess.UpdateImplTaskStateInDb(new WfImplTask { Id = 200, TicketId = 101, StateId = 60, Start = start }, false);

            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryRawValue(apiConn, "newData"), Does.Contain("Start"));
        }

        [Test]
        public async Task UpdateReqTaskAdditionalInfo_IsNeverAuditProofCritical()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedReqTaskAdditionalInfoId = 100,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Requester = new UiUser { DbId = 7 },
                    Tasks = { new WfReqTask { Id = 100, TicketId = 101 } }
                }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.approval);
            WfReqTask reqTask = new() { Id = 100, TicketId = 101 };
            reqTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "77");

            await dbAccess.UpdateReqTaskAdditionalInfo(reqTask);

            // additional info is written by workflow actions, not typed by the user acting on the ticket
            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(1));
            Assert.That(HistoryCriticalFlag(apiConn), Is.False);
        }

        [Test]
        public async Task ReqTaskWrites_ReuseSuppliedPreviousTicketWithoutReadingItAgain()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                NewReqTaskId = 201,
                DeletedReqTaskId = 100,
                UpdatedReqTaskAdditionalInfoId = 100,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Requester = new UiUser { DbId = 7 },
                    Tasks = { new WfReqTask { Id = 100, TicketId = 101 } }
                }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.request);
            WfTicket? storedTicket = await dbAccess.LoadPreviousTicket(101);
            int readsAfterLoad = apiConn.GetTicketByIdCallCount;
            WfReqTask changedTask = new() { Id = 100, TicketId = 101 };
            changedTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "77");

            await dbAccess.AddReqTaskToDb(new WfReqTask { Id = 0, TicketId = 101, Title = "New task" }, storedTicket);
            await dbAccess.UpdateReqTaskAdditionalInfo(changedTask, storedTicket);
            await dbAccess.DeleteReqTaskFromDb(new WfReqTask { Id = 100, TicketId = 101, Title = "Removed task" }, storedTicket);

            Assert.That(apiConn.GetTicketByIdCallCount, Is.EqualTo(readsAfterLoad));
            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(3));
        }

        [Test]
        public async Task ImplTaskWrites_ReuseSuppliedPreviousTicketWithoutReadingItAgain()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                NewImplTaskId = 200,
                DeletedImplTaskId = 200,
                Ticket = new WfTicket { Id = 101, Requester = new UiUser { DbId = 7 } }
            };
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.implementation);
            WfTicket? storedTicket = await dbAccess.LoadPreviousTicket(101);
            int readsAfterLoad = apiConn.GetTicketByIdCallCount;

            await dbAccess.AddImplTaskToDb(new WfImplTask { Id = 0, TicketId = 101, Title = "New implementation" }, storedTicket);
            await dbAccess.DeleteImplTaskFromDb(new WfImplTask { Id = 200, TicketId = 101, Title = "Removed implementation" }, storedTicket);

            Assert.That(apiConn.GetTicketByIdCallCount, Is.EqualTo(readsAfterLoad));
            Assert.That(apiConn.AddHistoryEntryCallCount, Is.EqualTo(2));
        }

        [Test]
        public async Task ConfCleanupImplTasks_ReadsPreviousTicketOnceForAllTasks()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                DeletedImplTaskId = 200,
                Ticket = new WfTicket { Id = 101, Requester = new UiUser { DbId = 7 } }
            };
            WfHandler wfHandler = new();
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.implementation);
            SetWorkflowContext(wfHandler, dbAccess);
            wfHandler.ActReqTask = new WfReqTask { Id = 100, TicketId = 101 };
            wfHandler.ActReqTask.ImplementationTasks.Add(new WfImplTask { Id = 200, TicketId = 101 });
            wfHandler.ActReqTask.ImplementationTasks.Add(new WfImplTask { Id = 201, TicketId = 101 });
            wfHandler.ActReqTask.ImplementationTasks.Add(new WfImplTask { Id = 202, TicketId = 101 });

            await wfHandler.ConfCleanupImplTasks();

            Assert.That(apiConn.DeleteImplTaskCallCount, Is.EqualTo(3));
            Assert.That(apiConn.GetTicketByIdCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SetAddInfoInReqTasks_ReadsPreviousTicketOncePerTicket()
        {
            WfDbAccessTestApiConn apiConn = new()
            {
                UpdatedReqTaskAdditionalInfoId = 100,
                Ticket = new WfTicket
                {
                    Id = 101,
                    Requester = new UiUser { DbId = 7 },
                    Tasks = { new WfReqTask { Id = 100, TicketId = 101 } }
                }
            };
            WfHandler wfHandler = new();
            WfDbAccess dbAccess = await CreateHistoryDbAccess(apiConn, WorkflowPhases.approval);
            SetWorkflowContext(wfHandler, dbAccess);
            List<WfReqTask> reqTasks = new()
            {
                new WfReqTask { Id = 100, TicketId = 101 },
                new WfReqTask { Id = 101, TicketId = 101 },
                new WfReqTask { Id = 102, TicketId = 101 }
            };

            await wfHandler.SetAddInfoInReqTasks(reqTasks, "PolicyCheck", "true");

            Assert.That(apiConn.UpdateReqTaskAdditionalInfoCallCount, Is.EqualTo(3));
            Assert.That(apiConn.GetTicketByIdCallCount, Is.EqualTo(1));
        }

        private static WfState CreatePromotingState(int stateId)
        {
            return new WfState
            {
                Id = stateId,
                Name = "requested",
                Actions = new List<WfStateActionDataHelper>
                {
                    new()
                    {
                        SortOrder = 1,
                        Action = new WfStateAction
                        {
                            Name = "promote created task",
                            ActionType = StateActionTypes.AutoPromote.ToString(),
                            Scope = WfObjectScopes.RequestTask.ToString(),
                            TaskType = WfTaskType.group_create.ToString(),
                            Event = StateActionEvents.OnSet.ToString(),
                            ExternalParams = "100"
                        }
                    }
                }
            };
        }

        private static WfTicket CreatePromotingTicket()
        {
            return new WfTicket
            {
                Id = 0,
                StateId = 48,
                Requester = new UiUser { DbId = 42 },
                Tasks = new List<WfReqTask>
                {
                    new()
                    {
                        StateId = 48,
                        TaskType = WfTaskType.group_create.ToString()
                    },
                    new()
                    {
                        StateId = 48,
                        TaskType = WfTaskType.group_create.ToString()
                    },
                    new()
                    {
                        StateId = 48,
                        TaskType = WfTaskType.group_create.ToString()
                    }
                }
            };
        }

        private static StateMatrix CreateStateMatrix(int lowestInputState, int lowestStartedState, int lowestEndState)
        {
            return new StateMatrix
            {
                LowestInputState = lowestInputState,
                LowestStartedState = lowestStartedState,
                LowestEndState = lowestEndState
            };
        }

        private static string? HistoryStringValue(WfDbAccessTestApiConn apiConnection, string name)
        {
            PropertyInfo? property = apiConnection.LastHistoryVariables?.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null);
            return property!.GetValue(apiConnection.LastHistoryVariables)?.ToString();
        }

        private static async Task<WfDbAccess> CreateHistoryDbAccess(WfDbAccessTestApiConn apiConnection, WorkflowPhases phase)
        {
            UserConfig userConfig = new();
            await userConfig.InitWithUserId(apiConnection, 42, false);
            WfHandler wfHandler = new();
            ActionHandler actionHandler = new(apiConnection, wfHandler);
            await actionHandler.Init(new List<WfState>());
            return new WfDbAccess(DefaultInit.DoNothing, userConfig, apiConnection, actionHandler, false, phase);
        }

        private static string? HistoryRawValue(WfDbAccessTestApiConn apiConnection, string name)
        {
            PropertyInfo? property = apiConnection.LastHistoryVariables?.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null);
            // The payload fields are handed to the transport serializer unchanged, so read them back the
            // way that serializer would write them instead of calling ToString on the CLR object.
            return JsonConvert.SerializeObject(property!.GetValue(apiConnection.LastHistoryVariables));
        }

        private static bool HistoryCriticalFlag(WfDbAccessTestApiConn apiConnection)
        {
            PropertyInfo? property = apiConnection.LastHistoryVariables?.GetType().GetProperty("auditProofCritical");
            Assert.That(property, Is.Not.Null);
            return (bool)property!.GetValue(apiConnection.LastHistoryVariables)!;
        }

        private static void SetRequestTaskStateMatrix(WfHandler wfHandler, StateMatrix requestTaskMatrix)
        {
            FieldInfo? stateMatrixField = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(stateMatrixField, Is.Not.Null);

            StateMatrixDict stateMatrixDict = new()
            {
                Matrices = new Dictionary<string, StateMatrix>
                {
                    { WfTaskType.group_create.ToString(), requestTaskMatrix }
                }
            };

            stateMatrixField!.SetValue(wfHandler, stateMatrixDict);
        }

        private static void SetWorkflowContext(WfHandler wfHandler, WfDbAccess dbAccess)
        {
            FieldInfo? dbAccField = typeof(WfHandler).GetField("dbAcc", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(dbAccField, Is.Not.Null);
            dbAccField!.SetValue(wfHandler, dbAccess);
        }
    }
}
