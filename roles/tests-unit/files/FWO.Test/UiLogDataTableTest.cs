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
        public async Task OnParametersSet_LimitsTheNumberOfLoadedEntries()
        {
            LogDataTableTestApiConn apiConnection = new();
            LogDataTable component = CreateComponent(apiConnection, ownerId: 7);

            await InvokeOnParametersSetAsync(component);

            Assert.That(apiConnection.LastLimit, Is.GreaterThan(0), "the query is bounded");
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

        [Test]
        public async Task OnParametersSet_DoesNotRepeatAFailedLoad()
        {
            LogDataTableTestApiConn apiConnection = new() { FailQuery = true };
            LogDataTable component = CreateComponent(apiConnection, ownerId: 7);

            await InvokeOnParametersSetAsync(component);
            await InvokeOnParametersSetAsync(component);

            Assert.That(apiConnection.QueryCount, Is.EqualTo(1),
                "a re-render of the surrounding page must not query and report the error again");
        }

        [Test]
        public async Task OnParametersSet_KeepsTheRowsOfTheOwnerSelectedLast()
        {
            GatedLogDataTableTestApiConn apiConnection = new();
            LogDataTable component = CreateComponent(apiConnection, ownerId: 7);
            Task firstLoad = StartOnParametersSetAsync(component);
            SetPrivateProperty(component, nameof(LogDataTable.OwnerId), 8);
            Task secondLoad = StartOnParametersSetAsync(component);

            // the owner selected last answers first, the load started before it answers late
            apiConnection.Answer(8);
            await secondLoad;
            apiConnection.Answer(7);
            await firstLoad;

            List<OwnerFirewallLogEntry> displayedEntries = GetPrivateField<List<OwnerFirewallLogEntry>>(component, "logEntries");
            Assert.That(displayedEntries.Single().LogCount, Is.EqualTo(8),
                "the late answer of the previous owner must not replace the rows on screen");
        }

        [Test]
        public void WrapperCssClass_LeavesAShortTableUnconstrained()
        {
            LogDataTable component = new();
            SetPrivateField(component, "logEntries", BuildEntries(3));

            Assert.That(GetPrivateProperty<string>(component, "WrapperCssClass"), Is.Empty,
                "a scrolling box would cut off the column filter of a table with few rows");
        }

        [Test]
        public void WrapperCssClass_LimitsTheHeightOfALongTable()
        {
            LogDataTable component = new();
            SetPrivateField(component, "logEntries", BuildEntries(25));

            Assert.That(GetPrivateProperty<string>(component, "WrapperCssClass"), Is.EqualTo("logdatatable-responsive"));
        }

        private static List<OwnerFirewallLogEntry> BuildEntries(int count)
        {
            return Enumerable.Range(0, count).Select(number => new OwnerFirewallLogEntry { LogCount = number }).ToList();
        }

        private static void SetPrivateField<T>(LogDataTable component, string fieldName, T value)
        {
            FieldInfo field = typeof(LogDataTable).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(LogDataTable).FullName, fieldName);
            field.SetValue(component, value);
        }

        private static T GetPrivateProperty<T>(LogDataTable component, string propertyName)
        {
            PropertyInfo property = typeof(LogDataTable).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(typeof(LogDataTable).FullName, propertyName);
            return (T)property.GetValue(component)!;
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
            await StartOnParametersSetAsync(component);
        }

        private static Task StartOnParametersSetAsync(LogDataTable component)
        {
            MethodInfo method = typeof(LogDataTable).GetMethod("OnParametersSetAsync", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(LogDataTable).FullName, "OnParametersSetAsync");
            return (Task)method.Invoke(component, null)!;
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

        /// <summary>
        /// Answers every owner only when the test lets it, so two loads can overlap.
        /// </summary>
        private sealed class GatedLogDataTableTestApiConn : SimulatedApiConnection
        {
            private readonly Dictionary<int, TaskCompletionSource> answers = [];

            public void Answer(int ownerId)
            {
                GetAnswer(ownerId).TrySetResult();
            }

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                int ownerId = (int?)variables?.GetType().GetProperty("ownerId")?.GetValue(variables) ?? 0;
                await GetAnswer(ownerId).Task;
                // the log count identifies the owner the entries were loaded for
                List<OwnerFirewallLogEntry> entries = [new() { LogCount = ownerId, Source = "192.0.2.1/32", Destination = "198.51.100.1/32" }];
                return (QueryResponseType)(object)entries;
            }

            private TaskCompletionSource GetAnswer(int ownerId)
            {
                lock (answers)
                {
                    if (!answers.TryGetValue(ownerId, out TaskCompletionSource? answer))
                    {
                        answer = new TaskCompletionSource();
                        answers.Add(ownerId, answer);
                    }
                    return answer;
                }
            }
        }

        private sealed class LogDataTableTestApiConn : SimulatedApiConnection
        {
            public int QueryCount { get; private set; }
            public int? LastOwnerId { get; private set; }
            public int? LastLimit { get; private set; }
            public bool FailQuery { get; init; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;
                LastOwnerId = (int?)variables?.GetType().GetProperty("ownerId")?.GetValue(variables);
                LastLimit = (int?)variables?.GetType().GetProperty("limit")?.GetValue(variables);
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
