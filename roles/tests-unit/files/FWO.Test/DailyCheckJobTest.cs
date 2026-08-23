using System.Reflection;
using System.IO;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Jobs;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class DailyCheckJobTest
    {
        private static readonly string[] ExpectedModUnansweredReqEmailBodyPlaceholders =
        [
            Placeholder.REQUESTER,
            Placeholder.REQUESTDATE,
            Placeholder.REQUESTING_APPNAME,
            Placeholder.REQUESTING_APPID,
            Placeholder.APPNAME,
            Placeholder.APPID,
            Placeholder.INTERFACE_LINK
        ];

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
        private static readonly long[] ExpectedUpdatedNotificationIds = [11L];

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
        public void GetInterfaceRequestCutOffPeriod_UsesInitialPlusRepeatTimesRepetitionsPlusOne()
        {
            MethodInfo helper = typeof(DailyCheckJob).GetMethod("GetInterfaceRequestCutOffPeriod", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("GetInterfaceRequestCutOffPeriod method not found.");

            object?[] noRepeatsArgs = [new FwoNotification
            {
                InitialOffsetAfterDeadline = 3,
                RepeatOffsetAfterDeadline = 7,
                RepetitionsAfterDeadline = 0
            }, SchedulerInterval.Days];
            int noRepeats = (int)(helper.Invoke(null, noRepeatsArgs) ?? throw new InvalidOperationException("Helper returned null."));

            object?[] oneRepeatArgs = [new FwoNotification
            {
                InitialOffsetAfterDeadline = 3,
                RepeatOffsetAfterDeadline = 7,
                RepetitionsAfterDeadline = 1
            }, SchedulerInterval.Days];
            int oneRepeat = (int)(helper.Invoke(null, oneRepeatArgs) ?? throw new InvalidOperationException("Helper returned null."));

            object?[] nullableArgs = [new FwoNotification(), SchedulerInterval.Days];
            int nullableValues = (int)(helper.Invoke(null, nullableArgs) ?? throw new InvalidOperationException("Helper returned null."));

            Assert.Multiple(() =>
            {
                Assert.That(noRepeats, Is.EqualTo(10));
                Assert.That(oneRepeat, Is.EqualTo(17));
                Assert.That(nullableValues, Is.Zero);
            });
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
        public async Task CheckRecerts_DoesNothingWhenDisabled()
        {
            CountingApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                RecCheckActive = false
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, globalConfig);
            MethodInfo checkRecerts = typeof(DailyCheckJob).GetMethod("CheckRecerts", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CheckRecerts method not found.");

            Task task = (Task)(checkRecerts.Invoke(dailyCheckJob, null)
                ?? throw new InvalidOperationException("CheckRecerts returned null task."));
            await task;

            Assert.That(apiConnection.QueryCount, Is.EqualTo(0));
        }

        [Test]
        public async Task CheckRecerts_RunsRecertCheckAndWritesLogWhenEnabled()
        {
            RecordingRecertCheckApiConnection apiConnection = new()
            {
                Ldaps =
                [
                    CreateInternalTestLdap()
                ],
                Users =
                [
                    new UiUser { Dn = "cn=user,dc=test", Email = "user@example.test" }
                ],
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Owner A", RecertActive = false }
                ]
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                RecCheckActive = true,
                RecertificationMode = RecertificationMode.OwnersAndRules,
                RecCheckParams = JsonSerializer.Serialize(new RecertCheckParams
                {
                    RecertCheckInterval = SchedulerInterval.Days,
                    RecertCheckOffset = 7
                }),
                RecCheckEmailSubject = "Recertification check",
                RecCheckEmailUpcomingText = "upcoming",
                RecCheckEmailOverdueText = "overdue",
                DefaultLanguage = GlobalConst.kEnglish,
                UseDummyEmailAddress = true,
                DummyEmailAddress = "dummy@example.test"
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, globalConfig);
            MethodInfo checkRecerts = typeof(DailyCheckJob).GetMethod("CheckRecerts", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CheckRecerts method not found.");

            Task task = (Task)(checkRecerts.Invoke(dailyCheckJob, null)
                ?? throw new InvalidOperationException("CheckRecerts returned null task."));
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.CountQuery(AuthQueries.getLdapConnections), Is.EqualTo(2));
                Assert.That(apiConnection.CountQuery(AuthQueries.getUsers), Is.EqualTo(1));
                Assert.That(apiConnection.CountQuery(OwnerQueries.getOwners), Is.EqualTo(1));
                Assert.That(apiConnection.CountQuery(NotificationQueries.getNotifications), Is.EqualTo(1));
                Assert.That(apiConnection.CountQuery(NotificationQueries.updateNotificationsLastSent), Is.EqualTo(1));
                Assert.That(apiConnection.CountQuery(MonitorQueries.addLogEntry), Is.EqualTo(1));
            });
        }

        private static TestableLdap CreateInternalTestLdap()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults()
            };

            return new TestableLdap(client)
            {
                TenantLevel = 1,
                UserSearchPath = "ou=users,dc=fworch,dc=internal",
                GroupSearchPath = "ou=groups,dc=fworch,dc=internal",
                Active = true
            };
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
            Assert.That(apiConnection.LogSeverities, Has.Count.EqualTo(2));
            Assert.That(apiConnection.LogSeverities[0], Is.EqualTo(1));
            Assert.That(apiConnection.LogSeverities[1], Is.EqualTo(1));
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
        [NonParallelizable]
        public async Task CheckUnansweredInterfaceRequests_LogsWarningAndSkipsTicketWithoutOwner()
        {
            DailyCheckInterfaceRequestsApiConnection apiConnection = new()
            {
                LdapConnections =
                [
                    CreateInternalTestLdap()
                ],
                Notifications =
                [
                    CreateInterfaceRequestNotification(11)
                ],
                OpenTickets =
                [
                    CreateInterfaceRequestTicket(501, null)
                ]
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                UseDummyEmailAddress = true,
                DummyEmailAddress = "dummy@example.test",
                ModUnansweredReqEmailBody = "body"
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, globalConfig);
            MethodInfo checkUnansweredInterfaceRequests = typeof(DailyCheckJob).GetMethod("CheckUnansweredInterfaceRequests", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CheckUnansweredInterfaceRequests method not found.");
            Func<GlobalStateMatrix> previousFactory = GlobalStateMatrix.Factory;
            GlobalStateMatrix.Factory = () => new TestGlobalStateMatrix();

            try
            {
                await (Task)(checkUnansweredInterfaceRequests.Invoke(dailyCheckJob, null)
                    ?? throw new InvalidOperationException("CheckUnansweredInterfaceRequests returned null task."));

                Assert.Multiple(() =>
                {
                    Assert.That(apiConnection.NotificationLoadCount, Is.EqualTo(1));
                    Assert.That(apiConnection.OpenTicketQueryCount, Is.EqualTo(1));
                    Assert.That(apiConnection.UpdatedNotificationIds, Is.Empty);
                });
            }
            finally
            {
                GlobalStateMatrix.Factory = previousFactory;
            }
        }

        [Test]
        [NonParallelizable]
        public async Task CheckUnansweredInterfaceRequests_SendsDueNotificationForOwnedTicket()
        {
            DailyCheckInterfaceRequestsApiConnection apiConnection = new()
            {
                LdapConnections =
                [
                    CreateInternalTestLdap()
                ],
                Notifications =
                [
                    CreateInterfaceRequestNotification(11)
                ],
                OpenTickets =
                [
                    CreateInterfaceRequestTicket(501, new FwoOwner { Id = 7, Name = "Owner A", ExtAppId = "APP-7" }, DateTime.Now.AddDays(-1))
                ]
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                UseDummyEmailAddress = true,
                DummyEmailAddress = "dummy@example.test",
                ModUnansweredReqEmailBody = "body"
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, globalConfig);
            MethodInfo checkUnansweredInterfaceRequests = typeof(DailyCheckJob).GetMethod("CheckUnansweredInterfaceRequests", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CheckUnansweredInterfaceRequests method not found.");
            Func<GlobalStateMatrix> previousFactory = GlobalStateMatrix.Factory;
            GlobalStateMatrix.Factory = () => new TestGlobalStateMatrix();

            try
            {
                await (Task)(checkUnansweredInterfaceRequests.Invoke(dailyCheckJob, null)
                    ?? throw new InvalidOperationException("CheckUnansweredInterfaceRequests returned null task."));

                Assert.Multiple(() =>
                {
                    Assert.That(apiConnection.NotificationLoadCount, Is.EqualTo(1));
                    Assert.That(apiConnection.OpenTicketQueryCount, Is.EqualTo(1));
                    Assert.That(apiConnection.UpdatedNotificationIds, Is.EqualTo(ExpectedUpdatedNotificationIds));
                });
            }
            finally
            {
                GlobalStateMatrix.Factory = previousFactory;
            }
        }

        [Test]
        [NonParallelizable]
        public async Task CheckUnansweredInterfaceRequests_LogsWarningWhenRecipientsCannotBeResolved()
        {
            DailyCheckInterfaceRequestsApiConnection apiConnection = new()
            {
                LdapConnections =
                [
                    CreateInternalTestLdap()
                ],
                Notifications =
                [
                    CreateInterfaceRequestNotificationWithoutRecipients(11)
                ],
                OpenTickets =
                [
                    CreateInterfaceRequestTicket(501, new FwoOwner { Id = 7, Name = "Owner A", ExtAppId = "APP-7" }, DateTime.Now.AddDays(-1))
                ]
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                UseDummyEmailAddress = false,
                DummyEmailAddress = "dummy@example.test",
                ModUnansweredReqEmailBody = "body"
            };
            DailyCheckJob dailyCheckJob = new(apiConnection, globalConfig);
            MethodInfo checkUnansweredInterfaceRequests = typeof(DailyCheckJob).GetMethod("CheckUnansweredInterfaceRequests", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CheckUnansweredInterfaceRequests method not found.");
            Func<GlobalStateMatrix> previousFactory = GlobalStateMatrix.Factory;
            GlobalStateMatrix.Factory = () => new TestGlobalStateMatrix();

            try
            {
                string output = await CaptureConsoleAsync(async () =>
                {
                    await (Task)(checkUnansweredInterfaceRequests.Invoke(dailyCheckJob, null)
                        ?? throw new InvalidOperationException("CheckUnansweredInterfaceRequests returned null task."));
                });

                Assert.Multiple(() =>
                {
                    Assert.That(output, Does.Contain("No recipients resolved for configured responsibles while preparing notification client InterfaceRequest."));
                    Assert.That(output, Does.Contain("Reminder notification 11 was due for unanswered interface request ticket 501, but no email was sent. Check recipient resolution and due settings."));
                    Assert.That(output, Does.Contain("Unanswered Interface Requests Check: Sent 0 emails."));
                    Assert.That(apiConnection.NotificationLoadCount, Is.EqualTo(1));
                    Assert.That(apiConnection.OpenTicketQueryCount, Is.EqualTo(1));
                    Assert.That(apiConnection.UpdatedNotificationIds, Is.Empty);
                });
            }
            finally
            {
                GlobalStateMatrix.Factory = previousFactory;
            }
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
                ModUnansweredReqEmailBody = string.Join("|", ExpectedModUnansweredReqEmailBodyPlaceholders)
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

        private static FwoNotification CreateInterfaceRequestNotification(int id)
        {
            return new FwoNotification
            {
                Id = id,
                NotificationClient = NotificationClient.InterfaceRequest,
                RecipientTo = EmailRecipientOption.OtherAddresses,
                EmailAddressTo = "notify@example.test",
                EmailSubject = "subject",
                EmailBody = "body",
                Deadline = NotificationDeadline.RequestDate,
                RepeatIntervalAfterDeadline = SchedulerInterval.Days,
                InitialOffsetAfterDeadline = 0,
                RepeatOffsetAfterDeadline = 1,
                RepetitionsAfterDeadline = 3
            };
        }

        private static WfTicket CreateInterfaceRequestTicket(long ticketId, FwoOwner? owner, DateTime? creationDate = null)
        {
            WfReqTask reqTask = new()
            {
                Id = ticketId + 1,
                TaskType = WfTaskType.new_interface.ToString(),
                Title = "Interface request"
            };
            if (owner != null)
            {
                reqTask.Owners = [new FwoOwnerDataHelper { Owner = owner }];
            }

            return new WfTicket
            {
                Id = ticketId,
                CreationDate = creationDate ?? DateTime.Now.AddDays(-7),
                Requester = new UiUser { Name = "Requester A" },
                Tasks = [reqTask]
            };
        }

        private static FwoNotification CreateInterfaceRequestNotificationWithoutRecipients(int id)
        {
            return new FwoNotification
            {
                Id = id,
                NotificationClient = NotificationClient.InterfaceRequest,
                RecipientTo = EmailRecipientOption.ConfiguredResponsibles,
                EmailAddressTo = "",
                EmailSubject = "subject",
                EmailBody = "body",
                Deadline = NotificationDeadline.RequestDate,
                RepeatIntervalAfterDeadline = SchedulerInterval.Days,
                InitialOffsetAfterDeadline = 0,
                RepeatOffsetAfterDeadline = 1,
                RepetitionsAfterDeadline = 3
            };
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

        private sealed class TestGlobalStateMatrix : GlobalStateMatrix
        {
            public override async Task Init(ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master)
            {
                await Task.CompletedTask;
                Dictionary<WorkflowPhases, StateMatrix> matrices = [];
                foreach (WorkflowPhases phase in Enum.GetValues<WorkflowPhases>())
                {
                    matrices[phase] = new StateMatrix
                    {
                        Active = true,
                        LowestInputState = 1,
                        LowestEndState = 10
                    };
                }
                GlobalMatrix = matrices;
            }
        }

        private sealed class DailyCheckInterfaceRequestsApiConnection : SimulatedApiConnection
        {
            public List<FWO.Middleware.Server.Ldap> LdapConnections { get; set; } = [];
            public List<FwoNotification> Notifications { get; set; } = [];
            public List<WfTicket> OpenTickets { get; set; } = [];
            public int NotificationLoadCount { get; private set; }
            public int OpenTicketQueryCount { get; private set; }
            public List<long> UpdatedNotificationIds { get; private set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == AuthQueries.getLdapConnections && typeof(QueryResponseType) == typeof(List<FWO.Middleware.Server.Ldap>))
                {
                    return Task.FromResult((QueryResponseType)(object)LdapConnections);
                }

                if (query == AuthQueries.getUserEmails && typeof(QueryResponseType) == typeof(List<UiUser>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<UiUser>());
                }

                if (query == ConfigQueries.getCustomTextsPerLanguage && typeof(QueryResponseType) == typeof(List<UiText>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<UiText>());
                }

                if (query == OwnerQueries.getOwnerResponsibleTypes && typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<OwnerResponsibleType>());
                }

                if (query == ConfigQueries.getConfigItemsByUser && typeof(QueryResponseType) == typeof(ConfigItem[]))
                {
                    return Task.FromResult((QueryResponseType)(object)Array.Empty<ConfigItem>());
                }

                if (query == RequestQueries.getStates && typeof(QueryResponseType) == typeof(List<WfState>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<WfState>());
                }

                if (query == DeviceQueries.getDeviceDetails && typeof(QueryResponseType) == typeof(List<Device>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Device>());
                }

                if (query == OwnerQueries.getOwners && typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
                }

                if (query == NotificationQueries.getNotifications && typeof(QueryResponseType) == typeof(List<FwoNotification>))
                {
                    NotificationLoadCount++;
                    return Task.FromResult((QueryResponseType)(object)Notifications);
                }

                if (query == RequestQueries.getTicketsByParameters && typeof(QueryResponseType) == typeof(List<WfTicket>))
                {
                    OpenTicketQueryCount++;
                    return Task.FromResult((QueryResponseType)(object)OpenTickets);
                }

                if (query == NotificationQueries.updateNotificationsLastSent && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    object vars = variables ?? throw new InvalidOperationException("Update notification variables missing.");
                    IEnumerable<int> ids = (IEnumerable<int>?)vars.GetType().GetProperty("ids")?.GetValue(vars) ?? [];
                    UpdatedNotificationIds = [.. ids.Select(id => (long)id)];
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = UpdatedNotificationIds.Count });
                }

                if (query == MonitorQueries.getOpenAlerts && typeof(QueryResponseType) == typeof(List<Alert>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Alert>());
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }

        private static async Task<string> CaptureConsoleAsync(Func<Task> action)
        {
            TextWriter originalOut = Console.Out;
            StringWriter writer = new();
            Console.SetOut(writer);
            try
            {
                await action();
                await writer.FlushAsync();
                return writer.ToString();
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        private sealed class RecordingRecertCheckApiConnection : SimulatedApiConnection
        {
            public List<(string Query, object? Variables)> Queries { get; } = [];
            public List<FWO.Middleware.Server.Ldap> Ldaps { get; set; } = [];
            public List<UiUser> Users { get; set; } = [];
            public List<FwoOwner> Owners { get; set; } = [];

            public int CountQuery(string query)
            {
                return Queries.Count(item => item.Query == query);
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add((query, variables));

                if (query == ConfigQueries.getCustomTextsPerLanguage && typeof(QueryResponseType) == typeof(List<UiText>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<UiText>());
                }

                if (query == AuthQueries.getLdapConnections && typeof(QueryResponseType) == typeof(List<FWO.Middleware.Server.Ldap>))
                {
                    return Task.FromResult((QueryResponseType)(object)Ldaps);
                }

                if (query == AuthQueries.getUsers && typeof(QueryResponseType) == typeof(List<UiUser>))
                {
                    return Task.FromResult((QueryResponseType)(object)Users);
                }

                if (query == OwnerQueries.getOwners && typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    return Task.FromResult((QueryResponseType)(object)Owners);
                }

                if (query == MonitorQueries.getOpenAlerts && typeof(QueryResponseType) == typeof(List<Alert>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Alert>());
                }

                if (query == MonitorQueries.addLogEntry && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = new ReturnId[] { new ReturnId { NewIdLong = 1 } }
                    });
                }

                if (query == NotificationQueries.getNotifications && typeof(QueryResponseType) == typeof(List<FwoNotification>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoNotification>());
                }

                if (query == NotificationQueries.updateNotificationsLastSent && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 0 });
                }

                if (query == ConfigQueries.getConfigItemsByUser && typeof(QueryResponseType) == typeof(ConfigItem[]))
                {
                    return Task.FromResult((QueryResponseType)(object)Array.Empty<ConfigItem>());
                }

                if (query == DeviceQueries.getDevicesByManagement && typeof(QueryResponseType) == typeof(List<ManagementSelect>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementSelect>());
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
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
