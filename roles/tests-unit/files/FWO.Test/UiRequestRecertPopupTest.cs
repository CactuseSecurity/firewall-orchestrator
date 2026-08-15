using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiRequestRecertPopupTest
    {
        [Test]
        public void CheckImplementation_RunsVarianceQueriesWithAmbientRole()
        {
            RequestRecertPopupTestApiConn apiConn = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            FwoOwner selectedApp = new() { Id = 7, Name = "App" };
            ModellingAppHandler appHandler = new(apiConn, userConfig, selectedApp, DefaultInit.DoNothing, true)
            {
                Connections = [CreateConnection(41)]
            };
            apiConn.SetAmbientRole(CreatePrincipal(Roles.Auditor), [Roles.Modeller, Roles.Admin, Roles.Auditor]);

            using BunitContext context = new();
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderPopup(context, apiConn, userConfig, appHandler);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(apiConn.WasOnlySentWithRole(DeviceQueries.getManagementNames, Roles.Auditor), Is.True);
                Assert.That(apiConn.WasOnlySentWithRole(ModellingQueries.getNwGroupObjects, Roles.Auditor), Is.True);
                Assert.That(apiConn.WasOnlySentWithRole(ModellingQueries.getAppZonesByAppId, Roles.Auditor), Is.True);
            });
        }

        [Test]
        public void RequestRunning_DisablesRecertButtonAndShowsWaitingMessage()
        {
            RequestRecertPopupTestApiConn apiConn = new()
            {
                LatestTicket = new WfTicket
                {
                    Id = 77,
                    StateId = 24,
                    Tasks =
                    [
                        new WfReqTask
                        {
                            Id = 901,
                            StateId = 24,
                            TaskType = WfTaskType.access.ToString(),
                            Title = "recert-task"
                        }
                    ]
                }
            };
            SimulatedUserConfig userConfig = CreateUserConfig();
            FwoOwner selectedApp = new() { Id = 7, Name = "App" };
            ModellingAppHandler appHandler = new(apiConn, userConfig, selectedApp, DefaultInit.DoNothing, true)
            {
                Connections = [CreateConnection(41)]
            };

            using BunitContext context = new();
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderPopup(context, apiConn, userConfig, appHandler);

            wrapper.WaitForAssertion(() =>
            {
                RequestRecertPopup popup = wrapper.FindComponent<RequestRecertPopup>().Instance;
                Assert.Multiple(() =>
                {
                    Assert.That(GetPrivateField<bool>(popup, "RequestRunning"), Is.True);
                    Assert.That(GetPrivateField<bool>(popup, "RecertPossible"), Is.False);
                    Assert.That(wrapper.FindAll("button.btn-success").All(button => button.HasAttribute("disabled")), Is.True);
                });
            });
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderPopup(BunitContext context, RequestRecertPopupTestApiConn apiConn, SimulatedUserConfig userConfig, ModellingAppHandler appHandler)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestRecertPopupAuthStateProvider(Roles.Auditor));
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<IRuleTreeBuilder, RuleTreeBuilder>();

            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<RequestRecertPopup>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.AppHandler, appHandler)
                    .Add(p => p.CanRecertify, true)));
        }

        [Test]
        public async Task Close_ResetsDisplayAndFirstTry()
        {
            RequestRecertPopupTestApiConn apiConn = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            FwoOwner selectedApp = new() { Id = 7, Name = "App" };
            ModellingAppHandler appHandler = new(apiConn, userConfig, selectedApp, DefaultInit.DoNothing, true)
            {
                Connections = [CreateConnection(41)]
            };

            using BunitContext context = new();
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderPopup(context, apiConn, userConfig, appHandler);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(GetPrivateField<bool>(wrapper.FindComponent<RequestRecertPopup>().Instance, "FirstTry"), Is.False);
            });

            RequestRecertPopup component = wrapper.FindComponent<RequestRecertPopup>().Instance;
            component.GetType().GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(component.Display, Is.False);
                Assert.That(GetPrivateField<bool>(component, "FirstTry"), Is.True);
            });
            await Task.CompletedTask;
        }

        [Test]
        public async Task StartRecert_WhenUserLacksRole_ShowsPermissionError()
        {
            RequestRecertPopupTestApiConn apiConn = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            userConfig.User.Roles = [];
            FwoOwner selectedApp = new() { Id = 7, Name = "App" };
            ModellingAppHandler appHandler = new(apiConn, userConfig, selectedApp, DefaultInit.DoNothing, true)
            {
                Connections = [CreateConnection(41)]
            };
            List<(string Title, string Message, bool Error)> messages = [];

            using BunitContext context = new();
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderPopup(context, apiConn, userConfig, appHandler);
            RequestRecertPopup component = wrapper.FindComponent<RequestRecertPopup>().Instance;

            SetPrivateProperty(component, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((_, title, message, error) => messages.Add((title, message, error))));
            SetPrivateProperty(component, "CanRecertify", true);

            await wrapper.InvokeAsync(() => (Task)component.GetType().GetMethod("StartRecert", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(component, null)!);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Not.Empty);
                Assert.That(messages.Any(message => message.Message == userConfig.GetText("E9104") && message.Error), Is.True);
            });
            await Task.CompletedTask;
        }

        private static SimulatedUserConfig CreateUserConfig()
        {
            return new()
            {
                ModNamingConvention = "{}",
                ModIntegrationMode = ModIntegrationMode.FullyIntegrated,
                ModModelledMarker = "FWO:",
                User = { Ownerships = [7], Roles = [Roles.Auditor] }
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

        private static ClaimsPrincipal CreatePrincipal(params string[] roles)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                "Test",
                ClaimTypes.Name,
                ClaimTypes.Role));
        }

        private sealed class RequestRecertPopupAuthStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal;

            public RequestRecertPopupAuthStateProvider(params string[] roles)
            {
                principal = CreatePrincipal(roles);
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private sealed class RequestRecertPopupTestApiConn : SimulatedApiConnection
        {
            private string activeRole = "";
            private readonly Stack<string> previousRoles = new();
            private readonly List<(string Query, string Role)> queries = [];
            public WfTicket? LatestTicket { get; init; }

            public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                SetRole(targetRoleList.First(role => user.IsInRole(role)));
            }

            public override void SetRole(string role)
            {
                previousRoles.Push(activeRole);
                activeRole = role;
            }

            public override string GetActRole()
            {
                return activeRole;
            }

            public override void SetAmbientRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                activeRole = targetRoleList.FirstOrDefault(role => user.IsInRole(role)) ?? "";
            }

            public override void SwitchBack()
            {
                activeRole = previousRoles.TryPop(out string? previousRole) ? previousRole : "";
            }

            public bool WasOnlySentWithRole(string query, string role)
            {
                List<(string Query, string Role)> matchingQueries = [.. queries.Where(q => q.Query == query)];
                return matchingQueries.Count > 0 && matchingQueries.All(q => q.Role == role);
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                queries.Add((query, activeRole));
                if (query == ExtRequestQueries.getLatestTicketId)
                {
                    return Task.FromResult((QueryResponseType)(object)(LatestTicket != null ? new List<TicketId> { new() { Id = LatestTicket.Id } } : new List<TicketId>()));
                }
                if (query == RequestQueries.getExtStates)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<WfExtState>
                    {
                        new() { Name = ExtStates.ExtReqDone.ToString(), StateId = 90 },
                        new() { Name = ExtStates.ExtReqRejected.ToString(), StateId = 91 }
                    });
                }
                if (query == DeviceQueries.getManagementNames)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Management>());
                }
                if (query == ModellingQueries.getNwGroupObjects)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingNetworkArea>());
                }
                if (query == ModellingQueries.getAppZonesByAppId)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppZone>());
                }
                if (query == RequestQueries.getTicketById && LatestTicket != null)
                {
                    return Task.FromResult((QueryResponseType)(object)LatestTicket);
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private static TValue GetPrivateField<TValue>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
            return (TValue)field.GetValue(instance)!;
        }

        private static void SetPrivateProperty(object instance, string propertyName, object? value)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            property.SetValue(instance, value);
        }
    }
}
