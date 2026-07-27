using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Report.Filter.FilterTypes;
using FWO.Middleware.Client;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json;
using ReportPage = FWO.Ui.Pages.Reporting.Report;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportPageTest
    {
        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("report_type", "Report type");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_time", "Report time");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_filters", "Report filters");
            SimulatedUserConfig.DummyTranslate.TryAdd("rule_filters", "Rule filters");
            SimulatedUserConfig.DummyTranslate.TryAdd("owner", "Owner");
            SimulatedUserConfig.DummyTranslate.TryAdd("all", "All");
            SimulatedUserConfig.DummyTranslate.TryAdd("generate_report", "Generate report");
            SimulatedUserConfig.DummyTranslate.TryAdd("save_as_template", "Save as template");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_duration", "Report duration");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_elements", "Report elements");
            SimulatedUserConfig.DummyTranslate.TryAdd("seconds", "seconds");
            SimulatedUserConfig.DummyTranslate.TryAdd("minutes", "minutes");
            SimulatedUserConfig.DummyTranslate.TryAdd("devices", "Devices");
            SimulatedUserConfig.DummyTranslate.TryAdd("managements", "Managements");
            SimulatedUserConfig.DummyTranslate.TryAdd("change", "change");
            SimulatedUserConfig.DummyTranslate.TryAdd("stop_fetching", "Stop fetching");
            SimulatedUserConfig.DummyTranslate.TryAdd("filter", "Filter");
            SimulatedUserConfig.DummyTranslate.TryAdd("object_fetch", "Object fetch");
        }

        [Test]
        public async Task ReportInitialization_SelectsFirstVisibleRuleReport()
        {
            TrackingReportPageApiConnection apiConnection = new(
                CreateManagements(),
                new List<FwoOwner>(),
                new List<FwoOwner>());

            await using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Reporter),
                new List<string> { Roles.Reporter },
                apiConnection,
                new List<ReportType>
                {
                    ReportType.Owners,
                    ReportType.AppRules,
                    ReportType.Rules
                },
                out _);

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderReport(context, null);

            wrapper.WaitForAssertion(() =>
            {
                ReportPage component = wrapper.FindComponent<ReportPage>().Instance;
                List<ReportType> availableReportTypes = GetPrivateProperty<List<ReportType>>(component, "availableReportTypes");
                ReportFilters reportFilters = GetPrivateField<ReportFilters>(component, "actReportFilters");

                Assert.Multiple(() =>
                {
                    Assert.That(availableReportTypes, Has.Count.EqualTo(1));
                    Assert.That(availableReportTypes[0], Is.EqualTo(ReportType.Rules));
                    Assert.That(reportFilters.ReportType, Is.EqualTo(ReportType.Rules));
                    Assert.That(GetPrivateField<bool>(component, "_showRightSidebar"), Is.True);
                    Assert.That(GetPrivateField<int>(component, "sidebarRightWidth"), Is.EqualTo(GlobalConst.kSidebarRightWidth));
                    Assert.That(wrapper.Markup, Does.Contain("Generate report"));
                    Assert.That(apiConnection.DeviceQueryCalls, Is.EqualTo(1));
                    Assert.That(apiConnection.EditableOwnerQueryCalls, Is.EqualTo(0));
                });
            });
        }

        [Test]
        public async Task ReportAppRulesSelection_CleansAppIdAndRestoresDeviceSelection()
        {
            TrackingReportPageApiConnection apiConnection = new(
                CreateManagements(),
                new List<FwoOwner>(),
                new List<FwoOwner>
                {
                    new FwoOwner { Id = 41, Name = "Owned app" }
                });

            await using BunitContext context = CreateContext(
                new MonitoringTestAuthStateProvider(Roles.Admin),
                new List<string> { Roles.Admin },
                apiConnection,
                new List<ReportType>
                {
                    ReportType.Rules,
                    ReportType.AppRules,
                    ReportType.Owners
                },
                out _);

            NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
            navigationManager.NavigateTo("http://localhost/report/generation/17");

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderReport(context, "17");

            wrapper.WaitForAssertion(() =>
            {
                ReportPage component = wrapper.FindComponent<ReportPage>().Instance;
                ReportFilters reportFilters = GetPrivateField<ReportFilters>(component, "actReportFilters");
                Assert.That(reportFilters.ReportType, Is.EqualTo(ReportType.Rules));
            });

            ReportPage report = wrapper.FindComponent<ReportPage>().Instance;
            SetPrivateField(report, "currentReport", new DummyReport(ReportType.Rules)
            {
                ReportData = new ReportData
                {
                    ElementsCount = 5
                }
            });

            InvokePrivateMethod(report, "ReportTypeChanged", ReportType.AppRules);

            ReportFilters appRulesFilters = GetPrivateField<ReportFilters>(report, "actReportFilters");
            ManagementSelect management = appRulesFilters.DeviceFilter.Managements[0];

            Assert.Multiple(() =>
            {
                Assert.That(appRulesFilters.ReportType, Is.EqualTo(ReportType.AppRules));
                Assert.That(appRulesFilters.DeviceFilter.IsAnyDeviceFilterSet(), Is.True);
                Assert.That(appRulesFilters.SelectAll, Is.False);
                Assert.That(management.Selected, Is.True);
                Assert.That(management.Devices.All(device => device.Selected), Is.True);
                Assert.That(GetPrivateField<bool>(report, "resetToEmptyDevFilter"), Is.True);
                Assert.That(GetPrivateField<bool>(report, "_showRightSidebar"), Is.True);
                Assert.That(GetPrivateField<int>(report, "sidebarRightWidth"), Is.EqualTo(GlobalConst.kSidebarRightWidth));
                Assert.That(GetPrivateField<int?>(report, "injectedAppId"), Is.Null);
                Assert.That(((DummyReport)GetPrivateField<ReportBase?>(report, "currentReport")!).ReportData.ElementsCount, Is.EqualTo(0));
                Assert.That(navigationManager.Uri, Does.EndWith("/report/generation"));
                Assert.That(apiConnection.DeviceQueryCalls, Is.EqualTo(1));
                Assert.That(apiConnection.EditableOwnerQueryCalls, Is.EqualTo(1));
                Assert.That(management.Devices.TrueForAll(device => device.Selected), Is.True);
            });

            InvokePrivateMethod(report, "ReportTypeChanged", ReportType.Owners);

            ReportFilters ownerFilters = GetPrivateField<ReportFilters>(report, "actReportFilters");

            Assert.Multiple(() =>
            {
                Assert.That(ownerFilters.DeviceFilter.IsAnyDeviceFilterSet(), Is.False);
                Assert.That(ownerFilters.SelectAll, Is.True);
                Assert.That(GetPrivateField<bool>(report, "resetToEmptyDevFilter"), Is.False);
                Assert.That(GetPrivateField<bool>(report, "_showRightSidebar"), Is.False);
                Assert.That(GetPrivateField<int>(report, "sidebarRightWidth"), Is.EqualTo(0));
            });
        }

        private static BunitContext CreateContext(
            AuthenticationStateProvider authStateProvider,
            List<string> roles,
            ApiConnection apiConnection,
            List<ReportType> availableReportTypes,
            out SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton(authStateProvider);
            context.Services.AddSingleton(apiConnection);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<IRuleTreeBuilder, NoOpRuleTreeBuilder>();

            userConfig = new SimulatedUserConfig
            {
                User =
                {
                    DbId = 50,
                    Language = "English",
                    Roles = new List<string>(roles)
                },
                AvailableReportTypes = JsonSerializer.Serialize(availableReportTypes)
            };
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderReport(BunitContext context, string? appId)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<ReportPage>(childParameters =>
                {
                    if (appId != null)
                    {
                        childParameters.Add(p => p.AppId, appId);
                    }
                }));
        }

        private static List<ManagementSelect> CreateManagements()
        {
            return new List<ManagementSelect>
            {
                new ManagementSelect
                {
                    Id = 1,
                    Name = "Management A",
                    Devices = new List<DeviceSelect>
                    {
                        new DeviceSelect { Id = 11, Name = "Device 11" },
                        new DeviceSelect { Id = 12, Name = "Device 12" }
                    }
                }
            };
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(instance.GetType().FullName, fieldName);
            }

            return (T)field.GetValue(instance)!;
        }

        private static T GetPrivateProperty<T>(object instance, string propertyName)
        {
            PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(instance.GetType().FullName, propertyName);
            }

            return (T)property.GetValue(instance)!;
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

        private static void SetPrivateField(object instance, string fieldName, object? value)
        {
            FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(instance.GetType().FullName, fieldName);
            }

            field.SetValue(instance, value);
        }
    }

    internal sealed class DummyReport : ReportBase
    {
        public DummyReport(ReportType reportType)
            : base(new DynGraphqlQuery("dummy-report"), new SimulatedUserConfig(), reportType)
        {
        }

        public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public override string ExportToCsv()
        {
            return string.Empty;
        }

        public override string ExportToJson()
        {
            return string.Empty;
        }

        public override string ExportToHtml()
        {
            return string.Empty;
        }

        public override string SetDescription()
        {
            return string.Empty;
        }
    }

    internal sealed class NoOpRuleTreeBuilder : IRuleTreeBuilder
    {
        public RuleTreeItem RuleTree { get; set; } = new();

        public Dictionary<(int managementId, int deviceId), RuleTreeItem> RuleTreeCache { get; set; } = new();

        public Dictionary<RuleTreeItem, Rule[]> FlattenedRules { get; set; } = new();

        public List<Rule> BuildRuleTree(RulebaseReport[] rulebases, RulebaseLink[] links, int managementId, int deviceId, bool suppressEmptyHeaders = false)
        {
            _ = rulebases;
            _ = links;
            _ = managementId;
            _ = deviceId;
            _ = suppressEmptyHeaders;
            return new List<Rule>();
        }

        public void ClearCachedRuleTrees()
        {
        }
    }

    internal sealed class TrackingReportPageApiConnection : SimulatedApiConnection
    {
        private readonly List<ManagementSelect> managements;
        private readonly List<FwoOwner> editableOwners;
        private readonly List<FwoOwner> owners;

        public int DeviceQueryCalls { get; private set; }
        public int EditableOwnerQueryCalls { get; private set; }
        public int OwnerQueryCalls { get; private set; }

        public TrackingReportPageApiConnection(List<ManagementSelect> managements, List<FwoOwner> editableOwners, List<FwoOwner> owners)
        {
            this.managements = managements;
            this.editableOwners = editableOwners;
            this.owners = owners;
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            _ = variables;
            _ = operationName;
            _ = chunkingOptions;

            if (typeof(QueryResponseType) == typeof(List<ManagementSelect>) && query == DeviceQueries.getDevicesByManagement)
            {
                DeviceQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)CloneManagements(managements));
            }

            if (typeof(QueryResponseType) == typeof(ReportTemplate[]) && query == ReportQueries.getReportTemplates)
            {
                return Task.FromResult((QueryResponseType)(object)Array.Empty<ReportTemplate>());
            }

            if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getEditableOwners)
            {
                EditableOwnerQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>(editableOwners));
            }

            if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getOwners)
            {
                OwnerQueryCalls++;
                return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>(owners));
            }

            throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
        }

        private static List<ManagementSelect> CloneManagements(List<ManagementSelect> source)
        {
            List<ManagementSelect> clone = new();
            foreach (ManagementSelect management in source)
            {
                ManagementSelect clonedManagement = new()
                {
                    Id = management.Id,
                    Name = management.Name,
                    Uid = management.Uid,
                    IsSuperManager = management.IsSuperManager,
                    Visible = management.Visible,
                    Selected = management.Selected,
                    Shared = management.Shared
                };

                foreach (DeviceSelect device in management.Devices)
                {
                    clonedManagement.Devices.Add(new DeviceSelect
                    {
                        Id = device.Id,
                        Name = device.Name,
                        Visible = device.Visible,
                        Selected = device.Selected,
                        Shared = device.Shared
                    });
                }

                clone.Add(clonedManagement);
            }

            return clone;
        }
    }
}
