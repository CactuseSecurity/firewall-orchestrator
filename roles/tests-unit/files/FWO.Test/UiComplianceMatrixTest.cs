using System.Reflection;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using FWO.Ui.Pages.Compliance;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiComplianceMatrixTest
    {
        [Test]
        public void AddAutoCalculatedInternetZone_MissingZone_AddsAndReloadsZone()
        {
            MatrixApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                AutoCalculateInternetZone = true,
                AutoCalculateUndefinedInternalZone = false
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            userConfig.User.Roles = [Roles.Admin];
            userConfig.SetExecutionMode(Roles.Admin);

            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton(userConfig);
            context.Services.AddSingleton(new NetworkZoneService());
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddLocalization();

            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<ComplianceMatrix>());

            root.Find("#add-auto-calculated-internet-zone").Click();

            root.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.AutoCalculatedInternetZoneAdditions, Is.EqualTo(1));
                Assert.That(root.FindAll("#add-auto-calculated-internet-zone"), Is.Empty);
            });
        }

        [Test]
        public async Task AddAutoCalculatedInternetZone_WhileAdding_BlocksConcurrentAddition()
        {
            MatrixApiConnection apiConnection = new() { DelayAutoCalculatedInternetZoneAddition = true };
            SimulatedGlobalConfig globalConfig = new()
            {
                AutoCalculateInternetZone = true,
                AutoCalculateUndefinedInternalZone = false
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            userConfig.User.Roles = [Roles.Admin];
            userConfig.SetExecutionMode(Roles.Admin);

            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton(userConfig);
            context.Services.AddSingleton(new NetworkZoneService());
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddLocalization();

            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<ComplianceMatrix>());

            try
            {
                root.Find("#add-auto-calculated-internet-zone").Click();
                await apiConnection.AutoCalculatedInternetZoneAdditionStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

                root.WaitForAssertion(() =>
                    Assert.That(root.Find("#add-auto-calculated-internet-zone").HasAttribute("disabled"), Is.True));
                root.Find("#add-auto-calculated-internet-zone").Click();

                Assert.That(apiConnection.AutoCalculatedInternetZoneAdditions, Is.EqualTo(1));
            }
            finally
            {
                apiConnection.ReleaseAutoCalculatedInternetZoneAddition.TrySetResult(null);
            }

            root.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.AutoCalculatedInternetZoneAdditions, Is.EqualTo(1));
                Assert.That(root.FindAll("#add-auto-calculated-internet-zone"), Is.Empty);
            });
        }

        [Test]
        public void AddAutoCalculatedInternetZone_PreservesUndefinedInternalZone()
        {
            MatrixApiConnection apiConnection = new();
            apiConnection.AddUndefinedInternalZoneWithCommunication();
            SimulatedGlobalConfig globalConfig = new()
            {
                AutoCalculateInternetZone = true,
                AutoCalculateUndefinedInternalZone = true
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            userConfig.User.Roles = [Roles.Admin];
            userConfig.SetExecutionMode(Roles.Admin);

            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton(userConfig);
            context.Services.AddSingleton(new NetworkZoneService());
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddLocalization();

            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<ComplianceMatrix>());

            root.Find("#add-auto-calculated-internet-zone").Click();

            root.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.AutoCalculatedInternetZoneAdditions, Is.EqualTo(1));
                Assert.That(apiConnection.AutoCalculatedUndefinedInternalZoneRemovals, Is.Zero);
            });
        }

        [Test]
        public void AddAutoCalculatedInternetZone_InternetZoneCreationFailsAfterUndefinedInternalZoneCreated_ReloadsZones()
        {
            MatrixApiConnection apiConnection = new() { ThrowWhenAddingAutoCalculatedInternetZone = true };
            SimulatedGlobalConfig globalConfig = new()
            {
                AutoCalculateInternetZone = true,
                AutoCalculateUndefinedInternalZone = true
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            userConfig.User.Roles = [Roles.Admin];
            userConfig.SetExecutionMode(Roles.Admin);
            NetworkZoneService networkZoneService = new();

            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton(userConfig);
            context.Services.AddSingleton(networkZoneService);
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddLocalization();

            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<ComplianceMatrix>());

            int networkZoneLoadsBeforeAdding = apiConnection.NetworkZoneLoads;
            root.Find("#add-auto-calculated-internet-zone").Click();

            root.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.AutoCalculatedInternetZoneAdditions, Is.EqualTo(1));
                Assert.That(apiConnection.NetworkZoneLoads, Is.EqualTo(networkZoneLoadsBeforeAdding + 2));
            });
        }

        [Test]
        public async Task ComplianceMatrix_WithoutAllowedRole_SkipsLoading()
        {
            MatrixApiConnection apiConnection = new();
            UserConfig userConfig = UserConfig.ForTextOnly(new SimulatedGlobalConfig());

            await using BunitContext context = CreateContext(apiConnection, userConfig);
            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<ComplianceMatrix>());

            root.WaitForAssertion(() => Assert.That(root.FindAll("#add-auto-calculated-internet-zone"), Is.Empty));
            Assert.That(apiConnection.NetworkZoneLoads, Is.Zero);
        }

        [Test]
        public async Task ComplianceMatrix_EmptyMatrices_ShowsNoNetworkZones()
        {
            MatrixApiConnection apiConnection = new() { Matrices = [] };
            UserConfig userConfig = CreateAdminUserConfig(new SimulatedGlobalConfig());

            await using BunitContext context = CreateContext(apiConnection, userConfig);
            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<ComplianceMatrix>());
            NetworkZoneService networkZoneService = context.Services.GetRequiredService<NetworkZoneService>();

            root.WaitForAssertion(() =>
            {
                Assert.That(networkZoneService.NetworkZones, Is.Empty);
                Assert.That(root.FindAll("table tbody tr"), Is.Empty);
            });
        }

        [Test]
        public async Task ComplianceMatrix_ReadonlyMatrix_DisallowsDelete()
        {
            MatrixApiConnection apiConnection = new MatrixApiConnection
            {
                Matrices =
                [
                    new ComplianceCriterion { Id = 1, Name = "Imported", ImportSource = "import" }
                ]
            };
            SimulatedGlobalConfig globalConfig = new() { ImportedMatrixReadOnly = true };
            UserConfig userConfig = CreateAdminUserConfig(globalConfig);

            await using BunitContext context = CreateContext(apiConnection, userConfig);
            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<ComplianceMatrix>());
            ComplianceMatrix component = root.FindComponent<ComplianceMatrix>().Instance;

            await root.InvokeAsync(() => InvokePrivateTask(component, "RequestDeleteMatrix"));

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "deleteMatrixMode"), Is.True);
                Assert.That(GetPrivateField<bool>(component, "deleteAllowed"), Is.False);
                Assert.That(GetPrivateField<string>(component, "deleteMessage"), Is.Not.Empty);
            });
        }

        [Test]
        public async Task ComplianceMatrix_LinkedMatrix_DisallowsDelete()
        {
            MatrixApiConnection apiConnection = new MatrixApiConnection
            {
                LinkedPolicies = [new LinkedPolicy { PolicyId = 7 }]
            };
            UserConfig userConfig = CreateAdminUserConfig(new SimulatedGlobalConfig());

            await using BunitContext context = CreateContext(apiConnection, userConfig);
            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<ComplianceMatrix>());
            ComplianceMatrix component = root.FindComponent<ComplianceMatrix>().Instance;

            await root.InvokeAsync(() => InvokePrivateTask(component, "RequestDeleteMatrix"));

            Assert.That(GetPrivateField<bool>(component, "deleteAllowed"), Is.False);
        }

        [Test]
        public async Task ComplianceMatrix_DeleteUnusedMatrix_RemovesMatrix()
        {
            MatrixApiConnection apiConnection = new();
            UserConfig userConfig = CreateAdminUserConfig(new SimulatedGlobalConfig());

            await using BunitContext context = CreateContext(apiConnection, userConfig);
            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<ComplianceMatrix>());
            ComplianceMatrix component = root.FindComponent<ComplianceMatrix>().Instance;

            await root.InvokeAsync(() => InvokePrivateTask(component, "DeleteMatrix"));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.RemovedCriterionCalls, Is.EqualTo(1));
                Assert.That(GetPrivateField<List<ComplianceCriterion>>(component, "matrices"), Is.Empty);
                Assert.That(GetPrivateField<bool>(component, "deleteMatrixMode"), Is.False);
            });
        }

        [Test]
        public async Task ComplianceMatrix_CommitChanges_SendsUpdateAndShowsSuccess()
        {
            MatrixApiConnection apiConnection = new();
            UserConfig userConfig = CreateAdminUserConfig(new SimulatedGlobalConfig());
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages =
                [];

            await using BunitContext context = CreateContext(apiConnection, userConfig, messages);
            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(value => value.Value, (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
                        messages.Add((exception, title, message, isError))))
                    .AddChildContent<ComplianceMatrix>()));
            ComplianceMatrix component = root.FindComponent<ComplianceMatrix>().Instance;
            List<NetworkZone> zones = context.Services.GetRequiredService<NetworkZoneService>().NetworkZones;
            root.WaitForAssertion(() => Assert.That(zones, Is.Not.Empty));
            SetPrivateField(component, "addCommunication", new List<(NetworkZone, NetworkZone)> { (zones[0], zones[0]) });
            SetPrivateField(component, "deleteCommunication", new List<(NetworkZone, NetworkZone)> { (zones[0], zones[0]) });

            await root.InvokeAsync(() => InvokePrivateTask(component, "CommitChanges"));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpdateNetworkZoneCommunicationCalls, Is.EqualTo(1));
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].IsError, Is.False, messages[0].Exception?.ToString() ?? messages[0].Message);
                Assert.That(GetPrivateField<bool>(component, "editMode"), Is.False);
            });
        }

        private static UserConfig CreateAdminUserConfig(SimulatedGlobalConfig globalConfig)
        {
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            userConfig.User.Roles = [Roles.Admin];
            userConfig.SetExecutionMode(Roles.Admin);
            return userConfig;
        }

        private static BunitContext CreateContext(MatrixApiConnection apiConnection, UserConfig userConfig,
            List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton(userConfig);
            context.Services.AddSingleton(new NetworkZoneService());
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(userConfig.User.Roles.FirstOrDefault() ?? ""));
            context.Services.AddLocalization();
            if (messages != null)
            {
                context.Services.AddSingleton<Action<Exception?, string, string, bool>>((exception, title, message, isError) =>
                    messages.Add((exception, title, message, isError)));
            }
            return context;
        }

        private static async Task InvokePrivateTask(object instance, string methodName)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            await (Task)(method.Invoke(instance, null) ?? throw new InvalidOperationException($"Method {methodName} returned null."));
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
            return (T)(field.GetValue(instance) ?? throw new InvalidOperationException($"Field {fieldName} is null."));
        }

        private static void SetPrivateField<T>(object instance, string fieldName, T value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
            field.SetValue(instance, value);
        }

        private sealed class MatrixApiConnection : SimulatedApiConnection
        {
            private readonly List<NetworkZone> networkZones =
            [
                new NetworkZone { Id = 1, IdString = "manual-zone", Name = "Manual zone" }
            ];

            public List<ComplianceCriterion> Matrices { get; init; } =
            [
                new ComplianceCriterion { Id = 1, Name = "Manual matrix" }
            ];
            public List<LinkedPolicy> LinkedPolicies { get; init; } = [];
            public int AutoCalculatedInternetZoneAdditions { get; private set; }
            public int AutoCalculatedUndefinedInternalZoneRemovals { get; private set; }
            public int NetworkZoneLoads { get; private set; }
            public bool DelayAutoCalculatedInternetZoneAddition { get; init; }
            public bool ThrowWhenAddingAutoCalculatedInternetZone { get; init; }
            public int RemovedCriterionCalls { get; private set; }
            public int UpdateNetworkZoneCommunicationCalls { get; private set; }
            public TaskCompletionSource<object?> AutoCalculatedInternetZoneAdditionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<object?> ReleaseAutoCalculatedInternetZoneAddition { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ComplianceQueries.getMatrices && typeof(QueryResponseType) == typeof(List<ComplianceCriterion>))
                {
                    return Task.FromResult((QueryResponseType)(object)Matrices);
                }

                if (query == ComplianceQueries.getPolicyIdsForCrit && typeof(QueryResponseType) == typeof(List<LinkedPolicy>))
                {
                    return Task.FromResult((QueryResponseType)(object)LinkedPolicies);
                }

                if (query == ComplianceQueries.removeCriterion)
                {
                    RemovedCriterionCalls++;
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == ComplianceQueries.updateNetworkZoneCommunication)
                {
                    UpdateNetworkZoneCommunicationCalls++;
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == NetworkZoneQueries.getNetworkZonesForMatrix && typeof(QueryResponseType) == typeof(List<NetworkZone>))
                {
                    NetworkZoneLoads++;
                    return Task.FromResult((QueryResponseType)(object)networkZones);
                }

                if (query == NetworkZoneQueries.addNetworkZone)
                {
                    PropertyInfo? isInternetZoneProperty = variables?.GetType().GetProperty("isAutoCalculatedInternetZone");
                    PropertyInfo? isUndefinedInternalZoneProperty = variables?.GetType().GetProperty("isAutoCalculatedUndefinedInternalZone");

                    if (isUndefinedInternalZoneProperty?.GetValue(variables) is true)
                    {
                        AddAutoCalculatedUndefinedInternalZone();
                        return Task.FromResult(default(QueryResponseType)!);
                    }

                    if (isInternetZoneProperty?.GetValue(variables) is true)
                    {
                        AutoCalculatedInternetZoneAdditions++;
                        if (ThrowWhenAddingAutoCalculatedInternetZone)
                        {
                            throw new InvalidOperationException("Unable to add auto-calculated Internet zone.");
                        }
                        if (DelayAutoCalculatedInternetZoneAddition)
                        {
                            AutoCalculatedInternetZoneAdditionStarted.TrySetResult(null);
                            return CompleteAutoCalculatedInternetZoneAddition<QueryResponseType>();
                        }
                        AddAutoCalculatedInternetZone();
                    }

                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == NetworkZoneQueries.removeNetworkZone)
                {
                    AutoCalculatedUndefinedInternalZoneRemovals++;
                    return Task.FromResult(default(QueryResponseType)!);
                }

                throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
            }

            /// <summary>
            /// Completes a delayed Internet-zone insertion after the test releases it.
            /// </summary>
            private async Task<QueryResponseType> CompleteAutoCalculatedInternetZoneAddition<QueryResponseType>()
            {
                await ReleaseAutoCalculatedInternetZoneAddition.Task;
                AddAutoCalculatedInternetZone();
                return default!;
            }

            /// <summary>
            /// Adds the simulated auto-calculated Internet zone to subsequent query results.
            /// </summary>
            private void AddAutoCalculatedInternetZone()
            {
                networkZones.Add(new NetworkZone
                {
                    Id = 2,
                    IdString = "AUTO_CALCULATED_ZONE_INTERNET",
                    Name = "Auto-calculated Internet Zone",
                    IsAutoCalculatedInternetZone = true
                });
            }

            private void AddAutoCalculatedUndefinedInternalZone()
            {
                networkZones.Add(new NetworkZone
                {
                    Id = 3,
                    IdString = NetworkZoneService.kAutoCalculatedUndefinedInternalZoneIdString,
                    Name = "Auto-calculated Undefined-internal Zone",
                    IsAutoCalculatedUndefinedInternalZone = true
                });
            }

            public void AddUndefinedInternalZoneWithCommunication()
            {
                List<NetworkZone> allowedDestinations = [networkZones[0]];
                networkZones.Add(new NetworkZone
                {
                    Id = 3,
                    IdString = "AUTO_CALCULATED_ZONE_UNDEFINED_INTERNAL",
                    Name = "Auto-calculated Undefined-internal Zone",
                    IsAutoCalculatedUndefinedInternalZone = true,
                    AllowedCommunicationDestinations = allowedDestinations.ToArray()
                });
            }
        }
    }
}
