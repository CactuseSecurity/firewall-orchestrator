using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class WorkflowMiddlewareUnitTest
    {
        private sealed class RecipientResolverApiConn : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<UiUser> Users { get; set; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null)
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

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{fieldName} not found.");
            return (T)field.GetValue(instance)!;
        }

        private static T GetTupleItem<T>(object tuple, string itemName)
        {
            FieldInfo field = tuple.GetType().GetField(itemName)
                ?? throw new InvalidOperationException($"{itemName} not found.");
            return (T)field.GetValue(tuple)!;
        }
    }
}
