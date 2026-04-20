using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiReportModellingParamSelectionTest
    {
        [Test]
        public void VarianceAnalysisCollapsesMultiOwnerSelectionToDisplayedOwner()
        {
            await using Bunit.TestContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new ReportModellingParamSelectionTestApiConn());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            FwoOwner firstOwner = new() { Id = 11, Name = "App One" };
            FwoOwner secondOwner = new() { Id = 12, Name = "App Two" };
            ModellingFilter modellingFilter = new()
            {
                SelectedOwners = [firstOwner, secondOwner]
            };

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportModellingParamSelection>(childParameters => childParameters
                    .Add(p => p.ModellingFilter, modellingFilter)
                    .Add(p => p.ReportType, ReportType.VarianceAnalysis)));

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(modellingFilter.SelectedOwners, Has.Count.EqualTo(1));
                Assert.That(modellingFilter.SelectedOwner.Id, Is.EqualTo(firstOwner.Id));
            });
        }
    }

    internal sealed class ReportModellingParamSelectionTestApiConn : SimulatedApiConnection
    {
        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
            string query,
            object? variables = null,
            string? operationName = null)
        {
            await DefaultInit.DoNothing();

            if (typeof(QueryResponseType) == typeof(List<FwoOwner>))
            {
                return (QueryResponseType)(object)new List<FwoOwner>
                {
                    new() { Id = 11, Name = "App One" },
                    new() { Id = 12, Name = "App Two" }
                };
            }

            if (typeof(QueryResponseType) == typeof(List<OwnerRecertification>))
            {
                return (QueryResponseType)(object)new List<OwnerRecertification>();
            }

            throw new NotImplementedException();
        }
    }
}
