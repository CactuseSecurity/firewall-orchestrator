using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Linq;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsResponsiblesTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(SettingsResponsibles).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(SettingsResponsibles).FullName, name);
        }

        private static void SetPrivateField<T>(SettingsResponsibles component, string fieldName, T value)
        {
            FieldInfo? field = typeof(SettingsResponsibles).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(SettingsResponsibles).FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static T GetPrivateField<T>(SettingsResponsibles component, string fieldName)
        {
            FieldInfo? field = typeof(SettingsResponsibles).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(SettingsResponsibles).FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static void SetInjectedUserConfig(SettingsResponsibles component, UserConfig userConfig)
        {
            PropertyInfo? prop = typeof(SettingsResponsibles).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType == typeof(UserConfig));
            if (prop == null)
            {
                throw new MissingMemberException(typeof(SettingsResponsibles).FullName, "userConfig");
            }
            prop.SetValue(component, userConfig);
        }

        private static void SetInjectedApiConnection(SettingsResponsibles component, ApiConnection apiConnection)
        {
            PropertyInfo? prop = typeof(SettingsResponsibles).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType == typeof(ApiConnection));
            if (prop == null)
            {
                throw new MissingMemberException(typeof(SettingsResponsibles).FullName, "apiConnection");
            }
            prop.SetValue(component, apiConnection);
        }

        [Test]
        public async Task RequestDeleteResponsibleType_UsesDeactivateMessage_WhenTypeIsInUseAndActive()
        {
            SettingsResponsibles component = new();
            SettingsResponsiblesTestApiConn apiConn = new();
            apiConn.ResponsibleTypes.Add(new OwnerResponsibleType { Id = 10, Name = "Main", Active = true, SortOrder = 10 });
            apiConn.InUseTypeIds.Add(10);
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new EditOwnerTestUserConfig());

            Task task = (Task)GetPrivateMethod("RequestDeleteResponsibleType").Invoke(component, [apiConn.ResponsibleTypes[0]])!;
            await task;

            Assert.That(GetPrivateField<bool>(component, "DeleteTypeMode"), Is.True);
            string message = GetPrivateField<string>(component, "deleteTypeMessage");
            Assert.That(message, Does.Contain("Main"));
            Assert.That(message.ToLowerInvariant(), Does.StartWith("deactivate"));
        }

        [Test]
        public async Task DeleteResponsibleType_Deactivates_WhenTypeIsInUse()
        {
            SettingsResponsibles component = new();
            SettingsResponsiblesTestApiConn apiConn = new();
            OwnerResponsibleType type = new() { Id = 11, Name = "Supporting", Active = true, SortOrder = 20 };
            apiConn.ResponsibleTypes.Add(type);
            apiConn.InUseTypeIds.Add(11);
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new EditOwnerTestUserConfig());
            SetPrivateField(component, "actType", new OwnerResponsibleType
            {
                Id = type.Id,
                Name = type.Name,
                Active = type.Active,
                SortOrder = type.SortOrder,
                AllowModelling = type.AllowModelling,
                AllowRecertification = type.AllowRecertification
            });
            SetPrivateField(component, "DeleteTypeMode", true);

            Task task = (Task)GetPrivateMethod("DeleteResponsibleType").Invoke(component, null)!;
            await task;

            Assert.That(apiConn.UpdateCalls, Is.EqualTo(1));
            Assert.That(apiConn.DeleteCalls, Is.EqualTo(0));
            Assert.That(apiConn.ResponsibleTypes.First(t => t.Id == 11).Active, Is.False);
            Assert.That(GetPrivateField<bool>(component, "DeleteTypeMode"), Is.False);
        }

        [Test]
        public async Task DeleteResponsibleType_Deletes_WhenTypeIsNotInUse()
        {
            SettingsResponsibles component = new();
            SettingsResponsiblesTestApiConn apiConn = new();
            OwnerResponsibleType type = new() { Id = 12, Name = "Escalation", Active = true, SortOrder = 30 };
            apiConn.ResponsibleTypes.Add(type);
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new EditOwnerTestUserConfig());
            SetPrivateField(component, "actType", new OwnerResponsibleType
            {
                Id = type.Id,
                Name = type.Name,
                Active = type.Active,
                SortOrder = type.SortOrder,
                AllowModelling = type.AllowModelling,
                AllowRecertification = type.AllowRecertification
            });
            SetPrivateField(component, "DeleteTypeMode", true);

            Task task = (Task)GetPrivateMethod("DeleteResponsibleType").Invoke(component, null)!;
            await task;

            Assert.That(apiConn.DeleteCalls, Is.EqualTo(1));
            Assert.That(apiConn.UpdateCalls, Is.EqualTo(0));
            Assert.That(apiConn.ResponsibleTypes.Any(t => t.Id == 12), Is.False);
            Assert.That(GetPrivateField<bool>(component, "DeleteTypeMode"), Is.False);
        }

        [Test]
        public async Task ReactivateResponsibleType_UpdatesActiveFlag()
        {
            SettingsResponsibles component = new();
            SettingsResponsiblesTestApiConn apiConn = new();
            OwnerResponsibleType type = new() { Id = 13, Name = "Optional", Active = false, SortOrder = 40 };
            apiConn.ResponsibleTypes.Add(type);
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new EditOwnerTestUserConfig());

            Task task = (Task)GetPrivateMethod("ReactivateResponsibleType").Invoke(component, [type])!;
            await task;

            Assert.That(apiConn.UpdateCalls, Is.EqualTo(1));
            Assert.That(apiConn.ResponsibleTypes.First(t => t.Id == 13).Active, Is.True);
        }
    }

    internal sealed class SettingsResponsiblesTestApiConn : SimulatedApiConnection
    {
        public List<OwnerResponsibleType> ResponsibleTypes { get; } = [];
        public HashSet<int> InUseTypeIds { get; } = [];
        public int UpdateCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            if (query == OwnerQueries.getOwnersForResponsibleType)
            {
                int typeId = GetAnonymousInt(variables, "responsibleTypeId");
                List<FwoOwner> owners = InUseTypeIds.Contains(typeId) ? [new FwoOwner { Id = 1 }] : [];
                return Task.FromResult((QueryResponseType)(object)owners);
            }

            if (query == OwnerQueries.updateOwnerResponsibleType)
            {
                ++UpdateCalls;
                int typeId = GetAnonymousInt(variables, "id");
                bool active = GetAnonymousBool(variables, "active");
                OwnerResponsibleType? type = ResponsibleTypes.FirstOrDefault(t => t.Id == typeId);
                if (type != null)
                {
                    type.Active = active;
                    type.Name = GetAnonymousString(variables, "name");
                    type.SortOrder = GetAnonymousInt(variables, "sort_order");
                    type.AllowModelling = GetAnonymousBool(variables, "allow_modelling");
                    type.AllowRecertification = GetAnonymousBool(variables, "allow_recertification");
                }
                return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedId = typeId });
            }

            if (query == OwnerQueries.deleteOwnerResponsibleType)
            {
                ++DeleteCalls;
                int typeId = GetAnonymousInt(variables, "id");
                ResponsibleTypes.RemoveAll(type => type.Id == typeId);
                ReturnIdWrapper wrapper = new()
                {
                    ReturnIds = [new ReturnId { DeletedId = typeId }]
                };
                return Task.FromResult((QueryResponseType)(object)wrapper);
            }

            if (query == OwnerQueries.getOwnerResponsibleTypes)
            {
                List<OwnerResponsibleType> copy = [.. ResponsibleTypes
                    .Select(type => new OwnerResponsibleType
                    {
                        Id = type.Id,
                        Name = type.Name,
                        Active = type.Active,
                        SortOrder = type.SortOrder,
                        AllowModelling = type.AllowModelling,
                        AllowRecertification = type.AllowRecertification
                    })];
                return Task.FromResult((QueryResponseType)(object)copy);
            }

            throw new NotImplementedException($"Query not implemented in SettingsResponsibles test api: {query}");
        }

        private static int GetAnonymousInt(object? variables, string propertyName)
        {
            object? value = GetAnonymousValue(variables, propertyName);
            return value is int intValue ? intValue : 0;
        }

        private static bool GetAnonymousBool(object? variables, string propertyName)
        {
            object? value = GetAnonymousValue(variables, propertyName);
            return value is bool boolValue && boolValue;
        }

        private static string GetAnonymousString(object? variables, string propertyName)
        {
            object? value = GetAnonymousValue(variables, propertyName);
            return value as string ?? "";
        }

        private static object? GetAnonymousValue(object? variables, string propertyName)
        {
            if (variables == null)
            {
                return null;
            }
            PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property?.GetValue(variables);
        }
    }
}
