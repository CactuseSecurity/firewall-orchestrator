using FWO.Api.Client;
using FWO.Api.Client.Queries;
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
    internal class WfHandlerTicketsTest
    {
        private static readonly long[] kVisibleRequestTaskIds = [11];
        private static readonly int[] kExclusiveVisibilityGroupIds = [1, 2, 4, 5, 6];
        private static readonly long[] kImplTaskIds = [21, 22];

        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            FieldInfo? field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            StateMatrixDict dict = (StateMatrixDict)(field!.GetValue(handler) ?? new StateMatrixDict());
            dict.Matrices[taskType] = matrix;
        }

        private static void EnableVisibilityChecks(UserConfig userConfig)
        {
            userConfig.ReqVisibilityBased = true;
        }

        private sealed class TicketTestApiConn : SimulatedApiConnection
        {
            public WfTicket Ticket { get; set; } = new();
            public List<WfTicket> Tickets { get; set; } = [];
            public List<long> RegisteredTicketIds { get; set; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == RequestQueries.getOwnerTicketIds)
                {
                    List<TicketId> ids = RegisteredTicketIds.ConvertAll(id => new TicketId { Id = id });
                    return Task.FromResult((T)(object)ids);
                }
                if (query == RequestQueries.getTicketById)
                {
                    return Task.FromResult((T)(object)Ticket);
                }
                if (query == RequestQueries.getTicketsByParameters || query == RequestQueries.getTickets || query == RequestQueries.getFullTickets)
                {
                    return Task.FromResult((T)(object)Tickets);
                }
                if (query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((T)(object)Array.Empty<ConfigItem>());
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private sealed class TestGlobalStateMatrix : GlobalStateMatrix
        {
            public override Task Init(ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master)
            {
                GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix>
                {
                    [WorkflowPhases.request] = new StateMatrix { LowestEndState = 5 },
                    [WorkflowPhases.approval] = new StateMatrix { LowestEndState = 10 },
                    [WorkflowPhases.planning] = new StateMatrix { LowestEndState = 20 }
                };
                return Task.CompletedTask;
            }
        }

        private static WfHandler CreateHandlerWithDbAccess(TicketTestApiConn apiConn, UserConfig userConfig)
        {
            WfHandler handler = new(DefaultInit.DoNothing, userConfig, new System.Security.Claims.ClaimsPrincipal(), apiConn, null!, WorkflowPhases.request);
            ActionHandler actionHandler = new(apiConn, handler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, userConfig, apiConn, actionHandler, false);
            FieldInfo? dbAccField = typeof(WfHandler).GetField("dbAcc", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(dbAccField, Is.Not.Null);
            dbAccField!.SetValue(handler, dbAccess);
            return handler;
        }

        [Test]
        public async Task HandleInjectedTicketId_ReturnsNewPhase_WhenStateInLaterPhase()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 12 } };
            UserConfig userConfig = new();
            Func<GlobalStateMatrix> originalFactory = GlobalStateMatrix.Factory;
            GlobalStateMatrix.Factory = () => new TestGlobalStateMatrix();
            try
            {
                WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
                handler.MasterStateMatrix.PhaseActive[WorkflowPhases.request] = true;
                handler.MasterStateMatrix.PhaseActive[WorkflowPhases.approval] = true;
                handler.MasterStateMatrix.PhaseActive[WorkflowPhases.planning] = true;
                handler.MasterStateMatrix.IsLastActivePhase = false;

                string phase = await handler.HandleInjectedTicketId(WorkflowPhases.request, 7);

                Assert.That(phase, Is.EqualTo(WorkflowPhases.planning.ToString()));
            }
            finally
            {
                GlobalStateMatrix.Factory = originalFactory;
            }
        }

        [Test]
        public async Task HandleInjectedTicketId_ReturnsEmpty_WhenNoDbAccess()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 12 } };
            UserConfig userConfig = new();
            WfHandler handler = new(DefaultInit.DoNothing, userConfig, new System.Security.Claims.ClaimsPrincipal(), apiConn, null!, WorkflowPhases.request);

            string phase = await handler.HandleInjectedTicketId(WorkflowPhases.request, 7);

            Assert.That(phase, Is.EqualTo(""));
        }

        [Test]
        public async Task ResolveTicket_ReturnsNull_WhenNoDbAccess()
        {
            WfHandler handler = new();

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task ResolveTicket_ReturnsNull_WhenVisibilityGroupDoesNotMatch()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 10 } };
            UserConfig userConfig = new();
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds = new Dictionary<int, List<int>>
                {
                    [10] = [3]
                }
            };

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task ResolveTicket_ReturnsNull_WhenUntaggedStateIsBlockedByExclusiveMembership()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 10 } };
            UserConfig userConfig = new();
            userConfig.User.WorkflowVisibilityGroupIds = [7];
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                ExclusiveVisibilityGroupIds = [7]
            };

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task ResolveTicket_AllowsVisibilityGroupMember()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 10 } };
            UserConfig userConfig = new();
            userConfig.User.WorkflowVisibilityGroupIds = [3];
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds = new Dictionary<int, List<int>>
                {
                    [10] = [3]
                }
            };

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Id, Is.EqualTo(7));
        }

        [Test]
        public async Task ResolveTicket_AllowsVisibleTicketWithoutRequestTasks()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 10 } };
            UserConfig userConfig = new();
            userConfig.User.WorkflowVisibilityGroupIds = [3];
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds = new Dictionary<int, List<int>>
                {
                    [10] = [3]
                }
            };

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Tasks, Is.Empty);
        }

        [Test]
        public async Task ResolveTicket_ReturnsNull_WhenAllChildTasksBecomeInvisible()
        {
            TicketTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 7,
                    StateId = 10,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 11,
                            TaskType = WfTaskType.access.ToString(),
                            StateId = 21
                        }
                    ]
                }
            };
            UserConfig userConfig = new();
            userConfig.User.WorkflowVisibilityGroupIds = [3, 7];
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [10] = [3]
                },
                ExclusiveVisibilityGroupIds = [7]
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [20] = [3]
                },
                ExclusiveVisibilityGroupIds = [7]
            });

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task ResolveTicket_ReturnsNull_WhenTicketHasNoVisibleRequestTasksEvenIfStateIsVisible()
        {
            TicketTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 7,
                    StateId = 10,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 11,
                            TaskType = WfTaskType.access.ToString(),
                            StateId = 21
                        }
                    ]
                }
            };
            UserConfig userConfig = new();
            userConfig.User.WorkflowVisibilityGroupIds = [3];
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [10] = [3]
                }
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [21] = [99]
                },
                ExclusiveVisibilityGroupIds = [3]
            });

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task ResolveTicket_ReturnsNull_WhenApprovalTicketContainsOnlyNewInterfaceTasksForExclusiveMember()
        {
            TicketTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 7,
                    StateId = 10,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 11,
                            TaskType = WfTaskType.new_interface.ToString(),
                            StateId = 20
                        }
                    ]
                }
            };
            UserConfig userConfig = new();
            userConfig.User.WorkflowVisibilityGroupIds = [3];
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.Phase = WorkflowPhases.approval;
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [10] = [3]
                },
                ExclusiveVisibilityGroupIds = [3]
            };
            SetMatrix(handler, WfTaskType.new_interface.ToString(), new StateMatrix
            {
                ExclusiveVisibilityGroupIds = [3]
            });
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix());

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Null);
        }

        [Test]
        public async Task ResolveTicket_KeepsVisibleRequestTasksAndDropsInvisibleOnes()
        {
            TicketTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 7,
                    StateId = 10,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 11,
                            TaskType = WfTaskType.access.ToString(),
                            StateId = 21
                        },
                        new WfReqTask
                        {
                            Id = 12,
                            TaskType = WfTaskType.new_interface.ToString(),
                            StateId = 22
                        }
                    ]
                }
            };
            UserConfig userConfig = new();
            userConfig.User.WorkflowVisibilityGroupIds = [3];
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [10] = [3]
                }
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [21] = [3]
                }
            });
            SetMatrix(handler, WfTaskType.new_interface.ToString(), new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [22] = [99]
                }
            });

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Tasks.Select(task => task.Id), Is.EqualTo(kVisibleRequestTaskIds));
        }

        [Test]
        public async Task SelectTicket_ReturnsWithoutReloading_WhenTicketIsNotOwnerVisible()
        {
            TicketTestApiConn apiConn = new()
            {
                RegisteredTicketIds = [1],
                Ticket = new WfTicket
                {
                    Id = 7,
                    StateId = 10,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 11,
                            TaskType = WfTaskType.access.ToString(),
                            StateId = 21
                        }
                    ]
                }
            };
            UserConfig userConfig = new();
            userConfig.User.WorkflowVisibilityGroupIds = [3];
            userConfig.ReqOwnerBased = true;
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.AllOwners = [new FwoOwner { Id = 7 }];
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [10] = [3]
                }
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [21] = [3]
                }
            });
            typeof(WfHandler).GetField("ReloadTasks", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(handler, true);
            handler.TicketList = [new WfTicket { Id = 7, StateId = 10 }];
            handler.ActTicket = new WfTicket { Id = 99 };

            await handler.SelectTicket(handler.TicketList[0], ObjAction.display, true);

            Assert.That(handler.ActTicket.Id, Is.EqualTo(99));
            Assert.That(handler.TicketList[0].Tasks, Is.Empty);
        }

        [Test]
        public async Task ResolveTicket_ReturnsVisibleTicket_WhenVisibilityChecksDisabled()
        {
            TicketTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 7,
                    StateId = 10,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 11,
                            TaskType = WfTaskType.access.ToString(),
                            StateId = 21
                        }
                    ]
                }
            };
            UserConfig userConfig = new();
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [10] = [99]
                }
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [21] = [99]
                },
                ExclusiveVisibilityGroupIds = [3]
            });

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Tasks, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ResolveTicket_AllowsApprovalAssignedToCurrentUserEvenIfVisibilityGroupsDoNotMatch()
        {
            TicketTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 7,
                    StateId = 10,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 11,
                            TaskType = WfTaskType.access.ToString(),
                            StateId = 21,
                            Approvals =
                            [
                                new WfApproval
                                {
                                    Id = 31,
                                    StateId = 41,
                                    ApproverDn = "uid=approver,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal"
                                }
                            ]
                        }
                    ]
                }
            };
            UserConfig userConfig = new();
            userConfig.User.Dn = "uid=approver,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal";
            userConfig.User.WorkflowVisibilityGroupIds = [3];
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [10] = [99]
                }
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [21] = [99]
                }
            });

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Tasks, Has.Count.EqualTo(1));
            Assert.That(ticket.Tasks[0].Approvals, Has.Count.EqualTo(1));
            Assert.That(ticket.Tasks[0].Approvals[0].Id, Is.EqualTo(31));
        }

        [Test]
        public async Task ResolveTicket_AllowsRequestTaskAssignedToCurrentUserEvenIfVisibilityGroupsDoNotMatch()
        {
            TicketTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 7,
                    StateId = 10,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 11,
                            TaskType = WfTaskType.access.ToString(),
                            StateId = 21,
                            CurrentHandler = new UiUser
                            {
                                DbId = 13,
                                Dn = "uid=approver,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal"
                            }
                        }
                    ]
                }
            };
            UserConfig userConfig = new();
            userConfig.User.Dn = "uid=approver,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal";
            userConfig.User.WorkflowVisibilityGroupIds = [3];
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [10] = [99]
                }
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [21] = [99]
                }
            });

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Tasks, Has.Count.EqualTo(1));
            Assert.That(ticket.Tasks[0].Id, Is.EqualTo(11));
        }

        [Test]
        public async Task ResolveTicket_AllowsRequestTaskAssignedToCurrentGroupEvenIfVisibilityGroupsDoNotMatch()
        {
            string groupDn = "cn=approvers,ou=groups,dc=fworch,dc=internal";
            TicketTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 7,
                    StateId = 10,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 11,
                            TaskType = WfTaskType.access.ToString(),
                            StateId = 21,
                            AssignedGroup = groupDn
                        }
                    ]
                }
            };
            UserConfig userConfig = new();
            userConfig.User.Dn = "uid=approver,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal";
            userConfig.User.Groups = [groupDn];
            userConfig.User.WorkflowVisibilityGroupIds = [3];
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [10] = [99]
                }
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [21] = [99]
                }
            });

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Tasks, Has.Count.EqualTo(1));
            Assert.That(ticket.Tasks[0].Id, Is.EqualTo(11));
        }

        [Test]
        public async Task ResolveTicket_AllowsApprovalAssignedToCurrentGroupEvenIfVisibilityGroupsDoNotMatch()
        {
            string groupDn = "cn=approvers,ou=groups,dc=fworch,dc=internal";
            TicketTestApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 7,
                    StateId = 10,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 11,
                            TaskType = WfTaskType.access.ToString(),
                            StateId = 21,
                            Approvals =
                            [
                                new WfApproval
                                {
                                    Id = 31,
                                    StateId = 41,
                                    ApproverGroup = groupDn
                                }
                            ]
                        }
                    ]
                }
            };
            UserConfig userConfig = new();
            userConfig.User.Dn = "uid=approver,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal";
            userConfig.User.Groups = [groupDn];
            userConfig.User.WorkflowVisibilityGroupIds = [3];
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [10] = [99]
                }
            };
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [21] = [99]
                }
            });

            WfTicket? ticket = await handler.ResolveTicket(7);

            Assert.That(ticket, Is.Not.Null);
            Assert.That(ticket!.Tasks, Has.Count.EqualTo(1));
            Assert.That(ticket.Tasks[0].Approvals, Has.Count.EqualTo(1));
            Assert.That(ticket.Tasks[0].Approvals[0].Id, Is.EqualTo(31));
        }

        [Test]
        public void GetWorkflowExclusiveVisibilityGroupIds_MergesMasterAndPhaseMatrices()
        {
            WfHandler handler = new();
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                ExclusiveVisibilityGroupIds = [4, 5]
            });
            SetMatrix(handler, WfTaskType.new_interface.ToString(), new StateMatrix
            {
                ExclusiveVisibilityGroupIds = [5, 6]
            });
            handler.MasterStateMatrix = new StateMatrix
            {
                ExclusiveVisibilityGroupIds = [1, 2]
            };

            HashSet<int> exclusiveGroupIds = handler.GetWorkflowExclusiveVisibilityGroupIds();

            Assert.That(exclusiveGroupIds, Is.EquivalentTo(kExclusiveVisibilityGroupIds));
        }

        [Test]
        public async Task HandleInjectedTicketId_ReturnsEmpty_WhenStateBeforeEnd()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 5 } };
            UserConfig userConfig = new();
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix.LowestEndState = 10;
            handler.MasterStateMatrix.IsLastActivePhase = false;

            string phase = await handler.HandleInjectedTicketId(WorkflowPhases.request, 7);

            Assert.That(phase, Is.EqualTo(""));
            Assert.That(handler.DisplayTicketMode, Is.True);
            Assert.That(handler.EditTicketMode, Is.True);
        }

        [Test]
        public async Task HandleInjectedTicketId_ReturnsEmpty_WhenLastActivePhase()
        {
            TicketTestApiConn apiConn = new() { Ticket = new WfTicket { Id = 7, StateId = 12 } };
            UserConfig userConfig = new();
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            handler.MasterStateMatrix.LowestEndState = 10;
            handler.MasterStateMatrix.IsLastActivePhase = true;

            string phase = await handler.HandleInjectedTicketId(WorkflowPhases.request, 7);

            Assert.That(phase, Is.EqualTo(""));
            Assert.That(handler.DisplayTicketMode, Is.True);
            Assert.That(handler.EditTicketMode, Is.False);
        }

        [Test]
        public async Task GetOpenTickets_ReturnsEmpty_WhenNoDbAccess()
        {
            WfHandler handler = new();

            List<WfTicket> tickets = await handler.GetOpenTickets(WfTaskType.access.ToString());

            Assert.That(tickets, Is.Empty);
        }

        [Test]
        public async Task GetOpenTickets_DoesNotApplyVisibilityFiltering()
        {
            TicketTestApiConn apiConn = new()
            {
                Tickets =
                [
                    new WfTicket
                    {
                        Id = 7,
                        StateId = 10
                    }
                ]
            };
            UserConfig userConfig = new();
            EnableVisibilityChecks(userConfig);
            WfHandler handler = CreateHandlerWithDbAccess(apiConn, userConfig);
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix
            {
                LowestInputState = 1,
                LowestEndState = 10
            });
            handler.MasterStateMatrix = new StateMatrix
            {
                StateVisibilityGroupIds =
                {
                    [10] = [3]
                }
            };

            List<WfTicket> tickets = await handler.GetOpenTickets(WfTaskType.access.ToString());

            Assert.That(tickets, Has.Count.EqualTo(1));
            Assert.That(tickets[0].Id, Is.EqualTo(7));
        }

        [Test]
        public void SetTicketEnv_SetsActiveTicketAndCollectsImplementationTasks()
        {
            WfHandler handler = new()
            {
                MasterStateMatrix = new StateMatrix { LowestInputState = 1 }
            };
            WfReqTask reqTask = new()
            {
                Id = 11,
                ImplementationTasks =
                {
                    new WfImplTask { Id = 21 },
                    new WfImplTask { Id = 22 }
                }
            };
            WfTicket ticket = new() { Id = 7, Tasks = { reqTask } };

            handler.SetTicketEnv(ticket);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActTicket, Is.SameAs(ticket));
                Assert.That(handler.ActStateMatrix, Is.SameAs(handler.MasterStateMatrix));
                Assert.That(handler.AllTicketImplTasks.Select(task => task.Id), Is.EqualTo(kImplTaskIds));
                Assert.That(handler.AllTicketImplTasks.All(task => task.TicketId == 7), Is.True);
                Assert.That(handler.AllTicketImplTasks.All(task => task.ReqTaskId == 11), Is.True);
            });
        }

        [Test]
        public async Task SelectTicket_SetsEnvironmentAndMode()
        {
            WfHandler handler = new()
            {
                MasterStateMatrix = new StateMatrix { LowestInputState = 1 }
            };
            WfTicket ticket = new() { Id = 7, StateId = 5 };

            await handler.SelectTicket(ticket, ObjAction.edit);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActTicket, Is.SameAs(ticket));
                Assert.That(handler.DisplayTicketMode, Is.True);
                Assert.That(handler.EditTicketMode, Is.True);
                Assert.That(handler.AddTicketMode, Is.False);
            });
        }

        [Test]
        public void SetTicketOpt_SetsModesAndResetClearsModes()
        {
            WfHandler handler = new();

            handler.SetTicketOpt(ObjAction.add);
            Assert.That(handler.DisplayTicketMode, Is.True);
            Assert.That(handler.EditTicketMode, Is.True);
            Assert.That(handler.AddTicketMode, Is.True);

            handler.ResetTicketActions();
            Assert.That(handler.DisplayTicketMode, Is.False);
            Assert.That(handler.EditTicketMode, Is.False);
            Assert.That(handler.AddTicketMode, Is.False);
        }

        [Test]
        public void SetTicketPopUpOpt_SetsPopupFlags()
        {
            WfHandler handler = new();

            handler.SetTicketPopUpOpt(ObjAction.displayPromote);
            Assert.That(handler.DisplayPromoteTicketMode, Is.True);

            handler.SetTicketPopUpOpt(ObjAction.displaySaveTicket);
            Assert.That(handler.DisplaySaveTicketMode, Is.True);
        }

        [Test]
        public void ResetTicketActions_ClearsPopupFlags()
        {
            WfHandler handler = new()
            {
                DisplayPromoteTicketMode = true,
                DisplaySaveTicketMode = true
            };

            handler.ResetTicketActions();

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayPromoteTicketMode, Is.False);
                Assert.That(handler.DisplaySaveTicketMode, Is.False);
            });
        }

        [Test]
        public async Task SaveTicket_ReturnsZero_WhenNoDbAccess()
        {
            WfHandler handler = new();

            long ticketId = await handler.SaveTicket(new WfStatefulObject { StateId = 5 });

            Assert.That(ticketId, Is.EqualTo(0));
        }

        [Test]
        public async Task ConfAddCommentToTicket_AddsComment()
        {
            WfHandler handler = new();
            handler.ActTicket = new WfTicket();

            await handler.ConfAddCommentToTicket("comment");

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActTicket.Comments, Has.Count.EqualTo(1));
                Assert.That(handler.ActTicket.Comments[0].Comment.Scope, Is.EqualTo(WfObjectScopes.Ticket.ToString()));
                Assert.That(handler.ActTicket.Comments[0].Comment.CommentText, Is.EqualTo("comment"));
            });
        }
    }
}
