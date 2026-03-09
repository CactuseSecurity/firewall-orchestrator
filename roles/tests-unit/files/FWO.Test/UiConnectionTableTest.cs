using FWO.Data;
using FWO.Data.Modelling;
using FWO.Ui.Shared;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    public class UiConnectionTableTest
    {
        private static void SetComponentParameter<TValue>(ConnectionTable table, string parameterName, TValue value)
        {
            PropertyInfo? parameter = typeof(ConnectionTable).GetProperty(parameterName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(parameter, Is.Not.Null);
            parameter!.SetValue(table, value);
        }

        private static void SetPrivateMember<TValue>(ConnectionTable table, string memberName, TValue value)
        {
            FieldInfo? field = typeof(ConnectionTable).GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(table, value);
                return;
            }

            PropertyInfo? property = typeof(ConnectionTable).GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null);
            property!.SetValue(table, value);
        }

        private static MethodInfo GetInstanceMethod(string methodName, params Type[] parameterTypes)
        {
            MethodInfo? method = typeof(ConnectionTable).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                parameterTypes,
                null);

            Assert.That(method, Is.Not.Null);
            return method!;
        }

        [Test]
        public void ToggleSelection_SingleSelection_ReplacesSelection()
        {
            ConnectionTable table = new();
            SetComponentParameter(table, nameof(ConnectionTable.SelectInterfaceView), true);
            PropertyInfo selectionTypeProperty = typeof(ConnectionTable).GetProperty(nameof(ConnectionTable.SelectionType))!;
            object selectionTypeSingle = Enum.Parse(selectionTypeProperty.PropertyType, "Single");
            SetComponentParameter(table, nameof(ConnectionTable.SelectionType), selectionTypeSingle);
            SetComponentParameter(table, nameof(ConnectionTable.SelectedConns), new List<ModellingConnection>());

            ModellingConnection conn1 = new() { Id = 1 };
            ModellingConnection conn2 = new() { Id = 2 };
            table.SelectedConns.Add(conn1);

            MethodInfo toggleSelection = GetInstanceMethod("ToggleSelection", typeof(ModellingConnection));
            toggleSelection.Invoke(table, [conn2]);

            Assert.That(table.SelectedConns, Is.EqualTo([conn2]));
        }

        [Test]
        public void ToggleSelection_MultipleSelection_RemovesExisting()
        {
            ConnectionTable table = new();
            SetComponentParameter(table, nameof(ConnectionTable.SelectInterfaceView), true);
            PropertyInfo selectionTypeProperty = typeof(ConnectionTable).GetProperty(nameof(ConnectionTable.SelectionType))!;
            object selectionTypeMultiple = Enum.Parse(selectionTypeProperty.PropertyType, "Multiple");
            SetComponentParameter(table, nameof(ConnectionTable.SelectionType), selectionTypeMultiple);
            SetComponentParameter(table, nameof(ConnectionTable.SelectedConns), new List<ModellingConnection>());

            ModellingConnection conn1 = new() { Id = 1 };
            ModellingConnection conn2 = new() { Id = 2 };
            table.SelectedConns.Add(conn1);
            table.SelectedConns.Add(conn2);

            MethodInfo toggleSelection = GetInstanceMethod("ToggleSelection", typeof(ModellingConnection));
            toggleSelection.Invoke(table, [conn2]);

            Assert.That(table.SelectedConns, Is.EqualTo([conn1]));
        }

        [Test]
        public void GetTableRowClass_ReturnsGreyForRemoved()
        {
            ConnectionTable table = new();
            ModellingConnection conn = new() { Removed = true };

            MethodInfo getRowClass = GetInstanceMethod("getTableRowClass", typeof(ModellingConnection));
            string rowClass = (string)getRowClass.Invoke(table, [conn])!;

            Assert.That(rowClass, Is.EqualTo("td-bg-light-grey"));
        }

        [Test]
        public void DisplayConditional_WrapsRequestedContent()
        {
            ConnectionTable table = new();
            ModellingConnection conn = new() { IsRequested = true };

            MethodInfo displayConditional = GetInstanceMethod("DisplayConditional", typeof(ModellingConnection), typeof(string));
            string result = (string)displayConditional.Invoke(table, [conn, "content"])!;

            Assert.That(result, Is.EqualTo("<span class=\"text-secondary\">content</span>"));
        }

        [Test]
        public void CollectModellingProps_AddsInterfaceRequestedWarning()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                Props = new Dictionary<string, string>
                {
                    { nameof(ConState.InterfaceRequested), "true" }
                }
            };

            MethodInfo collectModellingProps = GetInstanceMethod("CollectModellingProps", typeof(ModellingConnection));
            List<string> props = (List<string>)collectModellingProps.Invoke(table, [conn])!;

            Assert.That(props, Has.Count.EqualTo(1));
            Assert.That(props[0], Does.Contain("Requested by other owner"));
            Assert.That(props[0], Does.Contain("text-warning"));
        }

        [Test]
        public void CollectModellingProps_AddsInterfaceRejectedWarning()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                Props = new Dictionary<string, string>
                {
                    { nameof(ConState.InterfaceRejected), "true" }
                }
            };

            MethodInfo collectModellingProps = GetInstanceMethod("CollectModellingProps", typeof(ModellingConnection));
            List<string> props = (List<string>)collectModellingProps.Invoke(table, [conn])!;

            Assert.That(props, Has.Count.EqualTo(1));
            Assert.That(props[0], Does.Contain("Rejected"));
            Assert.That(props[0], Does.Contain("color:red"));
        }

        [Test]
        public void CollectImplementationProps_AddsNotImplementedWarning()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                Props = new Dictionary<string, string>
                {
                    { nameof(ConState.NotImplemented), "true" }
                }
            };

            MethodInfo collectImplementationProps = GetInstanceMethod("CollectImplementationProps", typeof(ModellingConnection));
            List<string> props = (List<string>)collectImplementationProps.Invoke(table, [conn])!;

            Assert.That(props, Has.Count.EqualTo(1));
            Assert.That(props[0], Does.Contain("Not implemented"));
            Assert.That(props[0], Does.Contain("text-warning"));
        }

        [Test]
        public void DisplayModState_ReturnsOk_WhenNoProps()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                Props = new Dictionary<string, string>()
            };

            string result = table.DisplayModState(conn);

            Assert.That(result, Does.Contain("Modelling ok"));
        }

        [Test]
        public void DisplayModState_ReturnsWarning_WhenPropsPresent()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                Props = new Dictionary<string, string>
                {
                    { nameof(ConState.Requested), "true" }
                }
            };

            string result = table.DisplayModState(conn);

            Assert.That(result, Does.Contain("Requested"));
            Assert.That(result, Does.Contain("text-warning"));
        }

        [Test]
        public void DisplayImplState_ReturnsDash_WhenModellingPropsPresent()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                Props = new Dictionary<string, string>
                {
                    { nameof(ConState.Requested), "true" }
                }
            };

            string result = table.DisplayImplState(conn);

            Assert.That(result, Is.EqualTo("-"));
        }

        [Test]
        public void DisplayImplState_ReturnsOk_WhenVarianceCheckedAndNoIssues()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                Props = new Dictionary<string, string>
                {
                    { nameof(ConState.VarianceChecked), "true" }
                }
            };

            string result = table.DisplayImplState(conn);

            Assert.That(result, Does.Contain("Implementation ok"));
        }

        [Test]
        public void DisplayImplState_ReturnsQuestion_WhenVarianceNotChecked()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                Props = new Dictionary<string, string>()
            };

            string result = table.DisplayImplState(conn);

            Assert.That(result, Does.Contain("Variance not checked"));
        }

        [Test]
        public void CollectModellingProps_AddsInterfaceNoPermission_WhenAlone()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                Props = new Dictionary<string, string>
                {
                    { nameof(ConState.InterfaceNoPermission), "true" }
                }
            };

            MethodInfo collectModellingProps = GetInstanceMethod("CollectModellingProps", typeof(ModellingConnection));
            List<string> props = (List<string>)collectModellingProps.Invoke(table, [conn])!;

            Assert.That(props, Has.Count.EqualTo(1));
            Assert.That(props[0], Does.Contain("Interface no permission"));
            Assert.That(props[0], Does.Contain("color:red"));
        }

        [Test]
        public void CollectModellingProps_AddsEmptySvcGroups_InterfaceVariant()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                IsInterface = true,
                Props = new Dictionary<string, string>
                {
                    { nameof(ConState.EmptySvcGrps), "true" }
                }
            };

            MethodInfo collectModellingProps = GetInstanceMethod("CollectModellingProps", typeof(ModellingConnection));
            List<string> props = (List<string>)collectModellingProps.Invoke(table, [conn])!;

            Assert.That(props, Has.Count.EqualTo(1));
            Assert.That(props[0], Does.Contain("Empty service groups (interface)"));
            Assert.That(props[0], Does.Contain("text-warning"));
        }

        [Test]
        public void CollectModellingProps_AddsDocumentationOnly()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                Props = new Dictionary<string, string>
                {
                    { nameof(ConState.DocumentationOnly), "true" }
                }
            };

            MethodInfo collectModellingProps = GetInstanceMethod("CollectModellingProps", typeof(ModellingConnection));
            List<string> props = (List<string>)collectModellingProps.Invoke(table, [conn])!;

            Assert.That(props, Has.Count.EqualTo(1));
            Assert.That(props[0], Does.Contain("Documentation only"));
            Assert.That(props[0], Does.Contain("text-warning"));
        }

        [Test]
        public void CollectImplementationProps_AddsVarianceFoundWarning()
        {
            ConnectionTable table = new();
            SetPrivateMember(table, "userConfig", new SimulatedUserConfig());

            ModellingConnection conn = new()
            {
                Props = new Dictionary<string, string>
                {
                    { nameof(ConState.VarianceFound), "true" }
                }
            };

            MethodInfo collectImplementationProps = GetInstanceMethod("CollectImplementationProps", typeof(ModellingConnection));
            List<string> props = (List<string>)collectImplementationProps.Invoke(table, [conn])!;

            Assert.That(props, Has.Count.EqualTo(1));
            Assert.That(props[0], Does.Contain("Variance found"));
            Assert.That(props[0], Does.Contain("text-warning"));
        }

        [Test]
        public void IsVisibleToOwner_ReturnsTrue_ForPublicInterface()
        {
            ConnectionTable table = new();
            SetComponentParameter(table, nameof(ConnectionTable.Application), new FwoOwner { Id = 1 });

            ModellingConnection conn = new()
            {
                InterfacePermission = InterfacePermissions.Public.ToString(),
                App = new FwoOwner { Id = 2 }
            };

            MethodInfo isVisibleToOwner = GetInstanceMethod("IsVisibleToOwner", typeof(ModellingConnection));
            bool result = (bool)isVisibleToOwner.Invoke(table, [conn])!;

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsVisibleToOwner_ReturnsFalse_ForPrivateInterface()
        {
            ConnectionTable table = new();
            SetComponentParameter(table, nameof(ConnectionTable.Application), new FwoOwner { Id = 1 });

            ModellingConnection conn = new()
            {
                InterfacePermission = InterfacePermissions.Private.ToString(),
                App = new FwoOwner { Id = 2 }
            };

            MethodInfo isVisibleToOwner = GetInstanceMethod("IsVisibleToOwner", typeof(ModellingConnection));
            bool result = (bool)isVisibleToOwner.Invoke(table, [conn])!;

            Assert.That(result, Is.False);
        }

        [Test]
        public void IsVisibleToOwner_ReturnsTrue_ForPermittedOwner()
        {
            ConnectionTable table = new();
            SetComponentParameter(table, nameof(ConnectionTable.Application), new FwoOwner { Id = 3 });

            ModellingConnection conn = new()
            {
                InterfacePermission = InterfacePermissions.Restricted.ToString(),
                App = new FwoOwner { Id = 2 },
                PermittedOwners = [new FwoOwner { Id = 3 }]
            };

            MethodInfo isVisibleToOwner = GetInstanceMethod("IsVisibleToOwner", typeof(ModellingConnection));
            bool result = (bool)isVisibleToOwner.Invoke(table, [conn])!;

            Assert.That(result, Is.True);
        }
    }
}
