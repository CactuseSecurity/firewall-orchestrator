using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiReportModellingParamSelectionTest
    {
        [Test]
        public async Task VarianceAnalysisCollapsesMultiOwnerSelectionToDisplayedOwner()
        {
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new ReportModellingParamSelectionTestApiConn());
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = [Roles.Admin] } });

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

        [Test]
        public async Task OwnerRecertificationRendersNestedOwnerRecertSelectionAndCopiesLoadedOwners()
        {
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new ReportModellingParamSelectionTestApiConn(new List<FwoOwner>
            {
                new() { Id = 11, Name = "App One" },
                new() { Id = 12, Name = "App Two" }
            }));
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = new List<string> { Roles.Admin } } });

            ModellingFilter modellingFilter = new();

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportModellingParamSelection>(childParameters => childParameters
                    .Add(p => p.ModellingFilter, modellingFilter)
                    .Add(p => p.ReportType, ReportType.OwnerRecertification)));

            wrapper.WaitForAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(modellingFilter.SelectedOwners, Has.Count.EqualTo(2));
                    Assert.That(modellingFilter.SelectedOwners[0].Name, Is.EqualTo("App One"));
                    Assert.That(wrapper.Markup, Does.Contain("ownerLabel-summary"));
                });
            });
        }

        [Test]
        public async Task AppRulesBranchRendersRuleFiltersAndKeepsTheLoadedOwnerSelected()
        {
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new ReportModellingParamSelectionTestApiConn(new List<FwoOwner>
            {
                new() { Id = 11, Name = "Solo Owner" }
            }));
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = new List<string> { Roles.Admin } } });

            ModellingFilter modellingFilter = new();

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportModellingParamSelection>(childParameters => childParameters
                    .Add(p => p.ModellingFilter, modellingFilter)
                    .Add(p => p.ReportType, ReportType.AppRules)));

            wrapper.WaitForAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(modellingFilter.SelectedOwners, Has.Count.EqualTo(1));
                    Assert.That(modellingFilter.SelectedOwner.Id, Is.EqualTo(11));
                    Assert.That(wrapper.Markup, Does.Contain("appRuleSrcMatch"));
                    Assert.That(wrapper.Markup, Does.Contain("appRuleShowFull"));
                });
            });
        }

        [Test]
        public async Task RecertEventReportBranchShowsDisabledNoRecertsInput()
        {
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new ReportModellingParamSelectionTestApiConn(new List<FwoOwner>
            {
                new() { Id = 11, Name = "Solo Owner" }
            }, new List<OwnerRecertification>()));
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = new List<string> { Roles.Admin } } });

            ModellingFilter modellingFilter = new();

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportModellingParamSelection>(childParameters => childParameters
                    .Add(p => p.ModellingFilter, modellingFilter)
                    .Add(p => p.ReportType, ReportType.RecertEventReport)));

            wrapper.WaitForAssertion(() =>
            {
                wrapper.Find("input[placeholder=\"no_recerts\"]");
            });
        }

        [Test]
        public async Task RecertEventReportOwnerRecertSelectionUpdatesRecertAndTimeFilters()
        {
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new ReportModellingParamSelectionTestApiConn(new List<FwoOwner>
            {
                new() { Id = 11, Name = "Solo Owner" }
            }, new List<OwnerRecertification>
            {
                new() { Id = 77, ReportId = 88, RecertDate = new DateTime(2026, 1, 2) }
            }));
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = new List<string> { Roles.Admin } } });

            ModellingFilter modellingFilter = new()
            {
                SelectedOwner = new FwoOwner { Id = 11, Name = "Solo Owner" }
            };
            TimeFilter timeFilter = new();

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportModellingParamSelection>(childParameters => childParameters
                    .Add(p => p.ModellingFilter, modellingFilter)
                    .Add(p => p.TimeFilter, timeFilter)
                    .Add(p => p.ReportType, ReportType.RecertEventReport)));

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.Markup.ToLowerInvariant(), Does.Contain("recertification"));
            });

            ReportModellingParamSelection component = wrapper.FindComponent<ReportModellingParamSelection>().Instance;
            InvokePrivateMethod(component, "OwnerRecertChanged", new OwnerRecertification
            {
                Id = 77,
                ReportId = 88,
                RecertDate = new DateTime(2026, 1, 2)
            });

            Assert.Multiple(() =>
            {
                Assert.That(modellingFilter.OwnerRecertId, Is.EqualTo(77));
                Assert.That(modellingFilter.ReportId, Is.EqualTo(88));
                Assert.That(timeFilter.ReportTime, Is.EqualTo(new DateTime(2026, 1, 2)));
            });
        }

        private static object? InvokePrivateMethod(object instance, string methodName, params object?[] args)
        {
            MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new MissingMethodException(instance.GetType().FullName, methodName);
            }

            return method.Invoke(instance, args);
        }
    }

    internal sealed class ReportModellingParamSelectionTestApiConn : SimulatedApiConnection
    {
        private readonly List<FwoOwner> owners;
        private readonly List<OwnerRecertification> ownerRecerts;

        public ReportModellingParamSelectionTestApiConn(List<FwoOwner>? owners = null, List<OwnerRecertification>? ownerRecerts = null)
        {
            this.owners = owners ?? new List<FwoOwner>
            {
                new() { Id = 11, Name = "App One" },
                new() { Id = 12, Name = "App Two" }
            };
            this.ownerRecerts = ownerRecerts ?? new List<OwnerRecertification>();
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<FwoOwner>))
            {
                return Task.FromResult((QueryResponseType)(object)owners.ToList());
            }

            if (typeof(QueryResponseType) == typeof(List<OwnerRecertification>))
            {
                return Task.FromResult((QueryResponseType)(object)ownerRecerts.ToList());
            }

            throw new NotImplementedException();
        }
    }
}
