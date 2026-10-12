using System.Reflection;
using System.Security.Claims;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Monitoring;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Focused tests for the "allow full rollback" setting and the guard that protects the
    /// destructive full-management rollback. They cover the default-safe behaviour, that the
    /// setting is read from the global config, and that the guard blocks the destructive API
    /// calls while the setting is disabled.
    /// </summary>
    [TestFixture]
    internal sealed class ImportRollbackGuardTest
    {
        [Test]
        public void ConfigData_DisablesFullRollbackByDefault()
        {
            FWO.Config.Api.Data.ConfigData configData = new();

            Assert.That(configData.AllowFullRollback, Is.False);
        }

        [Test]
        public void UserConfig_FullRollbackAllowed_DefaultsToFalseWithoutGlobalConfig()
        {
            using UserConfig userConfig = new();

            Assert.That(userConfig.FullRollbackAllowed, Is.False);
        }

        [Test]
        public void UserConfig_FullRollbackAllowed_ReflectsDisabledGlobalConfig()
        {
            SimulatedGlobalConfig globalConfig = new() { AllowFullRollback = false };
            using UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);

            Assert.That(userConfig.FullRollbackAllowed, Is.False);
        }

        [Test]
        public void UserConfig_FullRollbackAllowed_ReflectsEnabledGlobalConfig()
        {
            SimulatedGlobalConfig globalConfig = new() { AllowFullRollback = true };
            using UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);

            Assert.That(userConfig.FullRollbackAllowed, Is.True);
        }

        [Test]
        public async Task FullMgmRollback_DoesNothing_WhenSettingIsDisabled()
        {
            RecordingRollbackApiConn apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection, fullRollbackAllowed: false);
            ImportRollback component = RenderRollbackComponent(context);

            await InvokePrivateAsync(component, "FullMgmRollback");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.GetLastImportCallCount, Is.Zero);
                Assert.That(apiConnection.RollbackImportCallCount, Is.Zero);
                Assert.That(apiConnection.DeleteLatestConfigCallCount, Is.Zero);
            });
        }

        [Test]
        public async Task DeleteLatestConfigOfManagement_DoesNothing_WhenSettingIsDisabled()
        {
            RecordingRollbackApiConn apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection, fullRollbackAllowed: false);
            ImportRollback component = RenderRollbackComponent(context);

            await InvokePrivateAsync(component, "deleteLatestConfigOfManagement", 42);

            Assert.That(apiConnection.DeleteLatestConfigCallCount, Is.Zero);
        }

        [Test]
        public async Task FullMgmRollback_DeletesEveryImportAndLatestConfig_WhenSettingIsEnabled()
        {
            // FullMgmRollback queries getLastImport once for the initial count check and then again
            // inside the loop until no import id remains. Two loop iterations return an id (each
            // triggering a rollback), the third returns null to stop; afterwards the latest config
            // row of the management is removed.
            RecordingRollbackApiConn apiConnection = new()
            {
                RemainingImportIds = new Queue<long?>([99, 10, 9, null])
            };
            await using BunitContext context = CreateContext(apiConnection, fullRollbackAllowed: true);
            ImportRollback component = RenderRollbackComponent(context, managementId: 42);

            await InvokePrivateAsync(component, "FullMgmRollback");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.RollbackImportCallCount, Is.EqualTo(2));
                Assert.That(apiConnection.DeleteLatestConfigCallCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task IsRollbackDisabled_BlocksFullRollback_WhenNoIncompleteImportAndSettingDisabled()
        {
            bool disabled = await EvaluateRollbackDisabled(fullRollbackAllowed: false, hasIncompleteImport: false);

            Assert.That(disabled, Is.True);
        }

        [Test]
        public async Task IsRollbackDisabled_AllowsFullRollback_WhenNoIncompleteImportButSettingEnabled()
        {
            bool disabled = await EvaluateRollbackDisabled(fullRollbackAllowed: true, hasIncompleteImport: false);

            Assert.That(disabled, Is.False);
        }

        [Test]
        public async Task IsRollbackDisabled_AllowsSingleRollback_WhenIncompleteImportExistsRegardlessOfSetting()
        {
            // an incomplete import can always be rolled back (single-import rollback), independent of
            // the full-rollback setting.
            bool disabledWhenSettingOff = await EvaluateRollbackDisabled(fullRollbackAllowed: false, hasIncompleteImport: true);
            bool disabledWhenSettingOn = await EvaluateRollbackDisabled(fullRollbackAllowed: true, hasIncompleteImport: true);

            Assert.Multiple(() =>
            {
                Assert.That(disabledWhenSettingOff, Is.False);
                Assert.That(disabledWhenSettingOn, Is.False);
            });
        }

        private static Task<bool> EvaluateRollbackDisabled(bool fullRollbackAllowed, bool hasIncompleteImport)
        {
            // the full page render pulls in BlazorTable, which needs localization services that are
            // irrelevant to this guard. The guard reads only userConfig, so the component is created
            // directly and the injected userConfig is set via reflection.
            SimulatedGlobalConfig globalConfig = new() { AllowFullRollback = fullRollbackAllowed };
            using UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);

            MonitorImportStatus component = new();
            SetInjectedMember(component, "userConfig", userConfig);

            ImportStatus status = new()
            {
                MgmId = 1,
                LastIncompleteImport = hasIncompleteImport ? [new ImportControl { ControlId = 5 }] : []
            };

            MethodInfo method = typeof(MonitorImportStatus).GetMethod("IsRollbackDisabled", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(MonitorImportStatus).FullName, "IsRollbackDisabled");
            return Task.FromResult((bool)method.Invoke(component, [status])!);
        }

        private static void SetInjectedMember(object component, string memberName, object value)
        {
            Type type = component.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(component, value);
                return;
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(type.FullName, memberName);
            field.SetValue(component, value);
        }

        private static BunitContext CreateContext(ApiConnection apiConnection, bool fullRollbackAllowed)
        {
            SimulatedGlobalConfig globalConfig = new() { AllowFullRollback = fullRollbackAllowed };

            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RollbackAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton(apiConnection);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(UserConfig.ForTextOnly(globalConfig));
            return context;
        }

        private static ImportRollback RenderRollbackComponent(BunitContext context, int managementId = 1)
        {
            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, (_, _, _, _) => { })
                    .AddChildContent<ImportRollback>(rollback => rollback
                        .Add(p => p.ManagementId, managementId))));
            return wrapper.FindComponent<ImportRollback>().Instance;
        }

        private static async Task InvokePrivateAsync(object component, string methodName, params object?[]? args)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(component.GetType().FullName, methodName);
            Task task = (Task)(method.Invoke(component, args) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        private sealed class RollbackAuthStateProvider : AuthenticationStateProvider
        {
            private readonly AuthenticationState authenticationState;

            public RollbackAuthStateProvider(params string[] roles)
            {
                ClaimsIdentity identity = new(
                    roles.Select(role => new Claim(ClaimTypes.Role, role)),
                    authenticationType: "Test",
                    nameType: ClaimTypes.Name,
                    roleType: ClaimTypes.Role);
                authenticationState = new AuthenticationState(new ClaimsPrincipal(identity));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(authenticationState);
            }
        }

        private sealed class RecordingRollbackApiConn : SimulatedApiConnection
        {
            public int GetLastImportCallCount { get; private set; }
            public int RollbackImportCallCount { get; private set; }
            public int DeleteLatestConfigCallCount { get; private set; }

            // sequence of "latest import id" answers returned by getLastImport; a null id signals
            // that no more imports remain for the management.
            public Queue<long?> RemainingImportIds { get; init; } = new([null]);

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == MonitorQueries.getImportStatus && typeof(QueryResponseType) == typeof(List<ImportStatus>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ImportStatus>());
                }
                if (query == ImportQueries.getLastImport && typeof(QueryResponseType) == typeof(List<ImportControl>))
                {
                    GetLastImportCallCount++;
                    long? nextId = RemainingImportIds.Count > 0 ? RemainingImportIds.Dequeue() : null;
                    List<ImportControl> result = nextId == null ? [] : [new ImportControl { ControlId = nextId.Value }];
                    return Task.FromResult((QueryResponseType)(object)result);
                }
                if (query == ImportQueries.rollbackImport && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    RollbackImportCallCount++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }
                if (query == ImportQueries.deleteLatestConfigOfManagement && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    DeleteLatestConfigCallCount++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }
                throw new NotImplementedException($"Unexpected query: {query}");
            }
        }
    }
}
