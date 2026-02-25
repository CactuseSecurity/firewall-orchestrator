using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Bunit;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    public class ModellingAppHandlerTest
    {
        private static ModellingAppHandler CreateHandler(List<ModellingConnection> connections, UserConfig? userConfig = null)
        {
            UserConfig config = userConfig ?? new SimulatedUserConfig();
            ModellingAppHandler handler = new(
                new SimulatedApiConnection(),
                config,
                new FwoOwner { Id = 1 },
                DefaultInit.DoNothing,
                isOwner: true);
            handler.Connections = connections;
            SetPrivateField(handler, "dummyAppRoleId", 0L);
            return handler;
        }

        private static void SetPrivateField<TValue>(ModellingAppHandler handler, string fieldName, TValue value)
        {
            FieldInfo? field = typeof(ModellingAppHandler).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field!.SetValue(handler, value);
        }

        private static void SetComponentParameter<TValue>(object component, string parameterName, TValue value)
        {
            PropertyInfo? parameter = component.GetType().GetProperty(parameterName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(parameter, Is.Not.Null);
            parameter!.SetValue(component, value);
        }

        private static MethodInfo GetPrivateMethod(string name, params Type[] parameterTypes)
        {
            MethodInfo? method = typeof(ModellingAppHandler).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null);
            return method!;
        }

        [Test]
        public void GetInterfaces_ExcludesRejectedAndDecommissioned_WhenNotRequested()
        {
            ModellingConnection visible = new()
            {
                Id = 1,
                IsInterface = true,
                Props = new Dictionary<string, string>()
            };
            ModellingConnection rejected = new()
            {
                Id = 2,
                IsInterface = true,
                Props = new Dictionary<string, string>
                {
                    { ConState.Rejected.ToString(), "true" }
                }
            };
            ModellingConnection decommissioned = new()
            {
                Id = 3,
                IsInterface = true,
                Props = new Dictionary<string, string>
                {
                    { ConState.Decommissioned.ToString(), "true" }
                }
            };

            ModellingAppHandler handler = CreateHandler([visible, rejected, decommissioned]);

            List<ModellingConnection> interfaces = handler.GetInterfaces();

            Assert.That(interfaces, Is.EqualTo([visible]));
        }

        [Test]
        public void GetCommonServices_ReturnsOnlyCommonServices()
        {
            ModellingConnection common = new() { Id = 1, IsCommonService = true };
            ModellingConnection regular = new() { Id = 2 };

            ModellingAppHandler handler = CreateHandler([common, regular]);

            List<ModellingConnection> result = handler.GetCommonServices();

            Assert.That(result, Is.EqualTo([common]));
        }

        [Test]
        public void GetRegularConnections_ExcludesInterfacesAndCommonServices()
        {
            ModellingConnection regular = new() { Id = 1 };
            ModellingConnection common = new() { Id = 2, IsCommonService = true };
            ModellingConnection iface = new() { Id = 3, IsInterface = true };

            ModellingAppHandler handler = CreateHandler([regular, common, iface]);

            List<ModellingConnection> result = handler.GetRegularConnections();

            Assert.That(result, Is.EqualTo([regular]));
        }

        [Test]
        public void GetConnectionsToRequest_OrdersCommonServicesFirst()
        {
            ModellingConnection regular = new() { Id = 1 };
            ModellingConnection common = new() { Id = 2, IsCommonService = true };
            ModellingConnection iface = new() { Id = 3, IsInterface = true };

            ModellingAppHandler handler = CreateHandler([regular, common, iface]);

            List<ModellingConnection> result = handler.GetConnectionsToRequest();

            Assert.That(result, Is.EqualTo([common, regular]));
        }

        [Test]
        public void HasModellingIssues_ReturnsTrue_ForInterface()
        {
            ModellingConnection iface = new() { Id = 1, IsInterface = true };

            ModellingAppHandler handler = CreateHandler([iface]);

            Assert.That(handler.HasModellingIssues(iface), Is.True);
        }

        [Test]
        public async Task PrepareConnections_SyncsInterfaceState()
        {
            SimulatedUserConfig userConfig = new()
            {
                VarianceAnalysisSync = false,
                ModRolloutRemovedAppServers = false
            };
            ModellingConnection conn = new()
            {
                Id = 1,
                IsInterface = true,
                IsRequested = true
            };
            ModellingAppHandler handler = CreateHandler([conn], userConfig);

            MethodInfo prepareConnections = GetPrivateMethod("PrepareConnections", typeof(List<ModellingConnection>));
            Task prepareTask = (Task)prepareConnections.Invoke(handler, new object[] { handler.Connections })!;
            await prepareTask;

            Assert.That(conn.GetBoolProperty(ConState.Requested.ToString()), Is.True);
        }

        [Test]
        public async Task InitActiveTab_SetsInterfaceTab()
        {
            ModellingConnection iface = new() { Id = 1, IsInterface = true };
            ModellingAppHandler handler = CreateHandler([iface]);

            using Bunit.TestContext context = new();
            IRenderedComponent<TabSet> renderedTabSet = context.Render<TabSet>();
            TabSet tabSet = renderedTabSet.Instance;
            Tab tab0 = new();
            Tab tab1 = new();
            Tab tab2 = new();
            SetComponentParameter(tab0, nameof(Tab.Position), 0);
            SetComponentParameter(tab1, nameof(Tab.Position), 1);
            SetComponentParameter(tab2, nameof(Tab.Position), 2);
            tabSet.Tabs.AddRange([tab0, tab1, tab2]);
            handler.Tabset = tabSet;

            await renderedTabSet.InvokeAsync(() => handler.InitActiveTab(iface));

            Assert.That(handler.Tabset.ActiveTab, Is.EqualTo(tab1));
        }

        [Test]
        public async Task InitActiveTab_SetsCommonServiceTab_WhenNoRegularConnections()
        {
            ModellingConnection common = new() { Id = 2, IsCommonService = true };
            ModellingAppHandler handler = CreateHandler([common]);
            handler.Application.CommSvcPossible = true;

            using Bunit.TestContext context = new();
            IRenderedComponent<TabSet> renderedTabSet = context.Render<TabSet>();
            TabSet tabSet = renderedTabSet.Instance;
            Tab tab0 = new();
            Tab tab1 = new();
            Tab tab2 = new();
            SetComponentParameter(tab0, nameof(Tab.Position), 0);
            SetComponentParameter(tab1, nameof(Tab.Position), 1);
            SetComponentParameter(tab2, nameof(Tab.Position), 2);
            tabSet.Tabs.AddRange([tab0, tab1, tab2]);
            handler.Tabset = tabSet;

            await renderedTabSet.InvokeAsync(() => handler.InitActiveTab());

            Assert.That(handler.Tabset.ActiveTab, Is.EqualTo(tab2));
        }

        [Test]
        public async Task RestoreTab_UsesStoredTabPosition()
        {
            ModellingAppHandler handler = CreateHandler([]);

            using Bunit.TestContext context = new();
            IRenderedComponent<TabSet> renderedTabSet = context.Render<TabSet>();
            TabSet tabSet = renderedTabSet.Instance;
            Tab tab0 = new();
            Tab tab2 = new();
            SetComponentParameter(tab0, nameof(Tab.Position), 0);
            SetComponentParameter(tab2, nameof(Tab.Position), 2);
            tabSet.Tabs.AddRange([tab0, tab2]);
            await renderedTabSet.InvokeAsync(() => tabSet.SetActiveTab(tab0));
            handler.Tabset = tabSet;

            Tab actTab = new();
            SetComponentParameter(actTab, nameof(Tab.Position), 2);
            SetPrivateField(handler, "ActTab", actTab);

            await renderedTabSet.InvokeAsync(() => handler.RestoreTab());

            Assert.That(handler.Tabset.ActiveTab, Is.EqualTo(tab2));
        }
    }
}
