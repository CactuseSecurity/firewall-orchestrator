using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiFlowSettingsPagesTest
    {
        [SetUp]
        public void SetUp()
        {
            SeedTranslations();
        }

        [Test]
        public async Task FlowNetworkGroupsPage_RendersWithoutErrors()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsFlowNetworkGroups> component = RenderPage<SettingsFlowNetworkGroups>(context);

            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("Flow Network Group")));
        }

        [Test]
        public async Task FlowServiceObjectsPage_RendersWithoutErrors()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);

            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("Flow Service Object")));
        }

        [Test]
        public async Task FlowServiceObjectsPage_CreateCustomObject_SendsInsertAndMapping()
        {
            await using BunitContext context = CreateCustomServiceCreateContext(out FlowServiceObjectsCustomCreateApiConn apiConnection);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-primary"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-primary").First().Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("input.form-control.form-control-sm"), Is.Not.Empty));

            component.FindAll("input.form-control.form-control-sm").First().Change("Custom Service");
            component.FindAll("button.btn-outline-primary").First().Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-success"), Is.Not.Empty));
            component.FindAll("button.btn.btn-sm.btn-primary").Last().Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(FlowQueries.insertFlowSvcObjects));
                Assert.That(apiConnection.Queries, Does.Contain(FlowMutations.upsertFlowSvcObjectMapping));
                Assert.That(apiConnection.InsertedServiceObjectName, Is.EqualTo("Custom Service"));
                Assert.That(apiConnection.MappedServiceIds, Is.EqualTo(new List<long> { 11 }));
            });
        }

        [Test]
        public async Task FlowServiceGroupsPage_RendersWithoutErrors()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsFlowServiceGroups> component = RenderPage<SettingsFlowServiceGroups>(context);

            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("Flow Service Group")));
        }

        [Test]
        public async Task FlowTimeObjectsPage_RendersWithoutErrors()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsFlowTimeObjects> component = RenderPage<SettingsFlowTimeObjects>(context);

            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("Flow Time Object")));
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<ApiConnection>(new FlowSettingsPagesTestApiConn());
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User = { Roles = [Roles.Admin] }
            });
            context.Services.AddSingleton<AuthenticationStateProvider>(new FlowSettingsPagesAuthStateProvider(Roles.Admin));
            return context;
        }

        private static BunitContext CreateCustomServiceCreateContext(out FlowServiceObjectsCustomCreateApiConn apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            apiConnection = new FlowServiceObjectsCustomCreateApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User = { Roles = [Roles.Admin] }
            });
            context.Services.AddSingleton<AuthenticationStateProvider>(new FlowSettingsPagesAuthStateProvider(Roles.Admin));
            return context;
        }

        private static void SeedTranslations()
        {
            foreach (string key in new[]
            {
                "network_groups",
                "service_objects",
                "service_groups",
                "time_objects",
                "duplicate_objects",
                "flow_object",
                "management",
                "objects",
                "actions",
                "id",
                "name",
                "state",
                "show_in_request_module",
                "details",
                "uid",
                "search_name",
                "custom_objects",
                "create_custom_flow_object",
                "flow_objects",
                "edit_flow_object",
                "save",
                "cancel",
                "select",
                "no_duplicate_conflicts",
                "current",
                "type",
                "ip"
            })
            {
                SimulatedUserConfig.DummyTranslate.TryAdd(key, key);
            }
        }

        private static IRenderedComponent<TComponent> RenderPage<TComponent>(BunitContext context)
            where TComponent : Microsoft.AspNetCore.Components.IComponent
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<TComponent>())
                .FindComponent<TComponent>();
        }

        private sealed class FlowSettingsPagesAuthStateProvider(params string[] roles) : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal = new(new ClaimsIdentity(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                authenticationType: "Test",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role));

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }
    }

    internal sealed class FlowSettingsPagesTestApiConn : SimulatedApiConnection
    {
        private static readonly FlowSvcObject kFlowSvcObject = new()
        {
            Id = 100,
            Name = "Flow Service Object",
            PortStart = 80,
            PortEnd = 80,
            ProtoId = 6,
            State = FlowState.Requested,
            ShowInRequestModule = true
        };

        private static readonly FlowSvcGroup kFlowSvcGroup = new()
        {
            Id = 200,
            Name = "Flow Service Group",
            State = FlowState.Requested,
            ShowInRequestModule = true,
            SvcGroupMembers = [new FlowSvcGroupMember()]
        };

        private static readonly FlowNwGroup kFlowNwGroup = new()
        {
            Id = 300,
            Name = "Flow Network Group",
            State = FlowState.Requested,
            ShowInRequestModule = true,
            NwGroupMembers = [new FlowNwGroupMember()]
        };

        private static readonly FlowTimeObject kFlowTimeObject = new()
        {
            Id = 400,
            Name = "Flow Time Object",
            StartTime = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc),
            State = FlowState.Requested,
            ShowInRequestModule = true
        };

        private static readonly Management kManagement = new()
        {
            Id = 10,
            Name = "Management"
        };

        private static readonly Management kServiceManagement = new()
        {
            Id = 10,
            Name = "Management",
            Services =
            [
                new()
                {
                    Id = 11,
                    Name = "Service A",
                    Uid = "svc-a",
                    DestinationPort = 80,
                    ProtoId = 6,
                    FlowServiceObjectId = 100,
                    FlowServiceGroupId = 200,
                    FlowActive = false
                },
                new()
                {
                    Id = 12,
                    Name = "Service B",
                    Uid = "svc-b",
                    DestinationPort = 80,
                    ProtoId = 6,
                    FlowServiceObjectId = 100,
                    FlowServiceGroupId = 200,
                    FlowActive = false
                }
            ]
        };

        private static readonly Management kNetworkManagement = new()
        {
            Id = 10,
            Name = "Management",
            Objects =
            [
                new()
                {
                    Id = 21,
                    Name = "Object A",
                    Uid = "obj-a",
                    IP = "10.0.0.1/32",
                    FlowNetworkGroupId = 300,
                    FlowActive = false
                },
                new()
                {
                    Id = 22,
                    Name = "Object B",
                    Uid = "obj-b",
                    IP = "10.0.0.2/32",
                    FlowNetworkGroupId = 300,
                    FlowActive = false
                }
            ]
        };

        private static readonly Management kTimeManagement = new()
        {
            Id = 10,
            Name = "Management",
            TimeObjects =
            [
                new()
                {
                    Id = 31,
                    Name = "Time A",
                    Uid = "time-a",
                    StartTime = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc),
                    FlowTimeObjectId = 400,
                    FlowActive = false
                },
                new()
                {
                    Id = 32,
                    Name = "Time B",
                    Uid = "time-b",
                    StartTime = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 5, 1, 19, 0, 0, DateTimeKind.Utc),
                    FlowTimeObjectId = 400,
                    FlowActive = false
                }
            ]
        };

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            object result = query switch
            {
                string q when q == FlowQueries.getFlowServiceObjects => new List<FlowSvcObject> { kFlowSvcObject },
                string q when q == FlowQueries.getFlowServiceGroups => new List<FlowSvcGroup> { kFlowSvcGroup },
                string q when q == FlowQueries.getFlowAddressGroups => new List<FlowNwGroup> { kFlowNwGroup },
                string q when q == FlowQueries.getFlowTimeObjects => new List<FlowTimeObject> { kFlowTimeObject },
                string q when q == FlowQueries.getFlowSelectableManagements => new List<Management> { kManagement },
                string q when q == FlowQueries.getFlowCustomServiceCandidates => new List<Management> { kServiceManagement },
                string q when q == FlowQueries.getFlowCustomObjectCandidates => new List<Management> { kNetworkManagement },
                string q when q == FlowQueries.getFlowCustomTimeObjectCandidates => new List<Management> { kTimeManagement },
                _ => throw new InvalidOperationException($"Unexpected query: {query}")
            };

            return Task.FromResult((QueryResponseType)result);
        }
    }

    internal sealed class FlowServiceObjectsCustomCreateApiConn : SimulatedApiConnection
    {
        public List<string> Queries { get; } = [];
        public string InsertedServiceObjectName { get; private set; } = "";
        public List<long> MappedServiceIds { get; } = [];

        private readonly FlowSvcObject flowSvcObject = new()
        {
            Id = 100,
            Name = "Flow Service Object",
            PortStart = 80,
            PortEnd = 80,
            ProtoId = 6,
            State = FlowState.Requested,
            ShowInRequestModule = true
        };

        private readonly Management management = new()
        {
            Id = 10,
            Name = "Management",
            Services =
            [
                new()
                {
                    Id = 11,
                    Name = "Service A",
                    Uid = "svc-a",
                    DestinationPort = 80,
                    DestinationPortEnd = 80,
                    ProtoId = 6,
                    FlowServiceObjectId = null,
                    FlowActive = false
                },
                new()
                {
                    Id = 12,
                    Name = "Service B",
                    Uid = "svc-b",
                    DestinationPort = 80,
                    DestinationPortEnd = 80,
                    ProtoId = 6,
                    FlowServiceObjectId = null,
                    FlowActive = false
                }
            ]
        };

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (query == FlowQueries.getFlowServiceObjects)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FlowSvcObject> { flowSvcObject });
            }
            if (query == FlowQueries.getFlowSelectableManagements)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { new() { Id = 10, Name = "Management" } });
            }
            if (query == FlowQueries.getFlowCustomServiceCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { management });
            }
            if (query == FlowQueries.insertFlowSvcObjects && typeof(QueryResponseType) == typeof(FlowSvcObjectInsertResult))
            {
                object?[] insertedObjects = GetAnonymousArray(variables, "objects");
                object? firstObject = insertedObjects.FirstOrDefault();
                InsertedServiceObjectName = GetAnonymousProperty<string>(firstObject, "Name");
                return Task.FromResult((QueryResponseType)(object)new FlowSvcObjectInsertResult
                {
                    Returning =
                    [
                        new FlowSvcObject
                        {
                            Id = 900,
                            Name = InsertedServiceObjectName,
                            PortStart = 80,
                            PortEnd = 80,
                            ProtoId = 6,
                            State = FlowState.Implemented,
                            ShowInRequestModule = true
                        }
                    ]
                });
            }
            if (query == FlowMutations.upsertFlowSvcObjectMapping && typeof(QueryResponseType) == typeof(NetworkService))
            {
                long serviceId = GetAnonymousProperty<long>(variables, "svcId");
                MappedServiceIds.Add(serviceId);
                return Task.FromResult((QueryResponseType)(object)new NetworkService
                {
                    Id = serviceId,
                    Name = serviceId == 11 ? "Service A" : "Service B",
                    Uid = serviceId == 11 ? "svc-a" : "svc-b",
                    DestinationPort = 80,
                    DestinationPortEnd = 80,
                    ProtoId = 6,
                    FlowServiceObjectId = 900,
                    FlowActive = true
                });
            }
            throw new InvalidOperationException($"Unexpected query: {query}");
        }

        private static T GetAnonymousProperty<T>(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (T)(variables.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }

        private static object?[] GetAnonymousArray(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (object?[])(variables.GetType().GetProperty(propertyName)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }
    }
}
