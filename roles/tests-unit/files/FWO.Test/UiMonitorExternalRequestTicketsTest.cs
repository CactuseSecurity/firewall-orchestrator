using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiMonitorExternalRequestTicketsTest
    {
        private static MethodInfo GetPrivateMethod(string name, params Type[] parameterTypes)
        {
            return typeof(MonitorExternalRequestTickets).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null)
                ?? throw new MissingMethodException(typeof(MonitorExternalRequestTickets).FullName, name);
        }

        private static T GetPrivateField<T>(MonitorExternalRequestTickets component, string fieldName)
        {
            FieldInfo? field = typeof(MonitorExternalRequestTickets).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(MonitorExternalRequestTickets).FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static MonitorExternalRequestTickets RenderComponent(MonitorExternalRequestTicketsApiConn apiConn)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<MonitorExternalRequestTickets>());
            return component.FindComponent<MonitorExternalRequestTickets>().Instance;
        }

        [Test]
        public void OnInitializedAsync_LoadsOwnersTicketsAndStateMatrix()
        {
            MonitorExternalRequestTicketsApiConn apiConn = new();
            MonitorExternalRequestTickets component = RenderComponent(apiConn);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.OwnerQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.OwnerTicketQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.OpenRequestQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.StateQueryCount, Is.EqualTo(1));
                Assert.That(apiConn.WorkflowConfigurationQueryCount, Is.GreaterThan(0));
                Assert.That(GetPrivateField<bool>(component, "InitComplete"), Is.True);
                Assert.That(GetPrivateField<List<FwoOwner>>(component, "allOwners"), Has.Count.EqualTo(2));
                Assert.That(GetPrivateField<FwoOwner>(component, "actOwner").Id, Is.EqualTo(2));
                Assert.That(GetPrivateField<List<OwnerTicket>>(component, "ownerTickets"), Has.Count.EqualTo(2));
                Assert.That(GetPrivateField<List<OwnerTicket>>(component, "ownerTickets")[0].Ticket.Id, Is.EqualTo(30));
                Assert.That(GetPrivateField<List<ExternalRequest>>(component, "relevantRequests"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<StateMatrix>(component, "MasterStateMatrix").LowestEndState, Is.EqualTo(50));
            });
        }

        [Test]
        public async Task InitData_RefreshesSelectedOwnerAndRequests()
        {
            MonitorExternalRequestTicketsApiConn apiConn = new();
            MonitorExternalRequestTickets component = RenderComponent(apiConn);
            FwoOwner alternateOwner = new()
            {
                Id = 77,
                Name = "Owner 77"
            };

            await (Task)GetPrivateMethod("InitData", typeof(FwoOwner)).Invoke(component, new object[] { alternateOwner })!;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<FwoOwner>(component, "actOwner").Id, Is.EqualTo(77));
                Assert.That(apiConn.OwnerTicketQueryCount, Is.EqualTo(2));
                Assert.That(apiConn.OpenRequestQueryCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void ReInitPossible_RespectsStateThresholdAndRelevantRequests()
        {
            MonitorExternalRequestTicketsApiConn apiConn = new();
            MonitorExternalRequestTickets component = RenderComponent(apiConn);

            bool possibleWithoutRequest = (bool)GetPrivateMethod("ReInitPossible", typeof(OwnerTicket)).Invoke(component, new object[]
            {
                CreateOwnerTicket(10, 49)
            })!;
            bool possibleWithRelevantRequest = (bool)GetPrivateMethod("ReInitPossible", typeof(OwnerTicket)).Invoke(component, new object[]
            {
                CreateOwnerTicket(30, 49)
            })!;
            bool possibleAtLimit = (bool)GetPrivateMethod("ReInitPossible", typeof(OwnerTicket)).Invoke(component, new object[]
            {
                CreateOwnerTicket(40, 50)
            })!;

            Assert.Multiple(() =>
            {
                Assert.That(possibleWithoutRequest, Is.True);
                Assert.That(possibleWithRelevantRequest, Is.False);
                Assert.That(possibleAtLimit, Is.False);
            });
        }

        [Test]
        public void ClosePossible_UsesStateLimit()
        {
            MonitorExternalRequestTicketsApiConn apiConn = new();
            MonitorExternalRequestTickets component = RenderComponent(apiConn);

            bool closeAllowed = (bool)GetPrivateMethod("ClosePossible", typeof(OwnerTicket)).Invoke(component, new object[]
            {
                CreateOwnerTicket(10, 49)
            })!;
            bool closeBlocked = (bool)GetPrivateMethod("ClosePossible", typeof(OwnerTicket)).Invoke(component, new object[]
            {
                CreateOwnerTicket(11, 50)
            })!;

            Assert.Multiple(() =>
            {
                Assert.That(closeAllowed, Is.True);
                Assert.That(closeBlocked, Is.False);
            });
        }

        [Test]
        public void RequestReInit_StoresSelectedTicketAndOpensDialog()
        {
            MonitorExternalRequestTicketsApiConn apiConn = new();
            MonitorExternalRequestTickets component = RenderComponent(apiConn);
            OwnerTicket ownerTicket = CreateOwnerTicket(15, 49);

            GetPrivateMethod("RequestReInit", typeof(OwnerTicket)).Invoke(component, new object[] { ownerTicket });

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<OwnerTicket>(component, "actOwnerTicket").Ticket.Id, Is.EqualTo(15));
                Assert.That(GetPrivateField<bool>(component, "ReInitMode"), Is.True);
            });
        }

        [Test]
        public void RequestClose_StoresSelectedTicketAndOpensDialog()
        {
            MonitorExternalRequestTicketsApiConn apiConn = new();
            MonitorExternalRequestTickets component = RenderComponent(apiConn);
            OwnerTicket ownerTicket = CreateOwnerTicket(16, 49);

            GetPrivateMethod("RequestClose", typeof(OwnerTicket)).Invoke(component, new object[] { ownerTicket });

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<OwnerTicket>(component, "actOwnerTicket").Ticket.Id, Is.EqualTo(16));
                Assert.That(GetPrivateField<bool>(component, "CloseTicketMode"), Is.True);
            });
        }

        [Test]
        public void Details_OpensTicketDialogAndCloseClosesIt()
        {
            MonitorExternalRequestTicketsApiConn apiConn = new();
            MonitorExternalRequestTickets component = RenderComponent(apiConn);
            OwnerTicket ownerTicket = CreateOwnerTicket(18, 49);

            GetPrivateMethod("Details", typeof(OwnerTicket)).Invoke(component, new object[] { ownerTicket });
            Assert.That(GetPrivateField<bool>(component, "DetailsMode"), Is.True);

            GetPrivateMethod("Close").Invoke(component, Array.Empty<object>());
            Assert.That(GetPrivateField<bool>(component, "DetailsMode"), Is.False);
        }

        private static OwnerTicket CreateOwnerTicket(long ticketId, int stateId)
        {
            return new OwnerTicket
            {
                Owner = new FwoOwner
                {
                    Id = 2,
                    Name = "Owner 2"
                },
                Ticket = new WfTicket
                {
                    Id = ticketId,
                    StateId = stateId,
                    CreationDate = new DateTime(2026, 1, 1)
                }
            };
        }
    }

    internal sealed class MonitorExternalRequestTicketsApiConn : SimulatedApiConnection
    {
        public int StateQueryCount { get; private set; }
        public int OwnerQueryCount { get; private set; }
        public int OwnerTicketQueryCount { get; private set; }
        public int OpenRequestQueryCount { get; private set; }
        public int WorkflowConfigurationQueryCount { get; private set; }

        public List<WfState> States { get; } = new();
        public List<FwoOwner> Owners { get; } = new();
        public List<OwnerTicket> OwnerTickets { get; } = new();
        public List<ExternalRequest> RelevantRequests { get; } = new();

        public MonitorExternalRequestTicketsApiConn()
        {
            States.Add(new WfState { Id = 0, Name = "Zero" });
            States.Add(new WfState { Id = 49, Name = "FortyNine" });
            States.Add(new WfState { Id = 50, Name = "Fifty" });

            Owners.Add(new FwoOwner { Id = 2, Name = "Owner 2" });
            Owners.Add(new FwoOwner { Id = 1, Name = "Owner 1" });

            OwnerTickets.Add(CreateOwnerTicket(10, 49));
            OwnerTickets.Add(CreateOwnerTicket(30, 20));

            RelevantRequests.Add(new ExternalRequest
            {
                Id = 1,
                TicketId = 30,
                ExtRequestState = ExtStates.ExtReqRequested.ToString()
            });
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<WfState>) && query == RequestQueries.getStates)
            {
                StateQueryCount++;
                return Task.FromResult((QueryResponseType)(object)States);
            }

            if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getOwnersWithConn)
            {
                OwnerQueryCount++;
                return Task.FromResult((QueryResponseType)(object)Owners);
            }

            if (typeof(QueryResponseType) == typeof(List<OwnerTicket>) && query == MonitorQueries.getOwnerTickets)
            {
                OwnerTicketQueryCount++;
                return Task.FromResult((QueryResponseType)(object)OwnerTickets);
            }

            if (typeof(QueryResponseType) == typeof(List<ExternalRequest>) && query == ExtRequestQueries.getOpenRequests)
            {
                OpenRequestQueryCount++;
                return Task.FromResult((QueryResponseType)(object)RelevantRequests);
            }

            if (typeof(QueryResponseType) == typeof(List<WorkflowConfiguration>)
                && (query == RequestQueries.getActiveStateMatrixConfiguration || query == RequestQueries.getStateMatrixConfigurationByName))
            {
                WorkflowConfigurationQueryCount++;
                return Task.FromResult((QueryResponseType)(object)new List<WorkflowConfiguration> { CreateWorkflowConfiguration() });
            }

            throw new NotImplementedException();
        }

        private static WorkflowConfiguration CreateWorkflowConfiguration()
        {
            WorkflowConfiguration configuration = new()
            {
                Id = 1,
                Name = "Test configuration",
                IsActive = true
            };

            WorkflowConfigurationPhase phase = new()
            {
                TaskType = WfTaskType.master.ToString(),
                Phase = WorkflowPhases.request.ToString(),
                PhaseMatrixId = 1,
                PhaseMatrix = new StateMatrixPhase
                {
                    Id = 1,
                    Name = "Request phase",
                    Phase = WorkflowPhases.request.ToString(),
                    Active = true,
                    LowestInputState = 0,
                    LowestStartState = 1,
                    LowestEndState = 50,
                    DerivedStates = new List<StateMatrixDerivedState>
                    {
                        new StateMatrixDerivedState
                        {
                            FromStateId = 0,
                            DerivedStateId = 0
                        }
                    }
                }
            };

            configuration.Phases.Add(phase);
            return configuration;
        }

        private static OwnerTicket CreateOwnerTicket(long ticketId, int stateId)
        {
            return new OwnerTicket
            {
                Owner = new FwoOwner
                {
                    Id = 2,
                    Name = "Owner 2"
                },
                Ticket = new WfTicket
                {
                    Id = ticketId,
                    StateId = stateId,
                    CreationDate = new DateTime(2026, 1, 1)
                }
            };
        }
    }
}
