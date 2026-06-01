using FWO.Data.Workflow;
using FWO.Ui.Pages.Monitoring;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Pages.Request;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    internal class UiModellingDoubleClickGuardTest
    {
        [TestCase(typeof(RequestInterfacePopup), "SendRequest")]
        [TestCase(typeof(RequestFwChangePopup), "StartRequests")]
        [TestCase(typeof(RequestRecertPopup), "StartRecert")]
        [TestCase(typeof(MonitorRequestedInterfaces), "RejectRemovedInterfaceTickets")]
        [TestCase(typeof(DisplayRequestTask), "SaveReqTask")]
        [TestCase(typeof(DisplayImplementationTask), "AssignOwner")]
        [TestCase(typeof(DisplayImplementationTask), "SaveImplTask")]
        public async Task NoArgumentHandlers_ReturnImmediately_WhenWorkIsAlreadyInProgress(Type componentType, string methodName)
        {
            object component = CreateComponentWithRunningGuard(componentType);

            await InvokePrivateTask(component, methodName);

            Assert.That(GetPrivateField<bool>(component, "WorkInProgress"), Is.True);
        }

        [TestCaseSource(nameof(ArgumentHandlerCases))]
        public async Task ArgumentHandlers_ReturnImmediately_WhenWorkIsAlreadyInProgress(Type componentType, string methodName, object[] args)
        {
            object component = CreateComponentWithRunningGuard(componentType);

            await InvokePrivateTask(component, methodName, args);

            Assert.That(GetPrivateField<bool>(component, "WorkInProgress"), Is.True);
        }

        private static IEnumerable ArgumentHandlerCases()
        {
            yield return new TestCaseData(typeof(OrphanedRequestedInterfaceTicketsPopup), "RejectTickets", new object[] { new List<long> { 1 } });
            yield return new TestCaseData(typeof(DisplayTicket), "PerformAction", new object[] { new WfStateAction() });
            yield return new TestCaseData(typeof(DisplayRequestTask), "PerformAction", new object[] { new WfStateAction() });
            yield return new TestCaseData(typeof(DisplayRequestTask), "ConfApproveTask", new object[] { new WfTicket() });
            yield return new TestCaseData(typeof(DisplayImplementationTask), "PerformAction", new object[] { new WfStateAction() });
        }

        private static object CreateComponentWithRunningGuard(Type componentType)
        {
            object component = Activator.CreateInstance(componentType) ?? throw new AssertionException($"Could not create {componentType.Name}.");
            SetPrivateField(component, "WorkInProgress", true);
            return component;
        }

        private static async Task InvokePrivateTask(object component, string methodName, object[]? args = null)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(component.GetType().FullName, methodName);
            Task task = (Task)(method.Invoke(component, args) ?? throw new AssertionException($"{methodName} did not return a Task."));
            await task;
        }

        private static void SetPrivateField<TValue>(object component, string fieldName, TValue value)
        {
            FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            PropertyInfo property = component.GetType().GetProperty(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(component.GetType().FullName, fieldName);
            property.SetValue(component, value);
        }

        private static TValue GetPrivateField<TValue>(object component, string fieldName)
        {
            FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (TValue)field.GetValue(component)!;
            }

            PropertyInfo property = component.GetType().GetProperty(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(component.GetType().FullName, fieldName);
            return (TValue)property.GetValue(component)!;
        }
    }
}
