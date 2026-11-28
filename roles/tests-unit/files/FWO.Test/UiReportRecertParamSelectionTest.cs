using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using FWO.Ui.Shared;
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
            context.Services.AddSingleton<ApiConnection>(new ReportRecertParamSelectionTestApiConn(
            [
                new() { Id = 11, Name = "App One" }
            ]));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = [Roles.Admin] } });

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
            context.Services.AddSingleton<ApiConnection>(new ReportRecertParamSelectionTestApiConn(
            [
                new() { Id = 11, Name = "App One" },
                new() { Id = 12, Name = "App Two" }
            ]));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = [Roles.Admin] } });

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
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = [Roles.Reporter] } });

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

        private static BunitContext CreateContext(ApiConnection apiConnection, SimulatedUserConfig userConfig)
        {
            BunitContext context = CreateContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton(apiConnection);
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<ReportRecertParamSelection> RenderSelection(BunitContext context, RecertFilter recertFilter,
            List<(Exception? Exception, string Title)>? messages = null, string filterInput = "", Action<string>? filterInputChanged = null)
        {
            return context.Render<ReportRecertParamSelection>(parameters => parameters
                .AddCascadingValue<Action<Exception?, string, string, bool>>((exception, title, _, _) => messages?.Add((exception, title)))
                .Add(p => p.RecertFilter, recertFilter)
                .Add(p => p.FilterInput, filterInput)
                .Add(p => p.FilterInputChanged, (string updated) => filterInputChanged?.Invoke(updated)));
        }

        private static FwoOwner? SelectedOwner(IRenderedComponent<ReportRecertParamSelection> component)
        {
            return component.FindComponent<Dropdown<FwoOwner>>().Instance.SelectedElement;
        }

        [Test]
        public async Task ReportRecertParamSelection_OwnerDropdownOffersAllAndTheOwnersByName()
        {
            await using BunitContext context = CreateContext(new ReportRecertParamSelectionTestApiConn(
                [new() { Id = 12, Name = "Zulu App" }, new() { Id = 11, Name = "Alpha App" }]), new SimulatedUserConfig { User = { Roles = [Roles.Admin] } });

            IRenderedComponent<ReportRecertParamSelection> component = RenderSelection(context, new RecertFilter());
            IRenderedComponent<Dropdown<FwoOwner>> dropdown = component.FindComponent<Dropdown<FwoOwner>>();
            dropdown.Find("input").Focus();

            List<string> items = [.. dropdown.FindAll("button.dropdown-item").Select(item => item.TextContent.Trim())];
            Assert.That(items, Is.SupersetOf((List<string>)[new SimulatedUserConfig().GetText("all"), "show_rules_without_owners", "Alpha App", "Zulu App"]));
            Assert.That(items.IndexOf("Alpha App"), Is.LessThan(items.IndexOf("Zulu App")));
            Assert.That(SelectedOwner(component)?.Id, Is.EqualTo(-1));
        }

        [Test]
        public async Task ReportRecertParamSelection_ATemplateWithOneOwnerPreselectsIt()
        {
            await using BunitContext context = CreateContext(new ReportRecertParamSelectionTestApiConn(), new SimulatedUserConfig { User = { Roles = [Roles.Admin] } });

            IRenderedComponent<ReportRecertParamSelection> component = RenderSelection(context, new RecertFilter { RecertOwnerList = [12] });

            Assert.That(SelectedOwner(component)?.Name, Is.EqualTo("App Two"));
        }

        [Test]
        public async Task ReportRecertParamSelection_ATemplateWithAllOwnersSelectsAll()
        {
            await using BunitContext context = CreateContext(new ReportRecertParamSelectionTestApiConn(), new SimulatedUserConfig { User = { Roles = [Roles.Admin] } });

            IRenderedComponent<ReportRecertParamSelection> component = RenderSelection(context, new RecertFilter { RecertOwnerList = [11, 12] });

            Assert.That(SelectedOwner(component)?.Id, Is.EqualTo(-1));
        }

        [Test]
        public async Task ReportRecertParamSelection_ATemplateWithAVanishedOwnerFallsBackToAll()
        {
            await using BunitContext context = CreateContext(new ReportRecertParamSelectionTestApiConn(), new SimulatedUserConfig { User = { Roles = [Roles.Admin] } });

            IRenderedComponent<ReportRecertParamSelection> component = RenderSelection(context, new RecertFilter { RecertOwnerList = [99] });

            Assert.That(SelectedOwner(component)?.Id, Is.EqualTo(-1));
        }

        [Test]
        public async Task ReportRecertParamSelection_AnAdminTemplateForRulesWithoutOwnersKeepsThatChoice()
        {
            await using BunitContext context = CreateContext(new ReportRecertParamSelectionTestApiConn(), new SimulatedUserConfig { User = { Roles = [Roles.Admin] } });
            RecertFilter recertFilter = new() { ShowRulesWithoutOwner = true };

            IRenderedComponent<ReportRecertParamSelection> component = RenderSelection(context, recertFilter);

            Assert.That(SelectedOwner(component)?.Id, Is.EqualTo(-3));
            Assert.That(recertFilter.ShowRulesWithoutOwner, Is.True);
        }

        [Test]
        public async Task ReportRecertParamSelection_ANonAdminTemplateForRulesWithoutOwnersIsReset()
        {
            SimulatedUserConfig userConfig = new() { User = { Roles = [Roles.Reporter], Ownerships = [11] } };
            await using BunitContext context = CreateContext(new ReportRecertParamSelectionTestApiConn(), userConfig);
            RecertFilter recertFilter = new() { ShowRulesWithoutOwner = true, RecertOwnerList = [11] };

            IRenderedComponent<ReportRecertParamSelection> component = RenderSelection(context, recertFilter);

            Assert.That(recertFilter.ShowRulesWithoutOwner, Is.False);
            Assert.That(recertFilter.RecertOwnerList, Is.Empty);
            Assert.That(SelectedOwner(component)?.Id, Is.EqualTo(-1));
        }

        [Test]
        public async Task ReportRecertParamSelection_ChoosingOneOwnerFiltersForIt()
        {
            await using BunitContext context = CreateContext(new ReportRecertParamSelectionTestApiConn(), new SimulatedUserConfig { User = { Roles = [Roles.Admin] } });
            RecertFilter recertFilter = new() { ShowRulesWithoutOwner = true };
            IRenderedComponent<ReportRecertParamSelection> component = RenderSelection(context, recertFilter);

            Dropdown<FwoOwner> dropdown = component.FindComponent<Dropdown<FwoOwner>>().Instance;
            await component.InvokeAsync(() => dropdown.SelectedElementChanged.InvokeAsync(new FwoOwner { Id = 12, Name = "App Two" }));

            Assert.That(recertFilter.RecertOwnerList, Is.EqualTo((List<int>)[12]));
            Assert.That(recertFilter.ShowRulesWithoutOwner, Is.False);
        }

        [Test]
        public async Task ReportRecertParamSelection_AnotherFilterObjectReinitializesTheOwner()
        {
            await using BunitContext context = CreateContext(new ReportRecertParamSelectionTestApiConn(), new SimulatedUserConfig { User = { Roles = [Roles.Admin] } });
            IRenderedComponent<ReportRecertParamSelection> component = RenderSelection(context, new RecertFilter());

            component.Render(parameters => parameters.Add(p => p.RecertFilter, new RecertFilter { RecertOwnerList = [11] }));

            Assert.That(SelectedOwner(component)?.Name, Is.EqualTo("App One"));
        }

        [Test]
        public async Task ReportRecertParamSelection_UnsettingAnyMatchOnAnEmptyFilterAddsTheExclusionAlone()
        {
            await using BunitContext context = CreateContext(new ReportRecertParamSelectionTestApiConn(), new SimulatedUserConfig { User = { Roles = [Roles.Admin] } });
            string? filterInput = null;
            IRenderedComponent<ReportRecertParamSelection> component = RenderSelection(context, new RecertFilter { RecertShowAnyMatch = true },
                filterInputChanged: updated => filterInput = updated);

            component.Find("#recertShowAnyMatch").Input(false);

            Assert.That(filterInput, Is.EqualTo("(not src==0.0.0.0 and not dst==0.0.0.0)"));
        }

        [Test]
        public async Task ReportRecertParamSelection_ADueDateOfZeroIsReplacedByTheConfiguredPeriod()
        {
            SimulatedUserConfig userConfig = new() { User = { Roles = [Roles.Admin] }, RecertificationDisplayPeriod = 45 };
            await using BunitContext context = CreateContext(new ReportRecertParamSelectionTestApiConn(), userConfig);
            RecertFilter recertFilter = new() { RecertificationDisplayPeriod = 0 };

            RenderSelection(context, recertFilter);

            Assert.That(recertFilter.RecertificationDisplayPeriod, Is.EqualTo(45));
        }

        [Test]
        public async Task ReportRecertParamSelection_WithoutAnyOwnerItSaysSo()
        {
            List<(Exception? Exception, string Title)> messages = [];
            SimulatedUserConfig userConfig = new() { User = { Roles = [Roles.Reporter] } };
            await using BunitContext context = CreateContext(new ReportRecertParamSelectionTestApiConn(), userConfig);

            RenderSelection(context, new RecertFilter(), messages);

            Assert.That(messages, Is.EqualTo(new List<(Exception?, string)> { (null, userConfig.GetText("no_owners")) }));
        }

        [Test]
        public async Task ReportRecertParamSelection_AFailingOwnerFetchIsReported()
        {
            List<(Exception? Exception, string Title)> messages = [];
            SimulatedUserConfig userConfig = new() { User = { Roles = [Roles.Admin] } };
            await using BunitContext context = CreateContext(new FailingOwnerApiConnection(), userConfig);

            RenderSelection(context, new RecertFilter(), messages);

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(messages[0].Title, Is.EqualTo(userConfig.GetText("object_fetch")));
        }

        private sealed class FailingOwnerApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                throw new InvalidOperationException("simulated api failure");
            }
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
            this.owners = owners ??
            [
                new() { Id = 11, Name = "App One" },
                new() { Id = 12, Name = "App Two" }
            ];
            this.ownerRecerts = ownerRecerts ?? [];
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
