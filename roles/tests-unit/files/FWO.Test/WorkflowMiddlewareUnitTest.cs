using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
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
        public void WorkflowController_ValidatePersistedStateTransition_AllowsAdminForcedTransition()
        {
            WfHandler handler = new()
            {
                ActStateMatrix = new StateMatrix
                {
                    Matrix = new() { [0] = [49] }
                }
            };
            WfImplTask persistedTask = new() { StateId = 630 };
            WorkflowActionParameters parameters = new()
            {
                OldStateId = 0,
                NewStateId = 630
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidatePersistedStateTransition",
                PrincipalWithRoles(Roles.Admin), handler, parameters, persistedTask, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.True);
                Assert.That(result.ErrorMessage, Is.Null.Or.Empty);
            });
        }

        [Test]
        public void WorkflowController_ValidatePersistedStateTransition_RejectsNoStateChange()
        {
            WfHandler handler = new();
            WfTicket persistedTicket = new() { StateId = 5 };
            WorkflowActionParameters parameters = new()
            {
                OldStateId = 5,
                NewStateId = 5
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidatePersistedStateTransition",
                PrincipalWithRoles(Roles.Admin), handler, parameters, persistedTicket, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("no state change"));
            });
        }

        [Test]
        public void WorkflowController_ValidatePersistedStateTransition_RejectsAdminForceWhenInUserRolesMode()
        {
            WfHandler handler = new()
            {
                ActStateMatrix = new StateMatrix
                {
                    Matrix = new() { [0] = [49] }
                }
            };
            WfImplTask persistedTask = new() { StateId = 630 };
            WorkflowActionParameters parameters = new()
            {
                OldStateId = 0,
                NewStateId = 630,
                ExecutionMode = GlobalConst.kUserRolesSelection
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidatePersistedStateTransition",
                PrincipalWithRoles(Roles.Admin, Roles.Requester), handler, parameters, persistedTask, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("not allowed"));
            });
        }

        [Test]
        public void WorkflowController_ValidatePersistedStateTransition_RejectsNonAdminUnconfiguredTransition()
        {
            WfHandler handler = new()
            {
                ActStateMatrix = new StateMatrix
                {
                    Matrix = new() { [0] = [49] }
                }
            };
            WfImplTask persistedTask = new() { StateId = 630 };
            WorkflowActionParameters parameters = new()
            {
                OldStateId = 0,
                NewStateId = 630
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidatePersistedStateTransition",
                PrincipalWithRoles(Roles.Requester), handler, parameters, persistedTask, result);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("not allowed"));
            });
        }

        [Test]
        public void WorkflowController_ValidatePersistedStateTransition_AllowsCreatedObjectInitialState()
        {
            WfHandler handler = new()
            {
                ActStateMatrix = new StateMatrix
                {
                    Matrix = new() { [0] = [49] }
                }
            };
            WfTicket persistedTicket = new() { StateId = 1 };
            WorkflowActionParameters parameters = new()
            {
                OldStateId = 0,
                NewStateId = 1,
                StateChangedByCreation = true
            };
            WorkflowActionResult result = new();

            bool valid = InvokePrivateStatic<bool>(typeof(WorkflowController), "ValidatePersistedStateTransition",
                PrincipalWithRoles(Roles.Requester), handler, parameters, persistedTicket, result);

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
        public async Task WorkflowRecipientResolver_ResolveUserDns_ReturnsDistinctDirectUserDns()
        {
            WorkflowRecipientResolver resolver = new(new RecipientResolverApiConn(), []);

            List<string> resolvedDns = await resolver.ResolveUserDns([
                "uid=user,ou=users,dc=test",
                "UID=USER,ou=users,dc=test",
                ""
            ]);

            Assert.That(resolvedDns, Is.EqualTo(new[] { "uid=user,ou=users,dc=test" }));
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
                Assert.That(apiConn.Queries, Is.EqualTo(new[] { AuthQueries.getUserEmails }));
                Assert.That(users, Has.Count.EqualTo(1));
                Assert.That(users[0].Email, Is.EqualTo("user@example.test"));
            });
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
            WorkflowController controller = new(new GlobalConfig(), [], new JwtWriter(new RsaSecurityKey(rsa)), new TokenLifetimeProvider())
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
