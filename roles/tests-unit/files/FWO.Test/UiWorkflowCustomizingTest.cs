using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiWorkflowCustomizingTest
    {
        private static MethodInfo GetPrivateMethod(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(type.FullName, name);
        }

        private static void SetMember(object instance, string memberName, object? value)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            throw new MissingFieldException(type.FullName, memberName);
        }

        [Test]
        public async Task HandleAllowedChangesByApproverChanged_PersistsConfigImmediately()
        {
            SettingsCustomizing component = new();
            WorkflowCustomizingApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]"
            };
            SimulatedUserConfig userConfig = new();
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            ApproverAllowedChangesConfig newConfig = new();
            newConfig.SetTicketField(WorkflowEditableFieldKeys.Reason, true);

            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "configData", editableConfig);

            Task handlerTask = (Task)GetPrivateMethod(typeof(SettingsCustomizing), "HandleAllowedChangesByApproverChanged")
                .Invoke(component, [newConfig.ToConfigValue()])!;
            await handlerTask;

            Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
            Assert.That(apiConnection.LastConfigItems, Has.Count.EqualTo(1));
            Assert.That(apiConnection.LastConfigItems[0].Key, Is.EqualTo("reqAllowedChangesByApprover"));
            Assert.That(apiConnection.LastConfigItems[0].Value, Is.EqualTo(newConfig.ToConfigValue()));
            Assert.That(editableConfig.ReqAllowedChangesByApprover, Is.EqualTo(newConfig.ToConfigValue()));
        }

        private sealed class WorkflowCustomizingApiConn : SimulatedApiConnection
        {
            public int UpsertConfigCallCount { get; private set; }
            public List<ConfigItem> LastConfigItems { get; private set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == ConfigQueries.upsertConfigItems)
                {
                    UpsertConfigCallCount++;
                    PropertyInfo configItemsProperty = variables?.GetType().GetProperty("config_items")
                        ?? throw new MissingFieldException("config_items");
                    LastConfigItems = ((IEnumerable<ConfigItem>)configItemsProperty.GetValue(variables)!).ToList();
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                throw new NotImplementedException();
            }
        }
    }
}
