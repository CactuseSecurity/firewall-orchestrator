using System.Reflection;
using FWO.Api.Client;
using FWO.Data;
using FWO.Ui.Shared;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiLogDataTableTest
    {
        [Test]
        public async Task OnParametersSet_LoadsLogEntriesOfOwner()
        {
            LogDataTableTestApiConn apiConnection = new();
            LogDataTable component = CreateComponent(apiConnection, ownerId: 7);

            await InvokeOnParametersSetAsync(component);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<OwnerFirewallLogEntry>>(component, "logEntries"), Has.Count.EqualTo(1));
                Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastOwnerId, Is.EqualTo(7));
                Assert.That(GetPrivateField<bool>(component, "isLoading"), Is.False);
            });
        }

        [Test]
        public async Task OnParametersSet_DoesNotReloadForTheSameOwner()
        {
            LogDataTableTestApiConn apiConnection = new();
            LogDataTable component = CreateComponent(apiConnection, ownerId: 7);

            await InvokeOnParametersSetAsync(component);
            await InvokeOnParametersSetAsync(component);

            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task OnParametersSet_ReloadsAfterOwnerChange()
        {
            LogDataTableTestApiConn apiConnection = new();
            LogDataTable component = CreateComponent(apiConnection, ownerId: 7);

            await InvokeOnParametersSetAsync(component);
            SetPrivateProperty(component, nameof(LogDataTable.OwnerId), 8);
            await InvokeOnParametersSetAsync(component);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.QueryCount, Is.EqualTo(2));
                Assert.That(apiConnection.LastOwnerId, Is.EqualTo(8));
            });
        }

        [Test]
        public async Task OnParametersSet_KeepsEmptyListWhenTheQueryFails()
        {
            LogDataTableTestApiConn apiConnection = new() { FailQuery = true };
            LogDataTable component = CreateComponent(apiConnection, ownerId: 7);

            await InvokeOnParametersSetAsync(component);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<OwnerFirewallLogEntry>>(component, "logEntries"), Is.Empty);
                Assert.That(GetPrivateField<bool>(component, "isLoading"), Is.False);
            });
        }

        private static LogDataTable CreateComponent(ApiConnection apiConnection, int ownerId)
        {
            LogDataTable component = new();
            SetPrivateProperty(component, "apiConnection", apiConnection);
            SetPrivateProperty(component, "userConfig", new SimulatedUserConfig());
            SetPrivateProperty(component, nameof(LogDataTable.OwnerId), ownerId);
            return component;
        }

        private static async Task InvokeOnParametersSetAsync(LogDataTable component)
        {
            MethodInfo method = typeof(LogDataTable).GetMethod("OnParametersSetAsync", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(LogDataTable).FullName, "OnParametersSetAsync");
            await (Task)method.Invoke(component, null)!;
        }

        private static void SetPrivateProperty<T>(LogDataTable component, string propertyName, T value)
        {
            PropertyInfo property = typeof(LogDataTable).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMemberException(typeof(LogDataTable).FullName, propertyName);
            property.SetValue(component, value);
        }

        private static T GetPrivateField<T>(LogDataTable component, string fieldName)
        {
            FieldInfo field = typeof(LogDataTable).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(LogDataTable).FullName, fieldName);
            return (T)field.GetValue(component)!;
        }

        private sealed class LogDataTableTestApiConn : SimulatedApiConnection
        {
            public int QueryCount { get; private set; }
            public int? LastOwnerId { get; private set; }
            public bool FailQuery { get; init; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;
                LastOwnerId = (int?)variables?.GetType().GetProperty("ownerId")?.GetValue(variables);
                if (FailQuery)
                {
                    throw new InvalidOperationException("query failed");
                }

                List<OwnerFirewallLogEntry> entries = [new() { LogCount = 42, Source = "192.0.2.1/32", Destination = "198.51.100.1/32" }];
                return Task.FromResult((QueryResponseType)(object)entries);
            }
        }
    }
}
