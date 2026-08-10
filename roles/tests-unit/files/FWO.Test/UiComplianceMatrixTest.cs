using System.Reflection;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using FWO.Ui.Pages.Compliance;
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
            userConfig.User.Roles = new List<string> { Roles.Admin };
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

        private sealed class MatrixApiConnection : SimulatedApiConnection
        {
            private readonly List<ComplianceNetworkZone> networkZones = new()
            {
                new ComplianceNetworkZone { Id = 1, IdString = "manual-zone", Name = "Manual zone" }
            };

            public int AutoCalculatedInternetZoneAdditions { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ComplianceQueries.getMatrices && typeof(QueryResponseType) == typeof(List<ComplianceCriterion>))
                {
                    List<ComplianceCriterion> matrices = new()
                    {
                        new ComplianceCriterion { Id = 1, Name = "Manual matrix" }
                    };
                    return Task.FromResult((QueryResponseType)(object)matrices);
                }

                if (query == ComplianceQueries.getNetworkZonesForMatrix && typeof(QueryResponseType) == typeof(List<ComplianceNetworkZone>))
                {
                    return Task.FromResult((QueryResponseType)(object)networkZones);
                }

                if (query == ComplianceQueries.addNetworkZone)
                {
                    PropertyInfo? isInternetZoneProperty = variables?.GetType().GetProperty("isAutoCalculatedInternetZone");

                    if (isInternetZoneProperty?.GetValue(variables) is true)
                    {
                        AutoCalculatedInternetZoneAdditions++;
                        networkZones.Add(new ComplianceNetworkZone
                        {
                            Id = 2,
                            IdString = "AUTO_CALCULATED_ZONE_INTERNET",
                            Name = "Auto-calculated Internet Zone",
                            IsAutoCalculatedInternetZone = true
                        });
                    }

                    return Task.FromResult(default(QueryResponseType)!);
                }

                throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
            }
        }
    }
}
