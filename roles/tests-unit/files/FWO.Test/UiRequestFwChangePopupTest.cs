using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiRequestFwChangePopupTest
    {
        private static IRenderedComponent<RequestFwChangePopup> RenderPopup(
            Bunit.TestContext context,
            RequestFwChangePopupTestApiConn apiConn,
            SimulatedUserConfig userConfig,
            FwoOwner selectedApp,
            List<ModellingConnection> connections)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestFwChangePopupAuthStateProvider(Roles.Modeller));
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddScoped<DomEventService>();

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<RequestFwChangePopup>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.SelectedApp, selectedApp)
                    .Add(p => p.Connections, connections)
                    .Add(p => p.ChangeStatus, "Ready")
                    .Add(p => p.LastRequestDate, "2026-05-07")));

            return wrapper.FindComponent<RequestFwChangePopup>();
        }

        private static TValue GetPrivateField<TValue>(object instance, string fieldName)
        {
            FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null ? (TValue)field.GetValue(instance)! : throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        [Test]
        public void DisplayedWithWorkflowNotifications_BuildsTasksAndEnablesRequestButtonForOwner()
        {
            RequestFwChangePopupTestApiConn apiConn = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            FwoOwner selectedApp = new() { Id = 7, Name = "App" };

            using Bunit.TestContext context = new();
            IRenderedComponent<RequestFwChangePopup> component = RenderPopup(context, apiConn, userConfig, selectedApp, [CreateConnection(41)]);

            component.WaitForAssertion(() =>
            {
                List<WfReqTask> tasks = GetPrivateField<List<WfReqTask>>(component.Instance, "TaskList");
                Assert.That(tasks.Any(task => task.TaskType == WfTaskType.access.ToString()
                    && task.GetAddInfoIntValue(AdditionalInfoKeys.ConnId) == 41), Is.True);
            });
            Assert.That(apiConn.Queries, Does.Contain(ModellingQueries.getHistoryForApp));
            Assert.That(component.FindAll("button.btn-primary").Any(button => !button.HasAttribute("disabled")), Is.True);
        }

        [Test]
        public void ExistingRequestInProgress_UsesTicketTasksAndDisablesRequestButton()
        {
            WfReqTask existingTask = new()
            {
                Id = 901,
                TaskNumber = 1,
                TaskType = WfTaskType.access.ToString(),
                StateId = RequestFwChangePopupTestApiConn.kInProgressStateId,
                Title = "Existing request"
            };
            RequestFwChangePopupTestApiConn apiConn = new()
            {
                LatestTicket = new WfTicket
                {
                    Id = 77,
                    StateId = RequestFwChangePopupTestApiConn.kInProgressStateId,
                    CreationDate = new DateTime(2026, 5, 7, 10, 0, 0, DateTimeKind.Utc),
                    Tasks = [existingTask]
                }
            };
            SimulatedUserConfig userConfig = CreateUserConfig();

            using Bunit.TestContext context = new();
            IRenderedComponent<RequestFwChangePopup> component = RenderPopup(context, apiConn, userConfig, new() { Id = 7, Name = "App" }, [CreateConnection(41)]);

            component.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateField<bool>(component.Instance, "RequestInProcess"), Is.True);
                Assert.That(GetPrivateField<List<WfReqTask>>(component.Instance, "TaskList").Single().Id, Is.EqualTo(existingTask.Id));
            });
            Assert.That(apiConn.Queries, Does.Not.Contain(ModellingQueries.getHistoryForApp));
            Assert.That(component.FindAll("button.btn-primary").All(button => button.HasAttribute("disabled")), Is.True);
        }

        [Test]
        public void DisplayedWhenStateLoadingFails_HandlesErrorAndStopsProgress()
        {
            RequestFwChangePopupTestApiConn apiConn = new() { ThrowOnGetStates = true };
            SimulatedUserConfig userConfig = CreateUserConfig();

            using Bunit.TestContext context = new();
            IRenderedComponent<RequestFwChangePopup> component = RenderPopup(context, apiConn, userConfig, new() { Id = 7, Name = "App" }, [CreateConnection(41)]);

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.getStates));
                Assert.That(GetPrivateField<bool>(component.Instance, "WorkInProgress"), Is.False);
                Assert.That(GetPrivateField<List<WfReqTask>>(component.Instance, "TaskList"), Is.Empty);
            });
        }

        private static SimulatedUserConfig CreateUserConfig()
        {
            return new()
            {
                ModIntegrationMode = ModIntegrationMode.WorkflowNotifications,
                User = { Ownerships = [7], Roles = [Roles.Modeller] }
            };
        }

        private static ModellingConnection CreateConnection(int id)
        {
            return new()
            {
                Id = id,
                Name = $"Conn{id}",
                SourceAppRoles =
                [
                    new() { Content = new() { Id = 101, IdString = "AR1", Name = "AR1" } }
                ],
                DestinationAppServers =
                [
                    new() { Content = new() { Id = 201, Name = "Server1", Ip = "10.0.1.1/32" } }
                ],
                Services =
                [
                    new() { Content = new() { Id = 301, Name = "HTTPS", ProtoId = 6, Port = 443 } }
                ]
            };
        }

        private sealed class RequestFwChangePopupAuthStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal;

            public RequestFwChangePopupAuthStateProvider(params string[] roles)
            {
                List<Claim> claims = [.. roles.Select(role => new Claim(ClaimTypes.Role, role))];
                principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }
    }

    internal sealed class RequestFwChangePopupTestApiConn : SimulatedApiConnection
    {
        public const int kInitializedStateId = 23;
        public const int kInProgressStateId = 24;
        public WfTicket? LatestTicket { get; set; }
        public bool ThrowOnGetStates { get; set; }
        public List<string> Queries { get; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            Queries.Add(query);
            if (query == StmQueries.getIpProtocols)
            {
                return Task.FromResult((QueryResponseType)(object)new List<IpProtocol> { new() { Id = 6, Name = "tcp" } });
            }
            if (query == DeviceQueries.getDeviceDetails)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Device>());
            }
            if (query == RequestQueries.getStates)
            {
                if (ThrowOnGetStates)
                {
                    throw new HttpRequestException("state loading failed");
                }
                return Task.FromResult((QueryResponseType)(object)new List<WfState>
                {
                    new() { Id = kInitializedStateId, Name = "Initialized" },
                    new() { Id = kInProgressStateId, Name = "In progress" },
                    new() { Id = 90, Name = "Done" },
                    new() { Id = 91, Name = "Rejected" }
                });
            }
            if (query == RequestQueries.getExtStates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<WfExtState>
                {
                    new() { Name = ExtStates.ExtReqInitialized.ToString(), StateId = kInitializedStateId },
                    new() { Name = ExtStates.ExtReqInProgress.ToString(), StateId = kInProgressStateId },
                    new() { Name = ExtStates.ExtReqDone.ToString(), StateId = 90 },
                    new() { Name = ExtStates.ExtReqRejected.ToString(), StateId = 91 }
                });
            }
            if (query == ExtRequestQueries.getLatestTicketId)
            {
                List<TicketId> ticketIds = LatestTicket != null ? [new() { Id = LatestTicket.Id }] : [];
                return Task.FromResult((QueryResponseType)(object)ticketIds);
            }
            if (query == RequestQueries.getTicketById && LatestTicket != null)
            {
                return Task.FromResult((QueryResponseType)(object)LatestTicket);
            }
            if (query == ModellingQueries.getHistoryForApp)
            {
                return Task.FromResult((QueryResponseType)(object)new List<ModellingHistoryEntry>());
            }
            throw new AssertionException($"Unexpected query: {query}");
        }

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(
            Action<Exception> exceptionHandler,
            GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler,
            string subscription,
            object? variables = null,
            string? operationName = null)
        {
            return null!;
        }
    }
}
