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

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiReportRecertParamSelectionTest
    {
        [Test]
        public async Task ReportRecertParamSelection_HidesOwnerAndAnyMatchFieldsForOwnerRecertification()
        {
            await using BunitContext context = CreateContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new ReportRecertParamSelectionTestApiConn(new List<FwoOwner>
            {
                new() { Id = 11, Name = "App One" }
            }));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = new List<string> { Roles.Admin } } });

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportRecertParamSelection>(childParameters => childParameters
                    .Add(p => p.ReportType, ReportType.OwnerRecertification)
                    .Add(p => p.UseFormLayout, true)));

            wrapper.WaitForAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(wrapper.Markup, Does.Contain("due_within"));
                    Assert.That(wrapper.Markup, Does.Not.Contain("show_any_match"));
                    Assert.That(wrapper.Markup, Does.Not.Contain("recertShowAnyMatch"));
                });
            });
        }

        [Test]
        public async Task ReportRecertParamSelection_HandlesSpecialOwnerSelectionsAndAnyMatchToggle()
        {
            await using BunitContext context = CreateContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new ReportRecertParamSelectionTestApiConn(new List<FwoOwner>
            {
                new() { Id = 11, Name = "App One" },
                new() { Id = 12, Name = "App Two" }
            }));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = new List<string> { Roles.Admin } } });

            RecertFilter recertFilter = new()
            {
                RecertShowAnyMatch = true
            };
            string? latestFilterInput = null;

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportRecertParamSelection>(childParameters => childParameters
                    .Add(p => p.RecertFilter, recertFilter)
                    .Add(p => p.FilterInput, "src==1")
                    .Add(p => p.FilterInputChanged, updated => latestFilterInput = updated)));

            wrapper.WaitForAssertion(() =>
            {
                ReportRecertParamSelection component = wrapper.FindComponent<ReportRecertParamSelection>().Instance;
                List<FwoOwner> ownerList = GetPrivateMember<List<FwoOwner>>(component, "ownerList");

                Assert.Multiple(() =>
                {
                    Assert.That(ownerList.Select(owner => owner.Id), Does.Contain(-1));
                    Assert.That(ownerList.Select(owner => owner.Id), Does.Contain(-3));
                    Assert.That(wrapper.Markup, Does.Contain("show_any_match"));
                });
            });

            ReportRecertParamSelection reportRecertParamSelection = wrapper.FindComponent<ReportRecertParamSelection>().Instance;

            InvokePrivateMethod(reportRecertParamSelection, "SelectedOwnerChanged", new FwoOwner { Id = -1 });
            Assert.That(recertFilter.RecertOwnerList, Is.EqualTo(new List<int> { 11, 12 }));
            Assert.That(recertFilter.ShowRulesWithoutOwner, Is.False);

            InvokePrivateMethod(reportRecertParamSelection, "SelectedOwnerChanged", new FwoOwner { Id = -2 });
            Assert.That(recertFilter.RecertOwnerList, Is.Empty);

            InvokePrivateMethod(reportRecertParamSelection, "SelectedOwnerChanged", new FwoOwner { Id = -3 });
            Assert.That(recertFilter.ShowRulesWithoutOwner, Is.True);
            Assert.That(recertFilter.RecertOwnerList, Is.Empty);

            await (Task)InvokePrivateMethod(reportRecertParamSelection, "ToggleShowAnyMatch")!;
            Assert.Multiple(() =>
            {
                Assert.That(recertFilter.RecertShowAnyMatch, Is.False);
                Assert.That(latestFilterInput, Is.EqualTo("src==1 and (not src==0.0.0.0 and not dst==0.0.0.0)"));
            });

            await (Task)InvokePrivateMethod(reportRecertParamSelection, "ToggleShowAnyMatch")!;
            Assert.Multiple(() =>
            {
                Assert.That(recertFilter.RecertShowAnyMatch, Is.True);
                Assert.That(latestFilterInput, Is.EqualTo("src==1 "));
            });
        }

        [Test]
        public async Task ReportRecertParamSelection_DisallowedRulesWithoutOwnersSelectionIsCleared()
        {
            await using BunitContext context = CreateContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Reporter));
            context.Services.AddSingleton<ApiConnection>(new ReportRecertParamSelectionTestApiConn());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = new List<string> { Roles.Reporter } } });

            RecertFilter recertFilter = new()
            {
                ShowRulesWithoutOwner = true
            };

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportRecertParamSelection>(childParameters => childParameters
                    .Add(p => p.RecertFilter, recertFilter)));

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.Markup, Does.Contain("recert_parameter"));
            });

            ReportRecertParamSelection reportRecertParamSelection = wrapper.FindComponent<ReportRecertParamSelection>().Instance;
            InvokePrivateMethod(reportRecertParamSelection, "SelectedOwnerChanged", new FwoOwner { Id = -3 });

            Assert.Multiple(() =>
            {
                Assert.That(recertFilter.ShowRulesWithoutOwner, Is.False);
                Assert.That(recertFilter.RecertOwnerList, Is.Empty);
            });
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.Services.AddLocalization();
            return context;
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

        private static T GetPrivateMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T)property.GetValue(instance)!;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(instance)!;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }
    }

    internal sealed class ReportRecertParamSelectionTestApiConn : SimulatedApiConnection
    {
        private readonly List<FwoOwner> owners;
        private readonly List<OwnerRecertification> ownerRecerts;

        public ReportRecertParamSelectionTestApiConn(List<FwoOwner>? owners = null, List<OwnerRecertification>? ownerRecerts = null)
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
                if (query == OwnerQueries.getOwners || query == OwnerQueries.getEditableOwners)
                {
                    return Task.FromResult((QueryResponseType)(object)owners.ToList());
                }
            }

            if (typeof(QueryResponseType) == typeof(List<OwnerRecertification>) && query == RecertQueries.getOwnerRecerts)
            {
                return Task.FromResult((QueryResponseType)(object)ownerRecerts.ToList());
            }

            throw new NotImplementedException();
        }
    }
}
