using System.Reflection;
using FWO.Api.Client;
using FWO.Data;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.JSInterop;
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
            return InvokePrivateTask(component, "OnParametersSetAsync");
        }

        private static Task InvokePrivateTask(LogDataTable component, string methodName)
        {
            MethodInfo method = typeof(LogDataTable).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(LogDataTable).FullName, methodName);
            return (Task)method.Invoke(component, null)!;
        }

        [Test]
        public async Task AdjustPageSize_TakesTheRowCountMeasuredInTheBrowser()
        {
            LogDataTableTestJsRuntime jsRuntime = new() { MeasuredRows = 40 };
            LogDataTable component = await CreateLoadedComponent(jsRuntime);

            await InvokePrivateTask(component, "AdjustPageSize");

            Assert.That(GetPrivateField<int>(component, "pageSize"), Is.EqualTo(40));
        }

        [Test]
        public async Task AdjustPageSize_RaisesATinyWindowToTheMinimumPageSize()
        {
            LogDataTableTestJsRuntime jsRuntime = new() { MeasuredRows = 2 };
            LogDataTable component = await CreateLoadedComponent(jsRuntime);

            await InvokePrivateTask(component, "AdjustPageSize");

            Assert.That(GetPrivateField<int>(component, "pageSize"), Is.EqualTo(LogDataTableLayout.kMinPageSize),
                "a window too short for the minimum must still show enough rows to page through");
        }

        [Test]
        public async Task AdjustPageSize_KeepsTheDefaultWhenTheBrowserCannotBeMeasured()
        {
            LogDataTableTestJsRuntime jsRuntime = new() { Fail = true };
            LogDataTable component = await CreateLoadedComponent(jsRuntime);

            await InvokePrivateTask(component, "AdjustPageSize");

            Assert.That(GetPrivateField<int>(component, "pageSize"), Is.EqualTo(LogDataTableLayout.kDefaultPageSize),
                "a failed measurement must leave the table usable instead of emptying its pages");
        }

        [Test]
        public async Task AdjustPageSize_DoesNotMeasureAnEmptyTable()
        {
            LogDataTableTestJsRuntime jsRuntime = new() { MeasuredRows = 40 };
            LogDataTableTestApiConn apiConnection = new() { FailQuery = true };
            LogDataTable component = CreateComponent(apiConnection, ownerId: 7);
            SetPrivateProperty<IJSRuntime>(component, "jsRuntime", jsRuntime);
            await InvokeOnParametersSetAsync(component);

            await InvokePrivateTask(component, "AdjustPageSize");

            Assert.Multiple(() =>
            {
                Assert.That(jsRuntime.InvocationCount, Is.Zero, "there is no rendered row to measure");
                Assert.That(GetPrivateField<int>(component, "pageSize"), Is.EqualTo(LogDataTableLayout.kDefaultPageSize));
            });
        }

        [Test]
        public async Task AdjustPageSize_MeasuresAgainAfterTheOwnerChanged()
        {
            LogDataTableTestJsRuntime jsRuntime = new() { MeasuredRows = 40 };
            LogDataTable component = await CreateLoadedComponent(jsRuntime);
            await InvokePrivateTask(component, "AdjustPageSize");
            Assert.That(GetPrivateField<bool>(component, "pageSizeMeasured"), Is.True);

            SetPrivateProperty(component, nameof(LogDataTable.OwnerId), 8);
            await InvokeOnParametersSetAsync(component);

            Assert.That(GetPrivateField<bool>(component, "pageSizeMeasured"), Is.False,
                "the rows of another owner may be higher, so the window has to be measured again");
        }

        [Test]
        public async Task AdjustPageSize_RetriesAfterAMeasurementThatCouldNotBeTaken()
        {
            LogDataTableTestJsRuntime jsRuntime = new() { Fail = true };
            LogDataTable component = await CreateLoadedComponent(jsRuntime);

            await InvokePrivateTask(component, "AdjustPageSize");

            Assert.That(GetPrivateField<bool>(component, "pageSizeMeasured"), Is.False,
                "a measurement which could not be taken must not be treated as the final one");
        }

        [Test]
        public async Task AdjustPageSize_DoesNotChangeThePageSizeWhileTheUserHasPagedIntoTheData()
        {
            LogDataTableTestJsRuntime jsRuntime = new() { MeasuredRows = 40 };
            LogDataTable component = await CreateLoadedComponent(jsRuntime);
            SetPrivateField(component, "logTable", CreateTableOnPage(2));

            await InvokePrivateTask(component, "AdjustPageSize");

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<int>(component, "pageSize"), Is.EqualTo(LogDataTableLayout.kDefaultPageSize),
                    "a page number means different rows once the page size changes, so the table must stay as it is");
                Assert.That(GetPrivateField<int>(component, "measuredPageSize"), Is.EqualTo(40),
                    "the measurement is remembered for when the user is back on the first page");
            });
        }

        [Test]
        public async Task AdjustPageSize_KeepsTheCurrentSizeWhenTheWindowCouldNotBeMeasured()
        {
            LogDataTableTestJsRuntime jsRuntime = new() { MeasuredRows = 40 };
            LogDataTable component = await CreateLoadedComponent(jsRuntime);
            await InvokePrivateTask(component, "AdjustPageSize");

            SetPrivateProperty<IJSRuntime>(component, "jsRuntime", new LogDataTableTestJsRuntime { Fail = true });
            await InvokePrivateTask(component, "AdjustPageSize");

            Assert.That(GetPrivateField<int>(component, "pageSize"), Is.EqualTo(40),
                "a measurement that could not be taken must leave the size the window had allowed");
        }

        [Test]
        public async Task AdjustPageSize_RaisesAWindowTooShortForOneRowToTheMinimum()
        {
            LogDataTableTestJsRuntime jsRuntime = new() { MeasuredRows = 0 };
            LogDataTable component = await CreateLoadedComponent(jsRuntime);

            await InvokePrivateTask(component, "AdjustPageSize");

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<int>(component, "pageSize"), Is.EqualTo(LogDataTableLayout.kMinPageSize),
                    "zero rows fitting is a measurement, so the floor applies instead of the old size");
                Assert.That(GetPrivateField<bool>(component, "pageSizeMeasured"), Is.True,
                    "a window measured as too short was measured, so it must not be retried on every render");
            });
        }

        private static BlazorTable.Table<OwnerFirewallLogEntry> CreateTableOnPage(int pageNumber)
        {
            BlazorTable.Table<OwnerFirewallLogEntry> table = new();
            PropertyInfo property = typeof(BlazorTable.Table<OwnerFirewallLogEntry>).GetProperty("PageNumber")
                ?? throw new MissingMemberException(typeof(BlazorTable.Table<OwnerFirewallLogEntry>).FullName, "PageNumber");
            property.SetValue(table, pageNumber);
            return table;
        }

        private static void SetPrivateField<T>(LogDataTable component, string fieldName, T value)
        {
            FieldInfo field = typeof(LogDataTable).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(LogDataTable).FullName, fieldName);
            field.SetValue(component, value);
        }

        private static async Task<LogDataTable> CreateLoadedComponent(IJSRuntime jsRuntime)
        {
            LogDataTable component = CreateComponent(new LogDataTableTestApiConn(), ownerId: 7);
            SetPrivateProperty(component, "jsRuntime", jsRuntime);
            await InvokeOnParametersSetAsync(component);
            return component;
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
        /// Stands in for the browser: answers the row measurement of LogDataTable with a fixed
        /// number, or refuses it the way a circuit without a browser would.
        /// </summary>
        private sealed class LogDataTableTestJsRuntime : IJSRuntime
        {
            public int MeasuredRows { get; init; }
            public bool Fail { get; init; }
            public int InvocationCount { get; private set; }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                InvocationCount++;
                if (Fail)
                {
                    throw new InvalidOperationException("no browser attached");
                }
                return ValueTask.FromResult((TValue)(object)MeasuredRows);
            }
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
