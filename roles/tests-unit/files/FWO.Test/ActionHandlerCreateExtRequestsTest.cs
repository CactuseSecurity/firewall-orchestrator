using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests that the state action "Create external requests" starts the provisioning of the ticket in the scope of
    /// the action, writes a single implementation task in the implementation task scope, and does nothing outside the
    /// middleware server.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ActionHandlerCreateExtRequestsTest
    {
        private const long kTicketId = 5;
        private const long kReqTaskId = 7;
        private const long kImplTaskId = 9;

        private static readonly List<(long TicketId, long? ScopeReqTaskId, bool ByButton)> kTicketScopeCall = [(kTicketId, null, false)];
        private static readonly List<(long TicketId, long? ScopeReqTaskId, bool ByButton)> kReqTaskScopeCall = [(kTicketId, kReqTaskId, false)];
        private static readonly List<(long TicketId, long? ScopeReqTaskId, bool ByButton)> kReqTaskButtonCall = [(kTicketId, kReqTaskId, true)];
        private static readonly List<(long TicketId, long ImplTaskId, bool ByButton)> kImplTaskButtonCall = [(kTicketId, kImplTaskId, true)];

        /// <summary>
        /// Records the started provisionings.
        /// </summary>
        private sealed class RecordingProvisioning(bool result = true) : IExternalRequestProvisioning
        {
            public List<(long TicketId, long? ScopeReqTaskId, bool ByButton)> Calls { get; } = [];

            public Task<bool> StartProvisioning(ApiConnection apiConnection, UserConfig userConfig, long ticketId, long? scopeReqTaskId, bool byButton = false)
            {
                Calls.Add((ticketId, scopeReqTaskId, byButton));
                return Task.FromResult(result);
            }

            public List<(long TicketId, long ImplTaskId, bool ByButton)> ImplTaskCalls { get; } = [];

            public Task<bool> ProvisionImplTask(ApiConnection apiConnection, UserConfig userConfig, long ticketId, long implTaskId, bool byButton = false)
            {
                ImplTaskCalls.Add((ticketId, implTaskId, byButton));
                return Task.FromResult(result);
            }
        }

        private static WfStateAction Action(WfObjectScopes scope, StateActionEvents actionEvent = StateActionEvents.OnSet)
        {
            return new WfStateAction
            {
                Name = "Create external requests",
                ActionType = nameof(StateActionTypes.CreateExtRequests),
                Scope = scope.ToString(),
                Event = actionEvent.ToString()
            };
        }

        private static (ActionHandler ActionHandler, WfTicket Ticket, WfReqTask ReqTask) Create(bool useInMwServer, IExternalRequestProvisioning? provisioning)
        {
            WfReqTask reqTask = new() { Id = kReqTaskId, TicketId = kTicketId, TaskType = WfTaskType.group_create.ToString() };
            WfTicket ticket = new() { Id = kTicketId, Tasks = { reqTask } };
            WfHandler wfHandler = new() { userConfig = new SimulatedUserConfig(), ActTicket = ticket };
            ActionHandler actionHandler = new(new InMemoryDeviceSpecificSettingsApiConnection(), wfHandler, null, useInMwServer)
            {
                ExternalRequestProvisioningOverride = provisioning
            };
            return (actionHandler, ticket, reqTask);
        }

        [Test]
        public async Task TicketScope_StartsTheProvisioningOfTheWholeTicket()
        {
            RecordingProvisioning provisioning = new();
            (ActionHandler actionHandler, WfTicket ticket, _) = Create(true, provisioning);

            await actionHandler.PerformAction(Action(WfObjectScopes.Ticket), ticket, WfObjectScopes.Ticket);

            Assert.That(provisioning.Calls, Is.EqualTo(kTicketScopeCall));
        }

        [Test]
        public async Task RequestTaskScope_StartsTheProvisioningOfTheTask()
        {
            RecordingProvisioning provisioning = new();
            (ActionHandler actionHandler, _, WfReqTask reqTask) = Create(true, provisioning);

            await actionHandler.CreateExtRequests(Action(WfObjectScopes.RequestTask), reqTask, WfObjectScopes.RequestTask);

            Assert.That(provisioning.Calls, Is.EqualTo(kReqTaskScopeCall));
        }

        [Test]
        public async Task OfferedAsButton_ReleasesTheScope()
        {
            RecordingProvisioning provisioning = new();
            (ActionHandler actionHandler, _, WfReqTask reqTask) = Create(true, provisioning);

            await actionHandler.CreateExtRequests(Action(WfObjectScopes.RequestTask, StateActionEvents.OfferButton), reqTask, WfObjectScopes.RequestTask);

            Assert.That(provisioning.Calls, Is.EqualTo(kReqTaskButtonCall));
        }

        [Test]
        public void FailedProvisioning_DoesNotAbortTheStateChange()
        {
            RecordingProvisioning provisioning = new(false);
            (ActionHandler actionHandler, WfTicket ticket, _) = Create(true, provisioning);

            Assert.DoesNotThrowAsync(() => actionHandler.CreateExtRequests(Action(WfObjectScopes.Ticket), ticket, WfObjectScopes.Ticket));
        }

        [Test]
        public void OutsideTheMiddlewareServer_DoesNothing()
        {
            (ActionHandler actionHandler, WfTicket ticket, _) = Create(false, null);

            Assert.DoesNotThrowAsync(() => actionHandler.CreateExtRequests(Action(WfObjectScopes.Ticket), ticket, WfObjectScopes.Ticket));
        }

        [Test]
        public async Task ImplementationTaskScope_WritesOnlyThisImplementationTask()
        {
            RecordingProvisioning provisioning = new();
            (ActionHandler actionHandler, _, WfReqTask reqTask) = Create(true, provisioning);
            WfImplTask implTask = new(reqTask) { Id = kImplTaskId };

            await actionHandler.CreateExtRequests(Action(WfObjectScopes.ImplementationTask, StateActionEvents.OfferButton), implTask, WfObjectScopes.ImplementationTask);

            Assert.Multiple(() =>
            {
                Assert.That(provisioning.ImplTaskCalls, Is.EqualTo(kImplTaskButtonCall));
                Assert.That(provisioning.Calls, Is.Empty);
            });
        }

        [Test]
        public async Task ImplementationTaskScope_WithoutTicket_StartsNothing()
        {
            RecordingProvisioning provisioning = new();
            (ActionHandler actionHandler, WfTicket ticket, _) = Create(true, provisioning);
            ticket.Id = 0;
            WfImplTask implTask = new() { Id = kImplTaskId };

            await actionHandler.CreateExtRequests(Action(WfObjectScopes.ImplementationTask), implTask, WfObjectScopes.ImplementationTask);

            Assert.That(provisioning.ImplTaskCalls, Is.Empty);
        }

        [Test]
        public void ImplementationTaskScope_OutsideTheMiddlewareServer_DoesNothing()
        {
            (ActionHandler actionHandler, _, WfReqTask reqTask) = Create(false, null);
            WfImplTask implTask = new(reqTask) { Id = kImplTaskId };

            Assert.DoesNotThrowAsync(() => actionHandler.CreateExtRequests(Action(WfObjectScopes.ImplementationTask), implTask, WfObjectScopes.ImplementationTask));
        }
    }
}
