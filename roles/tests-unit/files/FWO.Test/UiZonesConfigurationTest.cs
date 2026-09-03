using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using FWO.Ui.Pages.Compliance;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NetTools;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiZonesConfigurationTest
    {
        private static readonly IPAddressRange kZoneRange = IPAddressRange.Parse("192.0.2.0/24");
        private static readonly IPAddressRange[] kAutoCalculatedZoneRanges = [kZoneRange];

        private sealed class ZonesConfigurationApiConnection : SimulatedApiConnection
        {
            public List<string> SentQueries { get; } = [];
            public List<ComplianceNetworkZone> NetworkZones { get; } = [];
            public bool ThrowWhenLoadingNetworkZones { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                SentQueries.Add(query);
                if (typeof(QueryResponseType) == typeof(List<ComplianceNetworkZone>))
                {
                    if (ThrowWhenLoadingNetworkZones)
                    {
                        throw new InvalidOperationException("Unable to refresh special zones.");
                    }
                    return Task.FromResult((QueryResponseType)(object)NetworkZones);
                }
                return Task.FromResult(default(QueryResponseType)!);
            }
        }

        private static BunitContext CreateContext(NetworkZoneService networkZoneService, ZonesConfigurationApiConnection? apiConnection = null, GlobalConfig? globalConfig = null)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(apiConnection ?? new ZonesConfigurationApiConnection());
            context.Services.AddSingleton<UserConfig>(globalConfig == null ? new SimulatedUserConfig() : UserConfig.ForTextOnly(globalConfig));
            context.Services.AddSingleton(networkZoneService);
            context.Services.AddScoped<DomEventService>();
            return context;
        }

        private static IRenderedComponent<ZonesConfiguration> Render(BunitContext context, Func<Task>? configChanged = null)
        {
            return context.Render<ZonesConfiguration>(parameters => parameters
                .Add(p => p.SelectedMatrix, new ComplianceCriterion { Id = 1, Name = "matrix" })
                .Add(p => p.ReadonlyMode, false)
                .Add(p => p.ConfigChanged, configChanged ?? DefaultInit.DoNothing));
        }

        [Test]
        public async Task UpdatingZoneRefreshesAutoCalculatedInternetZone()
        {
            ComplianceNetworkZone normalZone = new() { Id = 1, CriterionId = 1, Name = "normal" };
            ComplianceNetworkZone internetZone = new()
            {
                Id = 2,
                CriterionId = 1,
                IsAutoCalculatedInternetZone = true,
                Name = "Auto-calculated Internet Zone"
            };
            NetworkZoneService networkZoneService = new()
            {
                NetworkZones = new List<ComplianceNetworkZone> { normalZone, internetZone }
            };
            ZonesConfigurationApiConnection apiConnection = new();
            apiConnection.NetworkZones.Add(normalZone);
            apiConnection.NetworkZones.Add(internetZone);
            SimulatedGlobalConfig globalConfig = new()
            {
                AutoCalculateInternetZone = true,
                AutoCalculateUndefinedInternalZone = false
            };
            await using BunitContext context = CreateContext(networkZoneService, apiConnection, globalConfig);
            IRenderedComponent<ZonesConfiguration> page = Render(context);

            networkZoneService.InvokeOnEditZone(normalZone);
            NetworkZoneService.AdditionsDeletions addDel = new();
            addDel.IpRangesToAdd.Add(kZoneRange);
            typeof(ZonesConfiguration)
                .GetField("addDel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(page.Instance, addDel);
            await page.InvokeAsync(async () =>
            {
                Task updateTask = (Task)typeof(ZonesConfiguration)
                    .GetMethod("ExecuteNetworkZoneModifications", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(page.Instance, null)!;
                await updateTask;
            });

            Assert.That(apiConnection.SentQueries.First(), Is.EqualTo(ComplianceQueries.updateNetworkZone));
            Assert.That(apiConnection.SentQueries, Does.Contain(ComplianceQueries.removeNetworkZone));
            Assert.That(apiConnection.SentQueries.Count(query => query == ComplianceQueries.addNetworkZone), Is.EqualTo(1));
        }

        [Test]
        public async Task UpdatingZoneWithoutAddressCoverageChangeDoesNotRefreshAutoCalculatedZones()
        {
            ComplianceNetworkZone normalZone = new() { Id = 1, CriterionId = 1, Name = "normal" };
            ComplianceNetworkZone internetZone = new()
            {
                Id = 2,
                CriterionId = 1,
                IsAutoCalculatedInternetZone = true,
                Name = "Auto-calculated Internet Zone"
            };
            NetworkZoneService networkZoneService = new()
            {
                NetworkZones = new List<ComplianceNetworkZone> { normalZone, internetZone }
            };
            ZonesConfigurationApiConnection apiConnection = new();
            apiConnection.NetworkZones.Add(normalZone);
            apiConnection.NetworkZones.Add(internetZone);
            SimulatedGlobalConfig globalConfig = new()
            {
                AutoCalculateInternetZone = true
            };
            await using BunitContext context = CreateContext(networkZoneService, apiConnection, globalConfig);
            IRenderedComponent<ZonesConfiguration> page = Render(context);

            networkZoneService.InvokeOnEditZone(normalZone);
            await page.InvokeAsync(async () =>
            {
                Task updateTask = (Task)typeof(ZonesConfiguration)
                    .GetMethod("ExecuteNetworkZoneModifications", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(page.Instance, null)!;
                await updateTask;
            });

            Assert.That(apiConnection.SentQueries, Is.EqualTo(new List<string> { ComplianceQueries.updateNetworkZone }));
        }

        [Test]
        public async Task EditingAutoCalculatedZoneHidesIpRangeControls()
        {
            ComplianceNetworkZone internetZone = new()
            {
                Id = 2,
                CriterionId = 1,
                IsAutoCalculatedInternetZone = true,
                Name = "Auto-calculated Internet Zone",
                IPRanges = kAutoCalculatedZoneRanges
            };
            NetworkZoneService networkZoneService = new()
            {
                NetworkZones = new List<ComplianceNetworkZone> { internetZone }
            };
            await using BunitContext context = CreateContext(networkZoneService);
            IRenderedComponent<ZonesConfiguration> page = Render(context);

            networkZoneService.InvokeOnEditZone(internetZone);

            page.WaitForAssertion(() =>
            {
                Assert.That(page.FindAll("#addIpAddress"), Is.Empty);
                Assert.That(page.FindAll(".badge.bg-danger"), Is.Empty);
            });
        }

        [Test]
        public async Task EditingEmptyAutoCalculatedZoneHidesSubzoneControls()
        {
            ComplianceNetworkZone undefinedInternalZone = new()
            {
                Id = 2,
                CriterionId = 1,
                IsAutoCalculatedUndefinedInternalZone = true,
                Name = "Auto-calculated Undefined-internal Zone"
            };
            NetworkZoneService networkZoneService = new()
            {
                NetworkZones = new List<ComplianceNetworkZone> { undefinedInternalZone }
            };
            await using BunitContext context = CreateContext(networkZoneService);
            IRenderedComponent<ZonesConfiguration> page = Render(context);

            networkZoneService.InvokeOnEditZone(undefinedInternalZone);

            page.WaitForAssertion(() => Assert.That(page.FindAll("#addSubzone"), Is.Empty));
        }

        [Test]
        public async Task UpdatingAutoCalculatedZoneWithAddressCoverageChangeDoesNotRefreshAutoCalculatedZones()
        {
            ComplianceNetworkZone undefinedInternalZone = new()
            {
                Id = 2,
                CriterionId = 1,
                IsAutoCalculatedUndefinedInternalZone = true,
                Name = "Auto-calculated Undefined-internal Zone"
            };
            NetworkZoneService networkZoneService = new()
            {
                NetworkZones = new List<ComplianceNetworkZone> { undefinedInternalZone }
            };
            ZonesConfigurationApiConnection apiConnection = new();
            apiConnection.NetworkZones.Add(undefinedInternalZone);
            SimulatedGlobalConfig globalConfig = new()
            {
                AutoCalculateInternetZone = true
            };
            await using BunitContext context = CreateContext(networkZoneService, apiConnection, globalConfig);
            IRenderedComponent<ZonesConfiguration> page = Render(context);

            networkZoneService.InvokeOnEditZone(undefinedInternalZone);
            NetworkZoneService.AdditionsDeletions addDel = new();
            addDel.SubzonesToAdd.Add(new ComplianceNetworkZone { Id = 3, CriterionId = 1, Name = "subzone" });
            typeof(ZonesConfiguration)
                .GetField("addDel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(page.Instance, addDel);
            await page.InvokeAsync(async () =>
            {
                Task updateTask = (Task)typeof(ZonesConfiguration)
                    .GetMethod("ExecuteNetworkZoneModifications", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(page.Instance, null)!;
                await updateTask;
            });

            Assert.That(apiConnection.SentQueries, Is.EqualTo(new List<string> { ComplianceQueries.updateNetworkZone }));
        }

        [Test]
        public async Task DeletingZoneDisplaysAutoCalculatedZoneCommunicationWarning()
        {
            ComplianceNetworkZone normalZone = new() { Id = 1, CriterionId = 1, Name = "normal" };
            NetworkZoneService networkZoneService = new()
            {
                NetworkZones = new List<ComplianceNetworkZone> { normalZone }
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                AutoCalculateInternetZone = true
            };
            await using BunitContext context = CreateContext(networkZoneService, globalConfig: globalConfig);
            IRenderedComponent<ZonesConfiguration> page = Render(context);

            networkZoneService.InvokeOnDeleteZone(normalZone);

            page.WaitForAssertion(() => Assert.That(page.FindAll(".alert-warning"), Has.Count.EqualTo(1)));
        }

        [Test]
        public async Task DeletingAutoCalculatedInternetZone_DoesNotRecreateIt()
        {
            ComplianceNetworkZone internetZone = new()
            {
                Id = 2,
                CriterionId = 1,
                IsAutoCalculatedInternetZone = true,
                Name = "Auto-calculated Internet Zone"
            };
            NetworkZoneService networkZoneService = new()
            {
                NetworkZones = new List<ComplianceNetworkZone> { internetZone }
            };
            ZonesConfigurationApiConnection apiConnection = new();
            apiConnection.NetworkZones.Add(internetZone);
            SimulatedGlobalConfig globalConfig = new()
            {
                AutoCalculateInternetZone = true
            };
            await using BunitContext context = CreateContext(networkZoneService, apiConnection, globalConfig);
            IRenderedComponent<ZonesConfiguration> page = Render(context);

            networkZoneService.InvokeOnDeleteZone(internetZone);
            await page.InvokeAsync(async () =>
            {
                Task deleteTask = (Task)typeof(ZonesConfiguration)
                    .GetMethod("ExecuteNetworkZoneDeletion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(page.Instance, null)!;
                await deleteTask;
            });

            Assert.That(apiConnection.SentQueries, Is.EqualTo(new List<string> { ComplianceQueries.removeNetworkZone }));
        }

        [Test]
        public async Task RecalculationFailureAfterZoneUpdate_ReloadsThePersistedState()
        {
            ComplianceNetworkZone normalZone = new() { Id = 1, CriterionId = 1, Name = "normal" };
            NetworkZoneService networkZoneService = new()
            {
                NetworkZones = new List<ComplianceNetworkZone> { normalZone }
            };
            ZonesConfigurationApiConnection apiConnection = new()
            {
                ThrowWhenLoadingNetworkZones = true
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                AutoCalculateInternetZone = true
            };
            int configChangedCalls = 0;
            await using BunitContext context = CreateContext(networkZoneService, apiConnection, globalConfig);
            IRenderedComponent<ZonesConfiguration> page = Render(context, () =>
            {
                configChangedCalls++;
                return Task.CompletedTask;
            });

            networkZoneService.InvokeOnEditZone(normalZone);
            await page.InvokeAsync(async () =>
            {
                Task updateTask = (Task)typeof(ZonesConfiguration)
                    .GetMethod("ExecuteNetworkZoneModifications", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(page.Instance, null)!;
                await updateTask;
            });

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.SentQueries.First(), Is.EqualTo(ComplianceQueries.updateNetworkZone));
                Assert.That(configChangedCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task RecalculationFailureAfterZoneDeletion_ReloadsThePersistedState()
        {
            ComplianceNetworkZone normalZone = new() { Id = 1, CriterionId = 1, Name = "normal" };
            NetworkZoneService networkZoneService = new()
            {
                NetworkZones = new List<ComplianceNetworkZone> { normalZone }
            };
            ZonesConfigurationApiConnection apiConnection = new()
            {
                ThrowWhenLoadingNetworkZones = true
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                AutoCalculateInternetZone = true
            };
            int configChangedCalls = 0;
            await using BunitContext context = CreateContext(networkZoneService, apiConnection, globalConfig);
            IRenderedComponent<ZonesConfiguration> page = Render(context, () =>
            {
                configChangedCalls++;
                return Task.CompletedTask;
            });

            networkZoneService.InvokeOnDeleteZone(normalZone);
            await page.InvokeAsync(async () =>
            {
                Task deleteTask = (Task)typeof(ZonesConfiguration)
                    .GetMethod("ExecuteNetworkZoneDeletion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(page.Instance, null)!;
                await deleteTask;
            });

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.SentQueries.First(), Is.EqualTo(ComplianceQueries.removeNetworkZone));
                Assert.That(configChangedCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task SubscribesToTheZoneEventsWhileItIsRendered()
        {
            NetworkZoneService networkZoneService = new();
            await using BunitContext context = CreateContext(networkZoneService);

            Render(context);

            // proves the probe below detects an attached handler, so the dispose tests are meaningful
            Assert.Multiple(() =>
            {
                Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnEditZone)), Is.False);
                Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnDeleteZone)), Is.False);
            });
            Assert.DoesNotThrow(() => networkZoneService.InvokeOnEditZone(new ComplianceNetworkZone { Id = 5 }));
        }

        [Test]
        public async Task DisposingDetachesTheEditZoneHandler()
        {
            NetworkZoneService networkZoneService = new();
            await using BunitContext context = CreateContext(networkZoneService);
            IRenderedComponent<ZonesConfiguration> page = Render(context);

            page.Instance.Dispose();

            // an attached handler would still run against the disposed component and re-render it
            Assert.DoesNotThrow(() => networkZoneService.InvokeOnEditZone(new ComplianceNetworkZone { Id = 5 }));
            Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnEditZone)), Is.True);
        }

        [Test]
        public async Task DisposingDetachesTheDeleteZoneHandler()
        {
            NetworkZoneService networkZoneService = new();
            await using BunitContext context = CreateContext(networkZoneService);
            IRenderedComponent<ZonesConfiguration> page = Render(context);

            page.Instance.Dispose();

            Assert.DoesNotThrow(() => networkZoneService.InvokeOnDeleteZone(new ComplianceNetworkZone { Id = 5 }));
            Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnDeleteZone)), Is.True);
        }

        [Test]
        public async Task RenderingThePageRepeatedlyDoesNotAccumulateHandlers()
        {
            // this is the actual leak: the service outlives the page, so every visit used to leave
            // another handler behind that kept the disposed page and its zone data alive
            NetworkZoneService networkZoneService = new();
            await using BunitContext context = CreateContext(networkZoneService);

            for (int visit = 0; visit < 3; visit++)
            {
                IRenderedComponent<ZonesConfiguration> page = Render(context);
                page.Instance.Dispose();
            }

            Assert.Multiple(() =>
            {
                Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnEditZone)), Is.True);
                Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnDeleteZone)), Is.True);
            });
        }

        private static bool HasNoSubscribers(NetworkZoneService service, string eventName)
        {
            System.Reflection.FieldInfo? field = typeof(NetworkZoneService)
                .GetField(eventName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return field?.GetValue(service) is not Delegate handler || handler.GetInvocationList().Length == 0;
        }
    }
}
