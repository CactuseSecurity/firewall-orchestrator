using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Services;
using FWO.Services;
using FWO.Services.Workflow;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FWO.Test
{
    [TestFixture]
    internal class WorkflowMiddlewareUnitTest
    {
        private static readonly string[] kExpectedParsedGroups =
        [
            "cn=a,dc=fworch,dc=internal",
            "cn=b,dc=fworch,dc=internal"
        ];
        private static readonly string[] kGetUserEmailsQuery = [AuthQueries.getUserEmails];
        private static readonly string[] kExpectedResolvedUserDns = ["uid=user,ou=users,dc=test"];

        private sealed class RecipientResolverApiConn : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<UiUser> Users { get; set; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                if (query == AuthQueries.getUserEmails)
                {
                    return Task.FromResult((T)(object)Users);
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private sealed class TestRequestedRulePolicyChecker : IRequestedRulePolicyChecker
        {
            public Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks)
            {
                return Task.FromResult(true);
            }
        }

        private sealed class TestWorkflowRecipientResolver : IWorkflowRecipientResolver
        {
            public Task<List<string>> ResolveUserDns(IEnumerable<string> dns)
            {
                return Task.FromResult(dns.ToList());
            }

            public Task<List<UiUser>> ResolveUsers(IEnumerable<string> dns)
            {
                return Task.FromResult(dns.Select(dn => new UiUser { Dn = dn, Email = $"{dn}@example.test" }).ToList());
            }
        }

        private sealed class WorkflowExecutionApiConn : ApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<string> Roles { get; } = [];
            public List<WfState> States { get; set; } = [];
            public WfTicket Ticket { get; set; } = new();

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null,
                string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                if (query == RequestQueries.getStates)
                {
                    return Task.FromResult((T)(object)States);
                }

                if (query == RequestQueries.getTicketById)
                {
                    return Task.FromResult((T)(object)Ticket);
                }

                if (query.Contains("getConfigItemsByUser", StringComparison.OrdinalIgnoreCase)
                    || query.Contains("getConfigItemByKey", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(CreateEmptyQueryResult<T>());
                }

                throw new AssertionException($"Unexpected query: {query}");
            }

            public override Task<ApiResponse<T>> SendQuerySafeAsync<T>(string query, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override void SetAuthHeader(string jwt)
            { }

            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override void SetRole(string role)
            {
                Roles.Add(role);
            }

            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
            { }

            public override void SwitchBack()
            {
                Roles.Add("<switch>");
            }

            protected override void Dispose(bool disposing)
            { }

            public override void DisposeSubscriptions<T>()
            { }
        }

        private sealed class TestGlobalStateMatrix : GlobalStateMatrix
        {
            public override Task Init(ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master)
            {
                GlobalMatrix = BuildMatrices();
                return Task.CompletedTask;
            }

            private static Dictionary<WorkflowPhases, StateMatrix> BuildMatrices()
            {
                return new Dictionary<WorkflowPhases, StateMatrix>
                {
                    [WorkflowPhases.request] = BuildMatrix(8),
                    [WorkflowPhases.approval] = BuildMatrix(8),
                    [WorkflowPhases.planning] = BuildMatrix(8),
                    [WorkflowPhases.verification] = BuildMatrix(8),
                    [WorkflowPhases.implementation] = BuildMatrix(8),
                    [WorkflowPhases.review] = BuildMatrix(8),
                    [WorkflowPhases.recertification] = BuildMatrix(8)
                };
            }

            private static StateMatrix BuildMatrix(int lowestEndState)
            {
                return new StateMatrix
                {
                    Matrix = new Dictionary<int, List<int>>(),
                    DerivedStates = new Dictionary<int, int>(),
                    LowestInputState = 1,
                    LowestStartedState = 1,
                    LowestEndState = lowestEndState,
                    Active = true,
                    StateVisibilityGroupIds = new Dictionary<int, List<int>>(),
                    ExclusiveVisibilityGroupIds = new HashSet<int>(),
                    PhaseActive = new Dictionary<WorkflowPhases, bool>
                    {
                        [WorkflowPhases.request] = true,
                        [WorkflowPhases.approval] = true,
                        [WorkflowPhases.planning] = true,
                        [WorkflowPhases.verification] = true,
                        [WorkflowPhases.implementation] = true,
                        [WorkflowPhases.review] = true,
                        [WorkflowPhases.recertification] = true
                    }
                };
            }
        }

        [Test]
        public void WorkflowController_GetTicketId_UsesObjectIdForTicketScopeWhenTicketIdMissing()
        {
            WorkflowActionParameters parameters = new()
            {
                Scope = WfObjectScopes.Ticket.ToString(),
                ObjectId = 42,
                TicketId = 0
            };

            long ticketId = InvokePrivateStatic<long>(typeof(WorkflowController), "GetTicketId", parameters, WfObjectScopes.Ticket);

            Assert.That(ticketId, Is.EqualTo(42));
        }

        [Test]
        public void WorkflowController_ResolveActionContext_ReturnsRequestTaskOwnerAndTicketId()
        {
            FwoOwner owner = new() { Id = 7, Name = "App" };
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 42,
                Owners = [new FwoOwnerDataHelper { Owner = owner }]
            };
            WfTicket ticket = new()
            {
                Id = 42,
                Tasks = [reqTask]
            };
            WorkflowActionParameters parameters = new()
            {
                ObjectId = reqTask.Id
            };

            object context = InvokePrivateStatic<object>(typeof(WorkflowController), "ResolveActionContext", new WfHandler(), ticket, parameters, WfObjectScopes.RequestTask);

            Assert.Multiple(() =>
            {
                Assert.That(GetTupleItem<WfStatefulObject?>(context, "Item1"), Is.SameAs(reqTask));
                Assert.That(GetTupleItem<FwoOwner?>(context, "Item2"), Is.SameAs(owner));
                Assert.That(GetTupleItem<long?>(context, "Item3"), Is.EqualTo(42));
                Assert.That(GetTupleItem<string?>(context, "Item4"), Is.Null);
            });
        }

        [Test]
        public void WorkflowController_ResolveActionContext_ReturnsImplementationTaskOwnerAndTicketId()
        {
            FwoOwner owner = new() { Id = 7, Name = "App" };
            WfImplTask implTask = new() { Id = 21, TicketId = 0 };
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 42,
                Owners = [new FwoOwnerDataHelper { Owner = owner }],
                ImplementationTasks = [implTask]
            };
            WfTicket ticket = new()
            {
                Id = 42,
                Tasks = [reqTask]
            };
            WorkflowActionParameters parameters = new()
            {
                ObjectId = implTask.Id
            };

            object context = InvokePrivateStatic<object>(typeof(WorkflowController), "ResolveActionContext", new WfHandler(), ticket, parameters, WfObjectScopes.ImplementationTask);

            Assert.Multiple(() =>
            {
                Assert.That(GetTupleItem<WfStatefulObject?>(context, "Item1"), Is.SameAs(implTask));
                Assert.That(GetTupleItem<FwoOwner?>(context, "Item2"), Is.SameAs(owner));
                Assert.That(GetTupleItem<long?>(context, "Item3"), Is.EqualTo(42));
                Assert.That(implTask.TicketId, Is.EqualTo(42));
            });
        }

        [Test]
        public void WorkflowController_ResolveActionContext_ReturnsApprovalAndTicketId()
        {
            FwoOwner owner = new() { Id = 7, Name = "App" };
            WfApproval approval = new() { Id = 31 };
            WfReqTask reqTask = new()
            {
                Id = 11,
                TicketId = 42,
                Owners = [new FwoOwnerDataHelper { Owner = owner }],
                Approvals = [approval]
            };
            WfTicket ticket = new()
            {
                Id = 42,
                Tasks = [reqTask]
            };
            WorkflowActionParameters parameters = new()
            {
                ObjectId = approval.Id
            };

            object context = InvokePrivateStatic<object>(typeof(WorkflowController), "ResolveActionContext", new WfHandler(), ticket, parameters, WfObjectScopes.Approval);

            Assert.Multiple(() =>
            {
                Assert.That(GetTupleItem<WfStatefulObject?>(context, "Item1"), Is.SameAs(approval));
                Assert.That(GetTupleItem<FwoOwner?>(context, "Item2"), Is.SameAs(owner));
                Assert.That(GetTupleItem<long?>(context, "Item3"), Is.EqualTo(42));
            });
        }

        [Test]
        public void WorkflowController_ResolveActionContext_ReturnsNullForMissingObject()
        {
            WorkflowActionParameters parameters = new()
            {
                ObjectId = 999
            };

            object context = InvokePrivateStatic<object>(typeof(WorkflowController), "ResolveActionContext", new WfHandler(), new WfTicket(), parameters, WfObjectScopes.RequestTask);

            Assert.Multiple(() =>
            {
                Assert.That(GetTupleItem<WfStatefulObject?>(context, "Item1"), Is.Null);
                Assert.That(GetTupleItem<FwoOwner?>(context, "Item2"), Is.Null);
                Assert.That(GetTupleItem<long?>(context, "Item3"), Is.Null);
            });
        }

        [Test]
        public void WorkflowController_MarkStateChanged_RestoresOriginalChangeTracking()
        {
            WfTicket ticket = new() { StateId = 5 };
            ticket.ResetStateChanged();

            InvokePrivateStatic<object?>(typeof(WorkflowController), "MarkStateChanged", ticket, 5, 8);

            Assert.Multiple(() =>
            {
                Assert.That(ticket.StateId, Is.EqualTo(8));
                Assert.That(ticket.StateChanged(), Is.True);
                Assert.That(ticket.ChangedFrom(), Is.EqualTo(5));
            });
        }

        [Test]
        public async Task WorkflowController_ExecuteActionsRejectsInvalidScopeBeforeMiddlewareApi()
        {
            WorkflowController controller = CreateWorkflowController(PrincipalWithRoles(Roles.Admin));

            WorkflowActionResult result = await controller.ExecuteActions(new WorkflowActionParameters
            {
                Scope = "invalid",
                Phase = WorkflowPhases.request.ToString()
            });

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("Invalid scope"));
            });
        }

        [Test]
        public async Task WorkflowController_ExecuteActionsRejectsInvalidPhaseBeforeMiddlewareApi()
        {
            WorkflowController controller = CreateWorkflowController(PrincipalWithRoles(Roles.Admin));

            WorkflowActionResult result = await controller.ExecuteActions(new WorkflowActionParameters
            {
                Scope = WfObjectScopes.Ticket.ToString(),
                Phase = "invalid"
            });

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("Invalid workflow phase"));
            });
        }

        [Test]
        public async Task WorkflowController_ExecuteActionsRejectsPhaseNotAvailableInExecutionMode()
        {
            WorkflowController controller = CreateWorkflowController(PrincipalWithRoles(Roles.Auditor, Roles.Approver));

            WorkflowActionResult result = await controller.ExecuteActions(new WorkflowActionParameters
            {
                Scope = WfObjectScopes.Ticket.ToString(),
                Phase = WorkflowPhases.approval.ToString(),
                ExecutionMode = Roles.Auditor
            });

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("not authorized"));
            });
        }

        [Test]
        public async Task WorkflowController_ExecuteActions_ReturnsErrorWhenApiServerUriIsMissing()
        {
            string? previousApiServerUri = GetApiServerUri();
            SetApiServerUri(null);
            try
            {
                WorkflowController controller = CreateWorkflowController(PrincipalWithRoles(Roles.Admin));

                WorkflowActionResult result = await controller.ExecuteActions(new WorkflowActionParameters
                {
                    Scope = WfObjectScopes.Ticket.ToString(),
                    Phase = WorkflowPhases.request.ToString()
                });

                Assert.Multiple(() =>
                {
                    Assert.That(result.Success, Is.False);
                    Assert.That(result.ErrorMessage, Does.Contain("necessary config value"));
                });
            }
            finally
            {
                SetApiServerUri(previousApiServerUri);
            }
        }

        [Test]
        public async Task WorkflowController_ExecuteActionsInMiddlewareContext_CompletesTicketStateChange()
        {
            WorkflowController controller = CreateWorkflowController(PrincipalWithRoles(Roles.Admin));
            WorkflowExecutionApiConn apiConnection = new()
            {
                States = []
            };
            apiConnection.Ticket = new WfTicket
            {
                Id = 42,
                StateId = 8,
                Requester = new UiUser { Dn = "uid=requester,dc=fworch,dc=internal" }
            };
            WorkflowActionParameters parameters = new()
            {
                Scope = WfObjectScopes.Ticket.ToString(),
                Phase = WorkflowPhases.request.ToString(),
                ExecutionMode = Roles.Admin,
                OldStateId = 5,
                NewStateId = 8
            };
            WorkflowActionResult result = new();
            Func<GlobalStateMatrix> previousFactory = GlobalStateMatrix.Factory;
            GlobalStateMatrix.Factory = () => new TestGlobalStateMatrix();

            try
            {
                WorkflowActionResult executed = await InvokePrivateAsync<WorkflowActionResult>(controller, "ExecuteActionsInMiddlewareContext",
                    apiConnection, parameters, WfObjectScopes.Ticket, WorkflowPhases.request, 42L, result);

                Assert.Multiple(() =>
                {
                    Assert.That(executed.Success, Is.True);
                    Assert.That(executed.ErrorMessage, Is.Empty);
                    Assert.That(executed.Messages, Is.Empty);
                    Assert.That(apiConnection.Queries, Has.Some.EqualTo(RequestQueries.getStates));
                    Assert.That(apiConnection.Queries, Has.Some.EqualTo(RequestQueries.getTicketById));
                    Assert.That(apiConnection.Roles, Does.Contain(Roles.MiddlewareServer));
                });
            }
            finally
            {
                GlobalStateMatrix.Factory = previousFactory;
            }
        }

        [Test]
        public void WorkflowController_InitWorkflowHandler_ReturnsWarningWhenInitializationFails()
        {
            WfHandler handler = new();
            WorkflowActionResult result = new();

            bool initialized = InvokePrivateStaticAsync<bool>(typeof(WorkflowController), "InitWorkflowHandler", handler, result).GetAwaiter().GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(initialized, Is.False);
                Assert.That(result.ErrorMessage, Is.EqualTo("Workflow handler initialization failed."));
            });
        }

        [Test]
        public void WorkflowController_AddWorkflowMessage_UsesExceptionMessageWhenTextMissing()
        {
            WorkflowActionResult result = new();

            InvokePrivateStatic<object?>(typeof(WorkflowController), "AddWorkflowMessage", result,
                new InvalidOperationException("boom"), "Title", " ", true);

            Assert.Multiple(() =>
            {
                Assert.That(result.Messages, Has.Count.EqualTo(1));
                Assert.That(result.Messages[0].Title, Is.EqualTo("Title"));
                Assert.That(result.Messages[0].Message, Is.EqualTo("boom"));
                Assert.That(result.Messages[0].ErrorFlag, Is.True);
            });
        }

        [Test]
        public void WorkflowController_CallerCanExecutePhase_RequiresMatchingWorkflowRole()
        {
            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanExecutePhase", PrincipalWithRoles(Roles.Requester), "", WorkflowPhases.request), Is.True);
                Assert.That(InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanExecutePhase", PrincipalWithRoles(Roles.Requester), "", WorkflowPhases.approval), Is.False);
                Assert.That(InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanExecutePhase", PrincipalWithRoles(Roles.Modeller), "", WorkflowPhases.request), Is.False);
                Assert.That(InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanExecutePhase", PrincipalWithRoles(Roles.Admin), "", WorkflowPhases.review), Is.True);
                Assert.That(InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanExecutePhase", PrincipalWithRoles(Roles.Admin, Roles.Requester), "", WorkflowPhases.review), Is.False);
                Assert.That(InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanExecutePhase",
                    PrincipalWithRoles(Roles.Admin, Roles.Requester), Roles.Admin, WorkflowPhases.review), Is.True);
                Assert.That(InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanExecutePhase",
                    PrincipalWithRoles(Roles.Admin, Roles.Requester), GlobalConst.kUserRolesSelection, WorkflowPhases.review), Is.False);
                Assert.That(InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanExecutePhase",
                    PrincipalWithRoles(Roles.Auditor, Roles.Approver), Roles.Auditor, WorkflowPhases.approval), Is.False);
            });
        }

        [Test]
        public void WorkflowController_TryParseScope_RejectsNoneAndInvalidScope()
        {
            WorkflowActionResult noneResult = new();
            WorkflowActionParameters noneParameters = new() { Scope = WfObjectScopes.None.ToString() };
            object?[] noneArgs = [noneParameters, noneResult, WfObjectScopes.Ticket];
            WorkflowActionResult invalidResult = new();
            WorkflowActionParameters invalidParameters = new() { Scope = "invalid" };
            object?[] invalidArgs = [invalidParameters, invalidResult, WfObjectScopes.Ticket];

            bool noneParsed = InvokePrivateStaticWithRef<bool>(typeof(WorkflowController), "TryParseScope", noneArgs);
            bool invalidParsed = InvokePrivateStaticWithRef<bool>(typeof(WorkflowController), "TryParseScope", invalidArgs);

            Assert.Multiple(() =>
            {
                Assert.That(noneParsed, Is.False);
                Assert.That(noneResult.ErrorMessage, Does.Contain("Invalid scope"));
                Assert.That(invalidParsed, Is.False);
                Assert.That(invalidResult.ErrorMessage, Does.Contain("Invalid scope"));
            });
        }

        [Test]
        public void WorkflowController_TryParsePhase_RejectsInvalidPhase()
        {
            WorkflowActionParameters parameters = new() { Phase = "invalid" };
            WorkflowActionResult result = new();
            object?[] args = [parameters, result, WorkflowPhases.request];

            bool parsed = InvokePrivateStaticWithRef<bool>(typeof(WorkflowController), "TryParsePhase", args);

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("Invalid workflow phase"));
            });
        }

        [Test]
        public void WorkflowController_ValidateOfferedAction_UsesOldStateForLocallyChangedObject()
        {
            WfStateAction action = new()
            {
                Id = 1,
                Event = StateActionEvents.OfferButton.ToString(),
                Scope = WfObjectScopes.ImplementationTask.ToString(),
                TaskType = WfTaskType.access.ToString()
            };
            WfHandler handler = new()
            {
                Phase = WorkflowPhases.implementation,
                ActionHandler = new ActionHandler(new SimulatedApiConnection(), new WfHandler())
            };
            SetPrivateField(handler.ActionHandler, "states", new List<WfState>
            {
                new() { Id = 210, Actions = [new WfStateActionDataHelper { Action = action }] }
            });
            WfImplTask persistedTask = new() { StateId = 210, TaskType = WfTaskType.access.ToString() };
            WorkflowActionParameters parameters = new()
            {
                ActionId = action.Id,
                OldStateId = 210,
                NewStateId = 249
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidateOfferedAction",
                handler, parameters, WfObjectScopes.ImplementationTask, WorkflowPhases.implementation, persistedTask, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.True);
                Assert.That(result.ErrorMessage, Is.Null.Or.Empty);
            });
        }

        [Test]
        public void WorkflowController_ValidateOfferedAction_RejectsWhenActionIsNotOffered()
        {
            WfStateAction offeredAction = new()
            {
                Id = 2,
                Event = StateActionEvents.OfferButton.ToString(),
                Scope = WfObjectScopes.ImplementationTask.ToString(),
                TaskType = WfTaskType.access.ToString()
            };
            WfHandler handler = new()
            {
                Phase = WorkflowPhases.implementation,
                ActionHandler = new ActionHandler(new SimulatedApiConnection(), new WfHandler())
            };
            SetPrivateField(handler.ActionHandler, "states", new List<WfState>
            {
                new() { Id = 210, Actions = [new WfStateActionDataHelper { Action = offeredAction }] }
            });
            WfImplTask persistedTask = new() { StateId = 210, TaskType = WfTaskType.access.ToString() };
            WorkflowActionParameters parameters = new()
            {
                ActionId = 999,
                OldStateId = 210,
                NewStateId = 210
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidateOfferedAction",
                handler, parameters, WfObjectScopes.ImplementationTask, WorkflowPhases.implementation, persistedTask, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("not offered"));
            });
        }

        [Test]
        public void WorkflowController_ValidateOfferedAction_RejectsWhenPersistedStateDiffersFromOldState()
        {
            WfHandler handler = new()
            {
                Phase = WorkflowPhases.implementation,
                ActionHandler = new ActionHandler(new SimulatedApiConnection(), new WfHandler())
            };
            WfImplTask persistedTask = new() { StateId = 211, TaskType = WfTaskType.access.ToString() };
            WorkflowActionParameters parameters = new()
            {
                ActionId = 1,
                OldStateId = 210,
                NewStateId = 249
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidateOfferedAction",
                handler, parameters, WfObjectScopes.ImplementationTask, WorkflowPhases.implementation, persistedTask, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("not 210"));
            });
        }

        [Test]
        public void WorkflowController_ValidatePersistedStateTransition_AllowsPersistedTransition()
        {
            WfImplTask persistedTask = new() { StateId = 630 };
            WorkflowActionParameters parameters = new()
            {
                OldStateId = 0,
                NewStateId = 630
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidatePersistedStateTransition",
                parameters, persistedTask, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.True);
                Assert.That(result.ErrorMessage, Is.Null.Or.Empty);
            });
        }

        [Test]
        public void WorkflowController_ValidatePersistedStateTransition_RejectsNoStateChange()
        {
            WfTicket persistedTicket = new() { StateId = 5 };
            WorkflowActionParameters parameters = new()
            {
                OldStateId = 5,
                NewStateId = 5
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidatePersistedStateTransition",
                parameters, persistedTicket, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("no state change"));
            });
        }

        [Test]
        public void WorkflowController_ValidatePersistedStateTransition_AllowsPersistedAdminTransitionInUserRolesMode()
        {
            WfImplTask persistedTask = new() { StateId = 630 };
            WorkflowActionParameters parameters = new()
            {
                OldStateId = 0,
                NewStateId = 630,
                ExecutionMode = GlobalConst.kUserRolesSelection
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidatePersistedStateTransition",
                parameters, persistedTask, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.True);
                Assert.That(result.ErrorMessage, Is.Null.Or.Empty);
            });
        }

        [Test]
        public void WorkflowController_ValidatePersistedStateTransition_AllowsAlreadyPersistedTransitionForActions()
        {
            WfImplTask persistedTask = new() { StateId = 630 };
            WorkflowActionParameters parameters = new()
            {
                OldStateId = 0,
                NewStateId = 630
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidatePersistedStateTransition",
                parameters, persistedTask, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.True);
                Assert.That(result.ErrorMessage, Is.Null.Or.Empty);
            });
        }

        [Test]
        public void WorkflowController_ValidatePersistedStateTransition_AllowsCreatedObjectInitialState()
        {
            WfTicket persistedTicket = new() { StateId = 1 };
            WorkflowActionParameters parameters = new()
            {
                OldStateId = 0,
                NewStateId = 1,
                StateChangedByCreation = true
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidatePersistedStateTransition",
                parameters, persistedTicket, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.True);
                Assert.That(result.ErrorMessage, Is.Null.Or.Empty);
            });
        }

        [Test]
        public void WorkflowController_CallerCanAccessTicket_RequiresOwnerClaimWhenOwnerBased()
        {
            SimulatedUserConfig userConfig = new() { ReqOwnerBased = true };
            WfTicket ticket = new()
            {
                Id = 42,
                Tasks =
                [
                    new()
                    {
                        Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 7 } }]
                    }
                ]
            };

            bool ownerAllowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessTicket",
                PrincipalWithRolesAndClaims([Roles.Approver], new Claim("x-hasura-editable-owners", "{7}")), "", userConfig, ticket);
            bool otherOwnerRejected = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessTicket",
                PrincipalWithRolesAndClaims([Roles.Approver], new Claim("x-hasura-editable-owners", "{8}")), "", userConfig, ticket);

            Assert.Multiple(() =>
            {
                Assert.That(ownerAllowed, Is.True);
                Assert.That(otherOwnerRejected, Is.False);
            });
        }

        [Test]
        public void WorkflowController_CallerCanAccessTicket_AllowsRequesterByIdOrDn()
        {
            SimulatedUserConfig userConfig = new() { ReqOwnerBased = true };
            WfTicket ticket = new()
            {
                Id = 42,
                Requester = new UiUser { DbId = 5, Dn = "uid=requester,dc=fworch,dc=internal" }
            };

            bool byIdAllowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessTicket",
                PrincipalWithRolesAndClaims([Roles.Requester], new Claim("x-hasura-user-id", "5")), "", userConfig, ticket);
            bool byDnAllowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessTicket",
                PrincipalWithRolesAndClaims([Roles.Requester], new Claim("x-hasura-uuid", "UID=REQUESTER,dc=fworch,dc=internal")), "", userConfig, ticket);

            Assert.Multiple(() =>
            {
                Assert.That(byIdAllowed, Is.True);
                Assert.That(byDnAllowed, Is.True);
            });
        }

        [Test]
        public void WorkflowController_CallerCanAccessTicket_DoesNotUseAdminInUserRolesMode()
        {
            SimulatedUserConfig userConfig = new() { ReqOwnerBased = true };
            WfTicket ticket = new()
            {
                Id = 42,
                Tasks =
                [
                    new()
                    {
                        Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 7 } }]
                    }
                ]
            };

            bool allowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessTicket",
                PrincipalWithRoles(Roles.Admin, Roles.Requester), GlobalConst.kUserRolesSelection, userConfig, ticket);

            Assert.That(allowed, Is.False);
        }

        [Test]
        public void WorkflowController_CallerCanAccessTicket_AllowsTaskRequestingOwnerClaim()
        {
            SimulatedUserConfig userConfig = new() { ReqOwnerBased = true };
            WfReqTask reqTask = new();
            reqTask.SetAddInfo(AdditionalInfoKeys.ReqOwner, "9");
            WfTicket ticket = new()
            {
                Id = 42,
                Tasks = [reqTask]
            };

            bool allowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessTicket",
                PrincipalWithRolesAndClaims([Roles.Approver], new Claim("x-hasura-editable-owners", "{9}")), "", userConfig, ticket);

            Assert.That(allowed, Is.True);
        }

        [Test]
        public void WorkflowController_CallerCanAccessTicket_AllowsAdminAndWhenOwnerBasedIsDisabled()
        {
            SimulatedUserConfig userConfig = new()
            {
                ReqOwnerBased = false
            };
            WfTicket ticket = new()
            {
                Id = 42,
                Tasks =
                [
                    new()
                    {
                        Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 7 } }]
                    }
                ]
            };

            bool adminAllowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessTicket",
                PrincipalWithRoles(Roles.Admin), "", userConfig, ticket);
            bool fwAdminAllowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessTicket",
                PrincipalWithRoles(Roles.FwAdmin), Roles.Admin, userConfig, ticket);
            bool unrestrictedAllowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessTicket",
                PrincipalWithRoles(Roles.Approver), "", userConfig, ticket);

            Assert.Multiple(() =>
            {
                Assert.That(adminAllowed, Is.True);
                Assert.That(fwAdminAllowed, Is.True);
                Assert.That(unrestrictedAllowed, Is.True);
            });
        }

        [Test]
        public void WorkflowController_CallerCanAccessVisibility_RequiresMatchingVisibilityGroup()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig
                {
                    ReqVisibilityBased = true
                },
                MasterStateMatrix = new StateMatrix
                {
                    StateVisibilityGroupIds = new Dictionary<int, List<int>>
                    {
                        [10] = [7]
                    }
                },
                ActStateMatrix = new StateMatrix
                {
                    StateVisibilityGroupIds = new Dictionary<int, List<int>>
                    {
                        [20] = [8]
                    }
                }
            };

            bool allowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessVisibility",
                PrincipalWithRolesAndClaims([Roles.Approver], new Claim("x-hasura-workflow-visibility-groups", "{7}")), handler, WfObjectScopes.Ticket, new WfTicket { StateId = 10 });
            bool rejected = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessVisibility",
                PrincipalWithRolesAndClaims([Roles.Approver], new Claim("x-hasura-workflow-visibility-groups", "{9}")), handler, WfObjectScopes.Ticket, new WfTicket { StateId = 10 });

            Assert.Multiple(() =>
            {
                Assert.That(allowed, Is.True);
                Assert.That(rejected, Is.False);
            });
        }

        [Test]
        public void WorkflowController_CallerCanAccessVisibility_DeniesUntaggedObjectsForExclusiveMembers()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig
                {
                    ReqVisibilityBased = true
                },
                MasterStateMatrix = new StateMatrix
                {
                    ExclusiveVisibilityGroupIds = [7]
                }
            };

            bool allowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessVisibility",
                PrincipalWithRolesAndClaims([Roles.Approver], new Claim("x-hasura-workflow-visibility-groups", "{7}")), handler, WfObjectScopes.Ticket, new WfTicket { StateId = 10 });

            Assert.That(allowed, Is.False);
        }

        [Test]
        public void WorkflowController_CallerCanAccessVisibility_SkipsChecksWhenFeatureIsDisabled()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig
                {
                    ReqVisibilityBased = false
                },
                MasterStateMatrix = new StateMatrix
                {
                    StateVisibilityGroupIds = new Dictionary<int, List<int>>
                    {
                        [10] = [7]
                    }
                }
            };

            bool allowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessVisibility",
                PrincipalWithRolesAndClaims([Roles.Approver], new Claim("x-hasura-workflow-visibility-groups", "{9}")), handler, WfObjectScopes.Ticket, new WfTicket { StateId = 10 });

            Assert.That(allowed, Is.True);
        }

        [Test]
        public void WorkflowController_CallerCanAccessVisibility_AllowsExplicitApprovalGroupAssignment()
        {
            string approvalGroupDn = "cn=approvers,ou=groups,dc=fworch,dc=internal";
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig
                {
                    ReqVisibilityBased = true
                },
                MasterStateMatrix = new StateMatrix
                {
                    StateVisibilityGroupIds = new Dictionary<int, List<int>>
                    {
                        [10] = [7]
                    }
                }
            };
            WfApproval approval = new()
            {
                Id = 31,
                StateId = 10,
                ApproverGroup = approvalGroupDn
            };

            bool allowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessVisibility",
                PrincipalWithRolesAndClaims(
                    [Roles.Approver],
                    new Claim("x-hasura-groups", System.Text.Json.JsonSerializer.Serialize(new List<string> { approvalGroupDn }))),
                handler, WfObjectScopes.Approval, approval);

            Assert.That(allowed, Is.True);
        }

        [Test]
        public void WorkflowController_CallerCanAccessVisibility_AllowsExplicitHandlerAssignment()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig
                {
                    ReqVisibilityBased = true
                },
                MasterStateMatrix = new StateMatrix
                {
                    StateVisibilityGroupIds = new Dictionary<int, List<int>>
                    {
                        [10] = [7]
                    }
                }
            };
            WfTicket ticket = new()
            {
                StateId = 10,
                CurrentHandler = new UiUser { DbId = 7, Dn = "uid=handler,dc=fworch,dc=internal" }
            };

            bool allowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessVisibility",
                PrincipalWithRolesAndClaims([Roles.Approver], new Claim("x-hasura-user-id", "7")), handler, WfObjectScopes.Ticket, ticket);

            Assert.That(allowed, Is.True);
        }

        [Test]
        public void WorkflowController_CallerCanAccessVisibility_AllowsExplicitStatefulGroupAssignment()
        {
            string assignedGroupDn = "cn=assignees,ou=groups,dc=fworch,dc=internal";
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig
                {
                    ReqVisibilityBased = true
                },
                MasterStateMatrix = new StateMatrix
                {
                    StateVisibilityGroupIds = new Dictionary<int, List<int>>
                    {
                        [10] = [7]
                    }
                }
            };
            WfTicket ticket = new()
            {
                StateId = 10,
                AssignedGroup = assignedGroupDn
            };

            bool allowed = InvokePrivateStatic<bool>(typeof(WorkflowController), "CallerCanAccessVisibility",
                PrincipalWithRolesAndClaims([Roles.Approver], new Claim("x-hasura-groups", System.Text.Json.JsonSerializer.Serialize(new List<string> { assignedGroupDn }))),
                handler, WfObjectScopes.Ticket, ticket);

            Assert.That(allowed, Is.True);
        }

        [Test]
        public async Task WorkflowRecipientResolver_ResolveUserDns_ReturnsDistinctDirectUserDns()
        {
            WorkflowRecipientResolver resolver = new(new RecipientResolverApiConn(), []);

            List<string> resolvedDns = await resolver.ResolveUserDns([
                "uid=user,ou=users,dc=test",
                "UID=USER,ou=users,dc=test",
                ""
            ]);

            Assert.That(resolvedDns, Is.EqualTo(kExpectedResolvedUserDns));
        }

        [Test]
        public void WorkflowController_GetClaimHelpers_ParseIdsValuesAndJsonClaims()
        {
            ClaimsPrincipal user = PrincipalWithRolesAndClaims(
                [Roles.Approver],
                new Claim("x-hasura-user-id", "17"),
                new Claim("x-hasura-uuid", "uid=test,dc=fworch,dc=internal"),
                new Claim("x-hasura-workflow-visibility-groups", "{ 2, 4, x, 9 }"),
                new Claim("x-hasura-groups", "[\"cn=a,dc=fworch,dc=internal\", \"cn=b,dc=fworch,dc=internal\"]"));

            HashSet<int> ids = InvokePrivateStatic<HashSet<int>>(typeof(WorkflowController), "GetClaimIds", user, "x-hasura-workflow-visibility-groups");
            int? userId = InvokePrivateStatic<int?>(typeof(WorkflowController), "GetClaimInt", user, "x-hasura-user-id");
            List<string> groups = InvokePrivateStatic<List<string>>(typeof(WorkflowController), "GetClaimStrings", user, "x-hasura-groups");
            string? uuid = InvokePrivateStatic<string?>(typeof(WorkflowController), "GetClaimValue", user, "x-hasura-uuid");

            Assert.Multiple(() =>
            {
                Assert.That(ids, Is.EquivalentTo([2, 4, 9]));
                Assert.That(userId, Is.EqualTo(17));
                Assert.That(groups, Is.EqualTo(kExpectedParsedGroups));
                Assert.That(uuid, Is.EqualTo("uid=test,dc=fworch,dc=internal"));
            });
        }

        [Test]
        public void WorkflowController_GetClaimStrings_ReturnsEmptyListForInvalidJson()
        {
            ClaimsPrincipal user = PrincipalWithRolesAndClaims([Roles.Approver], new Claim("x-hasura-groups", "not-json"));

            List<string> groups = InvokePrivateStatic<List<string>>(typeof(WorkflowController), "GetClaimStrings", user, "x-hasura-groups");

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public void WorkflowController_GetTicketId_UsesTicketIdForNonTicketScope()
        {
            WorkflowActionParameters parameters = new()
            {
                Scope = WfObjectScopes.RequestTask.ToString(),
                ObjectId = 42,
                TicketId = 17
            };

            long ticketId = InvokePrivateStatic<long>(typeof(WorkflowController), "GetTicketId", parameters, WfObjectScopes.RequestTask);

            Assert.That(ticketId, Is.EqualTo(17));
        }

        [Test]
        public void WorkflowController_GetTicketId_UsesExplicitTicketIdForTicketScope()
        {
            WorkflowActionParameters parameters = new()
            {
                Scope = WfObjectScopes.Ticket.ToString(),
                ObjectId = 42,
                TicketId = 99
            };

            long ticketId = InvokePrivateStatic<long>(typeof(WorkflowController), "GetTicketId", parameters, WfObjectScopes.Ticket);

            Assert.That(ticketId, Is.EqualTo(99));
        }

        [Test]
        public void WorkflowController_ResolveActionContext_ReturnsTicketScopeTuple()
        {
            WfTicket ticket = new()
            {
                Id = 42,
                Requester = new UiUser { Dn = "uid=requester,dc=fworch,dc=internal" }
            };
            WorkflowActionParameters parameters = new()
            {
                TicketId = 42
            };

            object context = InvokePrivateStatic<object>(typeof(WorkflowController), "ResolveActionContext", new WfHandler(), ticket, parameters, WfObjectScopes.Ticket);

            Assert.Multiple(() =>
            {
                Assert.That(GetTupleItem<WfStatefulObject?>(context, "Item1"), Is.SameAs(ticket));
                Assert.That(GetTupleItem<FwoOwner?>(context, "Item2"), Is.Null);
                Assert.That(GetTupleItem<long?>(context, "Item3"), Is.EqualTo(42));
                Assert.That(GetTupleItem<string?>(context, "Item4"), Is.EqualTo("uid=requester,dc=fworch,dc=internal"));
            });
        }

        [Test]
        public async Task WorkflowRecipientResolver_ResolveUsers_UsesCachedUiUsersWithEmail()
        {
            RecipientResolverApiConn apiConn = new()
            {
                Users =
                [
                    new() { Dn = "uid=user,ou=users,dc=test", Email = "user@example.test" },
                    new() { Dn = "uid=other,ou=users,dc=test", Email = "other@example.test" }
                ]
            };
            WorkflowRecipientResolver resolver = new(apiConn, []);

            List<UiUser> users = await resolver.ResolveUsers(["uid=user,ou=users,dc=test"]);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(kGetUserEmailsQuery));
                Assert.That(users, Has.Count.EqualTo(1));
                Assert.That(users[0].Email, Is.EqualTo("user@example.test"));
            });
        }

        [Test]
        public async Task WorkflowRecipientResolver_ResolveUsers_ReturnsEmptyWhenDnsAreBlank()
        {
            RecipientResolverApiConn apiConn = new();
            WorkflowRecipientResolver resolver = new(apiConn, []);

            List<UiUser> users = await resolver.ResolveUsers(["", "   "]);

            Assert.Multiple(() =>
            {
                Assert.That(users, Is.Empty);
                Assert.That(apiConn.Queries, Is.Empty);
            });
        }

        [Test]
        public async Task WorkflowRecipientResolver_ResolveUsers_ReturnsCachedUserWithoutEmailWhenLdapLookupUnavailable()
        {
            RecipientResolverApiConn apiConn = new()
            {
                Users =
                [
                    new() { Dn = "uid=user,ou=users,dc=test", Name = "user" }
                ]
            };
            WorkflowRecipientResolver resolver = new(apiConn, []);

            List<UiUser> users = await resolver.ResolveUsers(["uid=user,ou=users,dc=test"]);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(kGetUserEmailsQuery));
                Assert.That(users, Has.Count.EqualTo(1));
                Assert.That(users[0].Dn, Is.EqualTo("uid=user,ou=users,dc=test"));
                Assert.That(users[0].Email, Is.Null);
            });
        }

        [Test]
        public async Task WorkflowRecipientResolver_ResolveUsers_ReturnsEmptyWhenDnCannotBeResolved()
        {
            RecipientResolverApiConn apiConn = new();
            WorkflowRecipientResolver resolver = new(apiConn, []);

            List<UiUser> users = await resolver.ResolveUsers(["uid=missing,ou=users,dc=test"]);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(kGetUserEmailsQuery));
                Assert.That(users, Is.Empty);
            });
        }

        [Test]
        public async Task WorkflowRecipientResolver_ResolveUserDns_UsesGroupHandlingLdapForDistinctDirectDns()
        {
            Ldap ldap = new()
            {
                GroupSearchPath = "ou=groups,dc=test",
                GroupWritePath = "ou=write,dc=test",
                UserSearchPath = ""
            };
            WorkflowRecipientResolver resolver = new(new RecipientResolverApiConn(), [ldap]);

            List<string> resolvedDns = await resolver.ResolveUserDns([
                "uid=user,ou=users,dc=test",
                "UID=USER,ou=users,dc=test",
                "cn=group,ou=groups,dc=test"
            ]);

            Assert.That(resolvedDns, Is.EqualTo(kExpectedResolvedUserDns));
        }

        [Test]
        public async Task WorkflowRecipientResolver_ResolveUserDns_IgnoresLdapsWithoutGroupHandling()
        {
            Ldap ldap = new()
            {
                UserSearchPath = "ou=users,dc=test"
            };
            WorkflowRecipientResolver resolver = new(new RecipientResolverApiConn(), [ldap]);

            List<string> resolvedDns = await resolver.ResolveUserDns([
                "uid=user,ou=users,dc=test",
                "cn=group,ou=groups,dc=test"
            ]);

            Assert.That(resolvedDns, Is.EqualTo([
                "uid=user,ou=users,dc=test",
                "cn=group,ou=groups,dc=test"
            ]));
        }

        [Test]
        public void ActionHandler_Constructor_UsesExplicitResolverAndPolicyChecker()
        {
            TestWorkflowRecipientResolver resolver = new();
            TestRequestedRulePolicyChecker policyChecker = new();
            ActionHandler handler = new(new RecipientResolverApiConn(), new WfHandler(), null, true, policyChecker, resolver);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<IWorkflowRecipientResolver?>(handler, "workflowRecipientResolver"), Is.SameAs(resolver));
                Assert.That(GetPrivateField<IRequestedRulePolicyChecker?>(handler, "requestedRulePolicyChecker"), Is.SameAs(policyChecker));
            });
        }

        [Test]
        public void WfHandler_MiddlewareConstructor_StoresWorkflowDependencies()
        {
            TestWorkflowRecipientResolver resolver = new();
            TestRequestedRulePolicyChecker policyChecker = new();
            WfHandler handler = new(new SimulatedUserConfig(), new RecipientResolverApiConn(), WorkflowPhases.approval, [], policyChecker, null, resolver);

            Assert.Multiple(() =>
            {
                Assert.That(handler.Phase, Is.EqualTo(WorkflowPhases.approval));
                Assert.That(handler.RequestedRulePolicyChecker, Is.SameAs(policyChecker));
                Assert.That(handler.WorkflowRecipientResolver, Is.SameAs(resolver));
            });
        }

        private static T InvokePrivateStatic<T>(Type type, string methodName, params object?[] parameters)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            return (T)method.Invoke(null, parameters)!;
        }

        private static async Task<T> InvokePrivateStaticAsync<T>(Type type, string methodName, params object?[] parameters)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            return await (Task<T>)method.Invoke(null, parameters)!;
        }

        private static T CreateEmptyQueryResult<T>()
        {
            Type resultType = typeof(T);
            if (resultType.IsArray)
            {
                return (T)(object)Array.CreateInstance(resultType.GetElementType()!, 0);
            }

            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return (T)Activator.CreateInstance(resultType)!;
            }

            return default!;
        }

        private static async Task<T> InvokePrivateAsync<T>(object instance, string methodName, params object?[] parameters)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            return await (Task<T>)method.Invoke(instance, parameters)!;
        }

        private static T InvokePrivateStaticWithRef<T>(Type type, string methodName, object?[] parameters)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            return (T)method.Invoke(null, parameters)!;
        }

        private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
        {
            ClaimsIdentity identity = new(roles.Select(role => new Claim(ClaimTypes.Role, role)), "test");
            return new ClaimsPrincipal(identity);
        }

        private static ClaimsPrincipal PrincipalWithRolesAndClaims(string[] roles, params Claim[] claims)
        {
            ClaimsIdentity identity = new(roles.Select(role => new Claim(ClaimTypes.Role, role)).Concat(claims), "test");
            return new ClaimsPrincipal(identity);
        }

        private static WorkflowController CreateWorkflowController(ClaimsPrincipal user)
        {
            RSA rsa = RSA.Create(2048);
            WorkflowController controller = new(new SimulatedGlobalConfig(), [], new JwtWriter(new RsaSecurityKey(rsa)), new TokenLifetimeProvider())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = user
                    }
                }
            };
            return controller;
        }

        private static string? GetApiServerUri()
        {
            Type configFileType = typeof(FWO.Config.File.ConfigFile);
            PropertyInfo dataProperty = configFileType.GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ConfigFile.Data not found.");
            object data = dataProperty.GetValue(null)!;
            return (string?)data.GetType().GetProperty("ApiServerUri", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(data);
        }

        private static void SetApiServerUri(string? apiServerUri)
        {
            Type configFileType = typeof(FWO.Config.File.ConfigFile);
            Type? configFileDataType = configFileType.GetNestedType("ConfigFileData", BindingFlags.NonPublic);
            object configData = Activator.CreateInstance(configFileDataType ?? throw new MissingMemberException(configFileType.FullName, "ConfigFileData"))
                ?? throw new InvalidOperationException("Could not create config file data.");
            configFileDataType.GetProperty("ApiServerUri", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(configData, apiServerUri);
            configFileType.GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)!
                .SetValue(null, configData);
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{fieldName} not found.");
            return (T)field.GetValue(instance)!;
        }

        private static void SetPrivateField(object instance, string fieldName, object? value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{fieldName} not found.");
            field.SetValue(instance, value);
        }

        private static T GetTupleItem<T>(object tuple, string itemName)
        {
            FieldInfo field = tuple.GetType().GetField(itemName)
                ?? throw new InvalidOperationException($"{itemName} not found.");
            return (T)field.GetValue(tuple)!;
        }
    }
}
