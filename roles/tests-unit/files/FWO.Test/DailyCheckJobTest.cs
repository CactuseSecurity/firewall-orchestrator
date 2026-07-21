using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Jobs;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class DailyCheckJobTest
    {
        private static readonly DailyCheckModule[] ExpectedImportsModules =
        [
            DailyCheckModule.Imports,
            DailyCheckModule.OwnerActiveRules
        ];

        private static readonly AlertCode[] ExpectedEnabledCheckAlertCodes =
        [
            AlertCode.SampleDataExisting,
            AlertCode.ImportRunningTooLong
        ];

        private static readonly AlertCode[] ExpectedSampleDataAlertCodes =
        [
            AlertCode.SampleDataExisting
        ];

        private static readonly AlertCode[] ExpectedImportAlertCodes =
        [
            AlertCode.ImportRunningTooLong,
            AlertCode.NoImport,
            AlertCode.SuccessfulImportOverdue
        ];

        private static readonly int[] ExpectedSuccessLogSeverities = [1];
        private static readonly int[] ExpectedNoAlertLogSeverities = [0];

        [Test]
        public void LoadEnabledModules_ReturnsAllModules_WhenConfigIsBlank()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                DailyCheckModules = ""
            };

            HashSet<DailyCheckModule> enabledModules = InvokeLoadEnabledModules(globalConfig);

            Assert.That(enabledModules, Is.EquivalentTo(Enum.GetValues<DailyCheckModule>()));
        }

        [Test]
        public void LoadEnabledModules_ReturnsEmptySet_WhenConfigContainsEmptyList()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                DailyCheckModules = "[]"
            };

            HashSet<DailyCheckModule> enabledModules = InvokeLoadEnabledModules(globalConfig);

            Assert.That(enabledModules, Is.Empty);
        }

        [Test]
        public void LoadEnabledModules_ReturnsConfiguredSubset_WhenConfigContainsModules()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                DailyCheckModules = "[2,7]"
            };

            HashSet<DailyCheckModule> enabledModules = InvokeLoadEnabledModules(globalConfig);

            Assert.That(enabledModules, Is.EquivalentTo(ExpectedImportsModules));
        }

        [Test]
        public void LoadEnabledModules_ReturnsAllModules_WhenConfigContainsNull()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                DailyCheckModules = "null"
            };

            HashSet<DailyCheckModule> enabledModules = InvokeLoadEnabledModules(globalConfig);

            Assert.That(enabledModules, Is.EquivalentTo(Enum.GetValues<DailyCheckModule>()));
        }

        [Test]
        public void LoadEnabledModules_ReturnsAllModules_WhenConfigIsInvalid()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                DailyCheckModules = "{invalid json}"
            };

            HashSet<DailyCheckModule> enabledModules = InvokeLoadEnabledModules(globalConfig);

            Assert.That(enabledModules, Is.EquivalentTo(Enum.GetValues<DailyCheckModule>()));
        }

        [Test]
        public async Task Execute_DoesNotRunChecks_WhenNoModulesAreEnabled()
        {
            CountingApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                DailyCheckModules = "[]",
                RecCheckActive = true,
                RecRefreshDaily = true
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, globalConfig);

            await dailyCheckJob.Execute(null!);

            Assert.That(apiConnection.QueryCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Execute_SkipsRecertChecks_WhenDisabled()
        {
            CountingApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                DailyCheckModules = "[3,4]",
                RecRefreshDaily = false,
                RecCheckActive = false
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, globalConfig);

            await dailyCheckJob.Execute(null!);

            Assert.That(apiConnection.QueryCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Execute_LogsAlertWhenModuleProcessingThrows()
        {
            ThrowingDailyCheckApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                DailyCheckModules = "[1]"
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, globalConfig);

            await dailyCheckJob.Execute(null!);

            Assert.That(apiConnection.LogCount, Is.EqualTo(1));
            Assert.That(apiConnection.AlertCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Execute_RunsEnabledChecks_WhenDemoDataAndImportsAreEnabled()
        {
            DailyCheckApiConnection apiConnection = new()
            {
                Managements =
                [
                    new Management { Name = $"mgmt{GlobalConst.k_demo}" }
                ],
                ImportStatuses =
                [
                    new ImportStatus
                    {
                        MgmId = 1,
                        ImportDisabled = false,
                        LastIncompleteImport =
                        [
                            new ImportControl
                            {
                                StartTime = DateTime.Now.AddHours(-3)
                            }
                        ]
                    }
                ]
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                DailyCheckModules = "[1,2]",
                MaxImportDuration = 1
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, globalConfig);

            await dailyCheckJob.Execute(null!);

            Assert.That(apiConnection.AlertCodes, Is.EquivalentTo(ExpectedEnabledCheckAlertCodes));
            Assert.That(apiConnection.LogSeverities, Is.EqualTo([1, 1]));
        }

        [Test]
        public async Task GetRequestingOwner_ReturnsNull_WhenOwnerIdIsNull()
        {
            DailyCheckJob dailyCheckJob = new(new CountingApiConnection(), new SimulatedGlobalConfig());
            MethodInfo getRequestingOwner = typeof(DailyCheckJob).GetMethod("GetRequestingOwner", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("GetRequestingOwner method not found.");

            Task<FwoOwner?> task = (Task<FwoOwner?>)(getRequestingOwner.Invoke(dailyCheckJob, [null])
                ?? throw new InvalidOperationException("GetRequestingOwner returned null task."));
            FwoOwner? owner = await task;

            Assert.That(owner, Is.Null);
        }

        [Test]
        public async Task GetRequestingOwner_ReturnsOwner_WhenOwnerExists()
        {
            OwnerLookupApiConnection apiConnection = new()
            {
                Owner = new FwoOwner { Id = 42, Name = "Owner A", ExtAppId = "APP-42" }
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, new SimulatedGlobalConfig());
            MethodInfo getRequestingOwner = typeof(DailyCheckJob).GetMethod("GetRequestingOwner", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("GetRequestingOwner method not found.");

            Task<FwoOwner?> task = (Task<FwoOwner?>)(getRequestingOwner.Invoke(dailyCheckJob, [42])
                ?? throw new InvalidOperationException("GetRequestingOwner returned null task."));
            FwoOwner? owner = await task;

            Assert.That(owner, Is.Not.Null);
            Assert.That(owner!.Id, Is.EqualTo(42));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GetRequestingOwner_ReturnsNull_WhenOwnerLookupThrows()
        {
            FailingOwnerLookupApiConnection apiConnection = new();
            DailyCheckJob dailyCheckJob = new(apiConnection, new SimulatedGlobalConfig());
            MethodInfo getRequestingOwner = typeof(DailyCheckJob).GetMethod("GetRequestingOwner", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("GetRequestingOwner method not found.");

            Task<FwoOwner?> task = (Task<FwoOwner?>)(getRequestingOwner.Invoke(dailyCheckJob, [42])
                ?? throw new InvalidOperationException("GetRequestingOwner returned null task."));
            FwoOwner? owner = await task;

            Assert.That(owner, Is.Null);
            Assert.That(apiConnection.OwnerLookupCount, Is.EqualTo(1));
            Assert.That(apiConnection.LogEntryCount, Is.EqualTo(1));
            Assert.That(apiConnection.AlertCount, Is.EqualTo(1));
        }

        [Test]
        public async Task CheckDemoData_LogsNoSampleData_WhenNothingMatches()
        {
            DailyCheckApiConnection apiConnection = new()
            {
                LdapConnections =
                [
                    new FWO.Middleware.Server.Ldap
                    {
                        UserSearchPath = "ou=users,dc=test"
                    }
                ]
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, new SimulatedGlobalConfig());
            MethodInfo checkDemoData = typeof(DailyCheckJob).GetMethod("CheckDemoData", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CheckDemoData method not found.");

            await (Task)checkDemoData.Invoke(dailyCheckJob, null)!;

            Assert.That(apiConnection.AlertCodes, Is.Empty);
            Assert.That(apiConnection.LogSeverities, Is.EqualTo(ExpectedNoAlertLogSeverities));
        }

        [Test]
        public async Task CheckDemoData_CreatesAlert_WhenDemoDataIsFound()
        {
            DailyCheckApiConnection apiConnection = new()
            {
                Managements =
                [
                    new Management { Name = $"mgmt{GlobalConst.k_demo}" }
                ]
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, new SimulatedGlobalConfig());
            MethodInfo checkDemoData = typeof(DailyCheckJob).GetMethod("CheckDemoData", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CheckDemoData method not found.");

            await (Task)checkDemoData.Invoke(dailyCheckJob, null)!;

            Assert.That(apiConnection.AlertCodes, Is.EqualTo(ExpectedSampleDataAlertCodes));
            Assert.That(apiConnection.LogSeverities, Is.EqualTo(ExpectedSuccessLogSeverities));
        }

        [Test]
        public void ConstructLink_ReturnsExpectedModellingUrl()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                UiHostName = "https://fwo.example"
            };
            DailyCheckJob dailyCheckJob = new(new CountingApiConnection(), globalConfig);
            MethodInfo constructLink = typeof(DailyCheckJob).GetMethod("ConstructLink", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ConstructLink method not found.");
            FwoOwner owner = new() { Name = "Owner A", ExtAppId = "APP-42" };
            WfReqTask reqTask = new() { Title = "Interface Request" };
            reqTask.SetAddInfo(AdditionalInfoKeys.ConnId, "123");

            string link = (string)(constructLink.Invoke(dailyCheckJob, [owner, reqTask])
                ?? throw new InvalidOperationException("ConstructLink returned null."));

            Assert.That(link, Is.EqualTo($"<a target=\"_blank\" href=\"https://fwo.example/{PageName.Modelling}/APP-42/123\">Interface Request</a>"));
        }

        [Test]
        public void ConstructLink_UsesLocalizedFallbackTitle_WhenTaskTitleIsMissing()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                UiHostName = "https://fwo.example"
            };
            DailyCheckJob dailyCheckJob = new(new CountingApiConnection(), globalConfig);
            MethodInfo constructLink = typeof(DailyCheckJob).GetMethod("ConstructLink", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ConstructLink method not found.");
            FwoOwner owner = new() { Name = "Owner A", ExtAppId = "APP-42" };

            string link = (string)(constructLink.Invoke(dailyCheckJob, [owner, null])
                ?? throw new InvalidOperationException("ConstructLink returned null."));

            Assert.That(link, Is.EqualTo($"<a target=\"_blank\" href=\"https://fwo.example/{PageName.Modelling}/APP-42/\">Interface</a>"));
        }

        [Test]
        public async Task PrepareBody_ReplacesAllKnownPlaceholders()
        {
            OwnerLookupApiConnection apiConnection = new()
            {
                Owner = new FwoOwner { Id = 7, Name = "Requesting App", ExtAppId = "REQ-7" }
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                UiHostName = "https://fwo.example",
                ModUnansweredReqEmailBody = string.Join("|", new[]
                {
                    Placeholder.REQUESTER,
                    Placeholder.REQUESTDATE,
                    Placeholder.REQUESTING_APPNAME,
                    Placeholder.REQUESTING_APPID,
                    Placeholder.APPNAME,
                    Placeholder.APPID,
                    Placeholder.INTERFACE_LINK
                })
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, globalConfig);
            MethodInfo prepareBody = typeof(DailyCheckJob).GetMethod("PrepareBody", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("PrepareBody method not found.");
            WfReqTask reqTask = new()
            {
                Title = "Interface Request",
                TaskType = WfTaskType.new_interface.ToString()
            };
            reqTask.SetAddInfo(AdditionalInfoKeys.ConnId, "123");
            reqTask.SetAddInfo(AdditionalInfoKeys.ReqOwner, "7");
            WfTicket ticket = new()
            {
                CreationDate = new DateTime(2025, 1, 2),
                Requester = new UiUser { Name = "Requester A" },
                Tasks = [reqTask]
            };
            FwoOwner owner = new() { Name = "Owner A", ExtAppId = "APP-42" };

            Task<string> task = (Task<string>)(prepareBody.Invoke(dailyCheckJob, [ticket, owner])
                ?? throw new InvalidOperationException("PrepareBody returned null task."));
            string body = await task;

            Assert.That(body, Is.EqualTo(
                $"Requester A|02.01.2025|Requesting App|REQ-7|Owner A|APP-42|<a target=\"_blank\" href=\"https://fwo.example/{PageName.Modelling}/APP-42/123\">Interface Request</a>"));
        }

        [Test]
        public async Task CheckImports_CreatesAlertsAndWarningLog_WhenIssuesAreFound()
        {
            ImportStatusRecordingApiConnection apiConnection = new()
            {
                ImportStatuses =
                [
                    new ImportStatus
                    {
                        MgmId = 1,
                        ImportDisabled = false,
                        LastIncompleteImport =
                        [
                            new ImportControl
                            {
                                StartTime = DateTime.Now.AddHours(-3)
                            }
                        ]
                    },
                    new ImportStatus
                    {
                        MgmId = 2,
                        ImportDisabled = false,
                        LastImport = []
                    },
                    new ImportStatus
                    {
                        MgmId = 3,
                        ImportDisabled = false,
                        LastImport = [new ImportControl()],
                        LastImportAttempt = DateTime.Now.AddHours(-5)
                    },
                    new ImportStatus
                    {
                        MgmId = 4,
                        ImportDisabled = true
                    }
                ]
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                MaxImportDuration = 1,
                MaxImportInterval = 2
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, globalConfig);
            MethodInfo checkImports = typeof(DailyCheckJob).GetMethod("CheckImports", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CheckImports method not found.");

            Task task = (Task)(checkImports.Invoke(dailyCheckJob, null)
                ?? throw new InvalidOperationException("CheckImports returned null task."));
            await task;

            Assert.That(apiConnection.AlertCodes, Is.EquivalentTo(ExpectedImportAlertCodes));
            Assert.That(apiConnection.LogSeverities, Is.EqualTo(ExpectedSuccessLogSeverities));
        }

        private static HashSet<DailyCheckModule> InvokeLoadEnabledModules(SimulatedGlobalConfig globalConfig)
        {
            DailyCheckJob dailyCheckJob = new(new CountingApiConnection(), globalConfig);
            MethodInfo loadEnabledModules = typeof(DailyCheckJob).GetMethod("LoadEnabledModules", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("LoadEnabledModules method not found.");

            return (HashSet<DailyCheckModule>)(loadEnabledModules.Invoke(dailyCheckJob, null)
                ?? throw new InvalidOperationException("LoadEnabledModules returned null."));
        }

        private sealed class CountingApiConnection : SimulatedApiConnection
        {
            public int QueryCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;
                throw new InvalidOperationException("No query should be executed in this test.");
            }
        }

        private sealed class ThrowingDailyCheckApiConnection : SimulatedApiConnection
        {
            public int QueryCount { get; private set; }
            public int LogCount { get; private set; }
            public int AlertCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;

                if (query == DeviceQueries.getManagementsDetails && typeof(QueryResponseType) == typeof(List<Management>))
                {
                    throw new InvalidOperationException("boom");
                }

                if (query == MonitorQueries.getOpenAlerts && typeof(QueryResponseType) == typeof(List<Alert>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Alert>());
                }

                if (query == MonitorQueries.addLogEntry && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    LogCount++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1 }]
                    });
                }

                if (query == MonitorQueries.addAlert && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    AlertCount++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewIdLong = 1 }]
                    });
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }

        private sealed class DailyCheckApiConnection : SimulatedApiConnection
        {
            public List<Management> Managements { get; set; } = [];
            public List<ImportCredential> Credentials { get; set; } = [];
            public List<UiUser> Users { get; set; } = [];
            public List<Tenant> Tenants { get; set; } = [];
            public List<FWO.Middleware.Server.Ldap> LdapConnections { get; set; } = [];
            public List<FwoOwner> Owners { get; set; } = [];
            public List<ImportStatus> ImportStatuses { get; set; } = [];
            public List<AlertCode> AlertCodes { get; } = [];
            public List<int> LogSeverities { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == DeviceQueries.getManagementsDetails && typeof(QueryResponseType) == typeof(List<Management>))
                {
                    return Task.FromResult((QueryResponseType)(object)Managements);
                }

                if (query == DeviceQueries.getCredentialsWithoutSecrets && typeof(QueryResponseType) == typeof(List<ImportCredential>))
                {
                    return Task.FromResult((QueryResponseType)(object)Credentials);
                }

                if (query == AuthQueries.getUsers && typeof(QueryResponseType) == typeof(List<UiUser>))
                {
                    return Task.FromResult((QueryResponseType)(object)Users);
                }

                if (query == AuthQueries.getTenants && typeof(QueryResponseType) == typeof(List<Tenant>))
                {
                    return Task.FromResult((QueryResponseType)(object)Tenants);
                }

                if (query == AuthQueries.getLdapConnections && typeof(QueryResponseType) == typeof(List<FWO.Middleware.Server.Ldap>))
                {
                    return Task.FromResult((QueryResponseType)(object)LdapConnections);
                }

                if (query == OwnerQueries.getOwners && typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    return Task.FromResult((QueryResponseType)(object)Owners);
                }

                if (query == MonitorQueries.getImportStatus && typeof(QueryResponseType) == typeof(List<ImportStatus>))
                {
                    return Task.FromResult((QueryResponseType)(object)ImportStatuses);
                }

                if (query == MonitorQueries.getOpenAlerts && typeof(QueryResponseType) == typeof(List<Alert>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Alert>());
                }

                if (query == MonitorQueries.addAlert && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    object variablesObject = variables ?? throw new InvalidOperationException("Alert variables missing.");
                    int alertCode = (int)(variablesObject.GetType().GetProperty("alertCode")?.GetValue(variablesObject)
                        ?? throw new InvalidOperationException("Alert code missing."));
                    AlertCodes.Add((AlertCode)alertCode);
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = AlertCodes.Count }] });
                }

                if (query == MonitorQueries.addLogEntry && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    object variablesObject = variables ?? throw new InvalidOperationException("Log variables missing.");
                    int severity = (int)(variablesObject.GetType().GetProperty("severity")?.GetValue(variablesObject)
                        ?? throw new InvalidOperationException("Severity missing."));
                    LogSeverities.Add(severity);
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = LogSeverities.Count }] });
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }

        private sealed class OwnerLookupApiConnection : SimulatedApiConnection
        {
            public int QueryCount { get; private set; }
            public FwoOwner? Owner { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;
                if (query == OwnerQueries.getOwnerById && typeof(QueryResponseType) == typeof(FwoOwner) && Owner != null)
                {
                    return Task.FromResult((QueryResponseType)(object)Owner);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }

        private sealed class FailingOwnerLookupApiConnection : SimulatedApiConnection
        {
            public int OwnerLookupCount { get; private set; }
            public int LogEntryCount { get; private set; }
            public int AlertCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == OwnerQueries.getOwnerById)
                {
                    OwnerLookupCount++;
                    throw new InvalidOperationException("Owner lookup failed.");
                }

                if (query == MonitorQueries.addLogEntry && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    LogEntryCount++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1 }]
                    });
                }

                if (query == MonitorQueries.getOpenAlerts && typeof(QueryResponseType) == typeof(List<Alert>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Alert>());
                }

                if (query == MonitorQueries.addAlert && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    AlertCount++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewIdLong = 1 }]
                    });
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }

        private sealed class ImportStatusRecordingApiConnection : SimulatedApiConnection
        {
            public List<ImportStatus> ImportStatuses { get; set; } = [];
            public List<AlertCode> AlertCodes { get; } = [];
            public List<int> LogSeverities { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == MonitorQueries.getImportStatus && typeof(QueryResponseType) == typeof(List<ImportStatus>))
                {
                    return Task.FromResult((QueryResponseType)(object)ImportStatuses);
                }

                if (query == MonitorQueries.getOpenAlerts && typeof(QueryResponseType) == typeof(List<Alert>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Alert>());
                }

                if (query == MonitorQueries.addAlert && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    object variablesObject = variables ?? throw new InvalidOperationException("Alert variables missing.");
                    int alertCode = (int)(variablesObject.GetType().GetProperty("alertCode")?.GetValue(variablesObject)
                        ?? throw new InvalidOperationException("Alert code missing."));
                    AlertCodes.Add((AlertCode)alertCode);

                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewIdLong = AlertCodes.Count }]
                    });
                }

                if (query == MonitorQueries.addLogEntry && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    object variablesObject = variables ?? throw new InvalidOperationException("Log variables missing.");
                    int severity = (int)(variablesObject.GetType().GetProperty("severity")?.GetValue(variablesObject)
                        ?? throw new InvalidOperationException("Severity missing."));
                    LogSeverities.Add(severity);

                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = LogSeverities.Count }]
                    });
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
