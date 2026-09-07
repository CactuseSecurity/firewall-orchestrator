using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages.Reporting.Reports;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportingReportsTest
    {
        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate["common_service"] = "Common service";
            SimulatedUserConfig.DummyTranslate["connections"] = "Connections";
            SimulatedUserConfig.DummyTranslate["interfaces"] = "Interfaces";
            SimulatedUserConfig.DummyTranslate["own_common_services"] = "Own common services";
            SimulatedUserConfig.DummyTranslate["global_common_services"] = "Global common services";
            SimulatedUserConfig.DummyTranslate["recertification"] = "Recertification";
            SimulatedUserConfig.DummyTranslate["recertified_rules"] = "Recertified rules";
            SimulatedUserConfig.DummyTranslate["app_roles_not_implemented"] = "App roles not implemented";
            SimulatedUserConfig.DummyTranslate["app_roles_with_diffs"] = "App roles with diffs";
            SimulatedUserConfig.DummyTranslate["connections_not_implemented"] = "Connections not implemented";
            SimulatedUserConfig.DummyTranslate["connections_with_diffs"] = "Connections with diffs";
            SimulatedUserConfig.DummyTranslate["rules_for_deleted_conns"] = "Rules for deleted connections";
            SimulatedUserConfig.DummyTranslate["rules_not_modelled"] = "Rules not modelled";
            SimulatedUserConfig.DummyTranslate["app_roles"] = "App roles";
            SimulatedUserConfig.DummyTranslate["fully_modelled"] = "Fully modelled";
            SimulatedUserConfig.DummyTranslate["implemented"] = "Implemented";
            SimulatedUserConfig.DummyTranslate["not_implemented"] = "Not implemented";
            SimulatedUserConfig.DummyTranslate["with_diffs"] = "With diffs";
            SimulatedUserConfig.DummyTranslate["missing_app_servers"] = "Missing app servers";
            SimulatedUserConfig.DummyTranslate["surplus_app_servers"] = "Surplus app servers";
            SimulatedUserConfig.DummyTranslate["app_servers"] = "App servers";
            SimulatedUserConfig.DummyTranslate["id"] = "Id";
            SimulatedUserConfig.DummyTranslate["name"] = "Name";
        }

        [Test]
        public void ConnectionsReport_RendersOnlyPopulatedSections()
        {
            using BunitContext context = CreateContext();
            OwnerConnectionReport ownerReport = new()
            {
                Owner = new FwoOwner { Name = "Application One", ExtAppId = "APP-1" },
                RegularConnections = CreateConnections("Regular connection"),
                Interfaces = CreateConnections("Interface connection"),
                CommonServices = CreateConnections("Own common service")
            };
            List<ModellingConnection> globalCommonServices = CreateConnections("Global common service");

            IRenderedComponent<ConnectionsReport> component = context.Render<ConnectionsReport>(parameters => parameters
                .Add(p => p.OwnerData, new List<OwnerConnectionReport> { ownerReport })
                .Add(p => p.AllCommonServices, globalCommonServices));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("Application One"));
                Assert.That(component.Markup, Does.Contain("Connections"));
                Assert.That(component.Markup, Does.Contain("Interfaces"));
                Assert.That(component.Markup, Does.Contain("Own common services"));
                Assert.That(component.Markup, Does.Contain("Global common services"));
                Assert.That(component.Markup, Does.Contain("Regular connection"));
                Assert.That(component.Markup, Does.Contain("Global common service"));
            });
        }

        [Test]
        public void ConnectionsReport_SkipsEmptySections()
        {
            using BunitContext context = CreateContext();
            OwnerConnectionReport ownerReport = new()
            {
                Owner = new FwoOwner { Name = "Application Two" }
            };

            IRenderedComponent<ConnectionsReport> component = context.Render<ConnectionsReport>(parameters => parameters
                .Add(p => p.OwnerData, new List<OwnerConnectionReport> { ownerReport }));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("Application Two"));
                Assert.That(component.Markup, Does.Not.Contain("Connections"));
                Assert.That(component.Markup, Does.Not.Contain("Interfaces"));
                Assert.That(component.Markup, Does.Not.Contain("Own common services"));
                Assert.That(component.Markup, Does.Not.Contain("Global common services"));
            });
        }

        [Test]
        public void RecertEventReport_RendersConnectionsAndRulesBranch()
        {
            using BunitContext context = CreateContext(includeRuleTreeBuilder: true);
            OwnerConnectionReport ownerReport = new()
            {
                Owner = new FwoOwner
                {
                    Name = "Recert Owner",
                    LastRecertified = new DateTime(2026, 1, 2, 0, 0, 0)
                },
                RegularConnections = CreateConnections("Recert connection")
            };

            IRenderedComponent<RecertEventReport> component = context.Render<RecertEventReport>(parameters => parameters
                .Add(p => p.OwnerData, new List<OwnerConnectionReport> { ownerReport })
                .Add(p => p.Managements, new List<ManagementReport> { new ManagementReport { Name = "Mgmt One" } })
                .Add(p => p.RulesPerPage, 25));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("Recertification"));
                Assert.That(component.Markup, Does.Contain("Recert Owner"));
                Assert.That(component.Markup, Does.Contain("Connections"));
                Assert.That(component.Markup, Does.Contain("Recertified rules"));
                Assert.That(component.Markup, Does.Contain("Recert connection"));
            });
        }

        [Test]
        public void VariancesReport_RendersSummaryAndDifferenceSections()
        {
            using BunitContext context = CreateContext();
            OwnerConnectionReport ownerReport = new()
            {
                Owner = new FwoOwner { Name = "Variance Owner" },
                ImplementationState = "Implemented in UI",
                ModelledConnectionsCount = 2,
                Connections = CreateConnections("Unimplemented connection"),
                RegularConnections = CreateConnections("Regular variance connection"),
                CommonServices = CreateConnections("Common variance connection"),
                AppRoleStats = new AppRoleStats
                {
                    ModelledAppRolesCount = 2,
                    AppRolesOk = 1,
                    AppRolesMissingCount = 1,
                    AppRolesDifferenceCount = 1
                },
                MissingAppRoles = CreateMissingAppRoles(),
                DifferingAppRoles = CreateDifferingAppRoles()
            };

            IRenderedComponent<VariancesReport> component = context.Render<VariancesReport>(parameters => parameters
                .Add(p => p.OwnerData, new List<OwnerConnectionReport> { ownerReport }));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("Variance Owner"));
                Assert.That(component.Markup, Does.Contain("Implemented in UI"));
                Assert.That(component.Markup, Does.Contain("App roles not implemented"));
                Assert.That(component.Markup, Does.Contain("App roles with diffs"));
                Assert.That(component.Markup, Does.Contain("Connections not implemented"));
                Assert.That(component.Markup, Does.Contain("App roles"));
                Assert.That(component.Markup, Does.Contain("Fully modelled"));
                Assert.That(component.Markup, Does.Contain("Implemented"));
                Assert.That(component.Markup, Does.Contain("Not implemented"));
                Assert.That(component.Markup, Does.Contain("With diffs"));
                Assert.That(component.Markup, Does.Contain("Missing app servers"));
                Assert.That(component.Markup, Does.Contain("Surplus app servers"));
                Assert.That(component.Markup, Does.Contain("Regular variance connection"));
                Assert.That(component.Markup, Does.Contain("Common variance connection"));
            });
        }

        private static BunitContext CreateContext(bool includeRuleTreeBuilder = false)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());

            if (includeRuleTreeBuilder)
            {
                context.Services.AddSingleton<IRuleTreeBuilder, RuleTreeBuilder>();
            }

            return context;
        }

        private static List<ModellingConnection> CreateConnections(string name)
        {
            return new List<ModellingConnection>
            {
                new ModellingConnection
                {
                    Id = 1,
                    Name = name
                }
            };
        }

        private static Dictionary<int, List<ModellingAppRole>> CreateMissingAppRoles()
        {
            return new Dictionary<int, List<ModellingAppRole>>
            {
                {
                    10,
                    new List<ModellingAppRole>
                    {
                        new ModellingAppRole
                        {
                            Id = 10,
                            Name = "Missing role",
                            ManagementName = "Missing management"
                        }
                    }
                }
            };
        }

        private static Dictionary<int, List<ModellingAppRole>> CreateDifferingAppRoles()
        {
            return new Dictionary<int, List<ModellingAppRole>>
            {
                {
                    20,
                    new List<ModellingAppRole>
                    {
                        new ModellingAppRole
                        {
                            Id = 20,
                            Name = "Differing role",
                            ManagementName = "Differing management"
                        }
                    }
                }
            };
        }
    }
}
