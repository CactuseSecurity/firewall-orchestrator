using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests that starting the provisioning of a ticket creates the external request of the next work list entry only:
    /// a firewall write referencing its implementation task, or a request task handed to the external ticket system
    /// selected in the device specific settings; nothing while a request of the ticket runs or nothing is left.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class ExternalRequestHandlerProvisioningTest
    {
        private const long kTicketId = 5;
        private const int kManagementId = 1;
        private const long kImplTaskId = 10;
        private const long kSecondImplTaskId = 11;
        private const long kOtherReqTaskImplTaskId = 20;
        private const int kOpenStateId = 210;
        private const int kDoneStateId = 631;

        private static readonly List<ExternalTicketSystem> kTicketSystems = [ExtTicketHandlerTest.ticketSystem];

        private static readonly WfReqElement kGroupMember = new()
        {
            Id = 1,
            TaskId = 1,
            RequestAction = RequestAction.create.ToString(),
            NetworkId = 100,
            GroupName = "ARxx12345-100",
            Name = "AppServerX",
            IpString = "10.0.0.1/32",
            Field = ElemFieldType.source.ToString()
        };

        /// <summary>
        /// Returns the ticket of the test and the unfinished requests of the ticket, and records the created requests.
        /// </summary>
        private sealed class ProvisioningApiConn(WfTicket ticket) : ExtTicketHandlerTestApiConn
        {
            public List<ExternalRequest> UnfinishedRequests { get; } = [];

            public List<object> CreatedRequests { get; } = [];

            public List<string> QueryLog { get; } = [];

            /// <summary>Descriptions of the alerts raised.</summary>
            public List<string> AlertDescriptions { get; } = [];

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                QueryLog.Add(query);
                if (query == ExtRequestQueries.getUnfinishedRequestsOfTicket)
                {
                    return (QueryResponseType)(object)UnfinishedRequests;
                }
                if (typeof(QueryResponseType) == typeof(WfTicket))
                {
                    return (QueryResponseType)(object)ticket;
                }
                if (query == RequestQueries.updateEffectiveRequestElementSnapshot)
                {
                    Dictionary<string, object?> payload = (Dictionary<string, object?>)variables!;
                    long elementId = Convert.ToInt64(payload["id"]);
                    WfReqElement element = ticket.Tasks.SelectMany(task => task.Elements)
                        .Single(candidate => candidate.Id == elementId);
                    element.IpString = payload["ip"]?.ToString();
                    element.Name = payload["name"]?.ToString();
                    element.GroupName = payload["groupName"]?.ToString();
                    ticket.UpdateCidrsInTaskElements();
                    return (QueryResponseType)(object)new ReturnId { UpdatedIdLong = elementId };
                }
                if (query == ExtRequestQueries.addExtRequest && variables != null)
                {
                    CreatedRequests.Add(variables);
                }
                if (query == MonitorQueries.getOpenAlerts)
                {
                    return (QueryResponseType)(object)new List<Alert>();
                }
                if (query == MonitorQueries.addAlert)
                {
                    AlertDescriptions.Add(variables?.GetType().GetProperty("description")?.GetValue(variables)?.ToString() ?? "");
                    return (QueryResponseType)(object)new ReturnIdWrapper();
                }
                return await base.SendQueryAsync<QueryResponseType>(query, variables, operationName, chunkingOptions);
            }
        }

        private static WfTicket Ticket(int implTaskStateId = kOpenStateId)
        {
            WfReqTask reqTask = new()
            {
                Id = 1,
                Title = "Group",
                TicketId = kTicketId,
                TaskNumber = 1,
                TaskType = WfTaskType.group_create.ToString(),
                ManagementId = kManagementId,
                StateId = kOpenStateId,
                Elements = [kGroupMember],
                AdditionalInfo = "{\"ConnId\":\"1\",\"GrpName\":\"ARxx12345-100\"}"
            };
            reqTask.ImplementationTasks.Add(new WfImplTask
            {
                Id = kImplTaskId,
                ReqTaskId = reqTask.Id,
                TaskNumber = 1,
                TaskType = reqTask.TaskType,
                ManagementId = kManagementId,
                StateId = implTaskStateId
            });
            return new WfTicket { Id = kTicketId, Title = "Ticket", Tasks = [reqTask] };
        }

        /// <summary>
        /// Two request tasks on a management implemented manually: the first with two implementation tasks, the first of
        /// them written already, the second with one open implementation task.
        /// </summary>
        private static WfTicket TicketWithTwoManualRequestTasks(int secondImplTaskStateId = kOpenStateId)
        {
            WfTicket ticket = Ticket(kDoneStateId);
            WfReqTask first = ticket.Tasks[0];
            first.ImplementationTasks.Add(new WfImplTask
            {
                Id = kSecondImplTaskId,
                ReqTaskId = first.Id,
                TaskNumber = 2,
                TaskType = first.TaskType,
                ManagementId = kManagementId,
                StateId = secondImplTaskStateId
            });
            WfReqTask second = new()
            {
                Id = 2,
                Title = "Other group",
                TicketId = kTicketId,
                TaskNumber = 2,
                TaskType = WfTaskType.group_create.ToString(),
                ManagementId = kManagementId,
                StateId = kOpenStateId
            };
            second.ImplementationTasks.Add(new WfImplTask
            {
                Id = kOtherReqTaskImplTaskId,
                ReqTaskId = second.Id,
                TaskNumber = 1,
                TaskType = second.TaskType,
                ManagementId = kManagementId,
                StateId = kOpenStateId
            });
            ticket.Tasks.Add(second);
            return ticket;
        }

        private static GlobalDeviceSpecificSettings ManualSettings()
        {
            return new GlobalDeviceSpecificSettings { ImplementationMode = ProvisioningImplementationMode.Manual };
        }

        /// <summary>A finished firewall write of the first request task released by a button.</summary>
        private static ExternalRequest FinishedRequestReleasingTheFirstRequestTask()
        {
            return new ExternalRequest
            {
                Id = 3,
                TicketId = kTicketId,
                TaskNumber = 1,
                ExtRequestType = WfTaskType.group_create.ToString(),
                ExtRequestState = ExtStates.ExtReqDone.ToString(),
                ExtQueryVariables = "{\"ManagementId\":[1],\"ButtonRelease\":[1]}"
            };
        }

        private static ExternalRequestHandler CreateHandler(ProvisioningApiConn apiConnection, GlobalDeviceSpecificSettings settings)
        {
            SimulatedUserConfig userConfig = new()
            {
                ReqPriorities = ExtTicketHandlerTest.reqPrios,
                ModNamingConvention = ExtTicketHandlerTest.namingConvention
            };
            return new ExternalRequestHandler(userConfig, apiConnection, null)
            {
                SCTemplateSource = new FixedSCTemplateSource(ExtTicketHandlerTest.ticketSystem.Templates)
            }.WithProvisioningSettings(settings);
        }

        private static GlobalDeviceSpecificSettings ExternalTicketSystemSettings()
        {
            return ExtTicketHandlerTest.ExternalTicketSystemSettings(ExtTicketHandlerTest.ticketSystem);
        }

        private static object? Variable(object variables, string name)
        {
            return variables.GetType().GetProperty(name)?.GetValue(variables);
        }

        [Test]
        public async Task StartProvisioning_AutomaticTarget_CreatesAFirewallWriteRequest()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, new GlobalDeviceSpecificSettings());

            bool result = await handler.StartProvisioning(kTicketId);

            Assert.That(result, Is.True);
            Assert.That(apiConnection.CreatedRequests, Has.Count.EqualTo(1));
            object request = apiConnection.CreatedRequests[0];
            Assert.Multiple(() =>
            {
                Assert.That(Variable(request, "implTaskId"), Is.EqualTo(kImplTaskId));
                Assert.That(Variable(request, "taskNumber"), Is.EqualTo(1));
                Assert.That(Variable(request, "extTicketSystem"), Is.EqualTo(""));
                Assert.That(Variable(request, "extTaskType"), Is.EqualTo(WfTaskType.group_create.ToString()));
                Assert.That(Variable(request, "extRequestState"), Is.EqualTo(ExtStates.ExtReqInitialized.ToString()));
                Assert.That(Variable(request, "extQueryVariables"), Is.EqualTo("{\"ManagementId\":[1]}"));
            });
        }

        [Test]
        public async Task StartProvisioning_ExternalManagement_HandsTheRequestTaskToTheSelectedSystem()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, ExternalTicketSystemSettings());

            bool result = await handler.StartProvisioning(kTicketId);

            Assert.That(result, Is.True);
            Assert.That(apiConnection.CreatedRequests, Has.Count.EqualTo(1));
            ExternalTicketSystem storedSystem = System.Text.Json.JsonSerializer.Deserialize<ExternalTicketSystem>(apiConnection.AddedExtTicketSystem ?? "")
                ?? throw new AssertionException("No external ticket system stored");
            Assert.Multiple(() =>
            {
                Assert.That(Variable(apiConnection.CreatedRequests[0], "implTaskId"), Is.Null);
                Assert.That(storedSystem.TypeId, Is.EqualTo(BuiltInExternalTicketSystemTypes.TufinSecureChangeId));
                Assert.That(storedSystem.Url, Is.EqualTo(ExtTicketHandlerTest.ticketSystem.Url));
                Assert.That(storedSystem.CredentialId, Is.EqualTo(ExtTicketHandlerTest.kTestCredentialId));
            });
        }

        [Test]
        public async Task SendFirstRequest_MaterializesGroupMembersBeforeConstructingExternalContent()
        {
            WfTicket ticket = Ticket();
            WfReqTask groupTask = ticket.Tasks[0];
            WfReqElement groupMember = new(kGroupMember)
            {
                TaskId = groupTask.Id,
                ObjectTaskId = 2,
                NetworkId = null,
                IpString = "10.0.0.10"
            };
            groupTask.Elements = [groupMember];
            WfReqTask objectTask = new()
            {
                Id = 2,
                TicketId = kTicketId,
                TaskNumber = 0,
                TaskType = WfTaskType.object_create.ToString(),
                StateId = kDoneStateId,
                Elements =
                [
                    new WfReqElement
                    {
                        Id = 20,
                        TaskId = 2,
                        Field = ElemFieldType.source.ToString(),
                        RequestAction = RequestAction.create.ToString(),
                        Name = "AppServerX",
                        IpString = "10.0.1.10"
                    }
                ]
            };
            ticket.Tasks.Insert(0, objectTask);
            ticket.UpdateCidrsInTaskElements();
            ProvisioningApiConn apiConnection = new(ticket);
            using ExternalRequestHandler handler = CreateHandler(apiConnection, ExternalTicketSystemSettings());

            bool result = await handler.StartProvisioning(kTicketId);

            string content = Variable(apiConnection.CreatedRequests.Single(), "extTaskContent")?.ToString() ?? "";
            int updateIndex = apiConnection.QueryLog.IndexOf(RequestQueries.updateEffectiveRequestElementSnapshot);
            int insertIndex = apiConnection.QueryLog.IndexOf(ExtRequestQueries.addExtRequest);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(updateIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(insertIndex, Is.GreaterThan(updateIndex));
                Assert.That(content, Does.Contain("10.0.1.10"));
                Assert.That(content, Does.Not.Contain("10.0.0.10"));
            });
        }

        [Test]
        public async Task StartProvisioning_ExternalManagement_StoresTheTimeoutAndRetriesOfTheManagement()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            GlobalDeviceSpecificSettings settings = ExternalTicketSystemSettings();
            settings.RequestResponseTimeout = 60;
            settings.RequestMaxAttempts = 4;
            settings.RequestCyclesBetweenAttempts = 9;
            using ExternalRequestHandler handler = CreateHandler(apiConnection, settings);

            await handler.StartProvisioning(kTicketId);

            ExternalTicketSystem storedSystem = System.Text.Json.JsonSerializer.Deserialize<ExternalTicketSystem>(apiConnection.AddedExtTicketSystem ?? "")
                ?? throw new AssertionException("No external ticket system stored");
            Assert.Multiple(() =>
            {
                Assert.That(storedSystem.ResponseTimeout, Is.EqualTo(60));
                Assert.That(storedSystem.MaxAttempts, Is.EqualTo(4));
                Assert.That(storedSystem.CyclesBetweenAttempts, Is.EqualTo(9));
            });
        }

        [Test]
        public async Task StartProvisioning_ConnectionWithoutCredential_Fails()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            GlobalDeviceSpecificSettings settings = ExternalTicketSystemSettings();
            settings.ScCredential = null;
            using ExternalRequestHandler handler = CreateHandler(apiConnection, settings);

            bool result = await handler.StartProvisioning(kTicketId);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(apiConnection.CreatedRequests, Is.Empty);
            });
        }

        [Test]
        public async Task StartProvisioning_RequestOfTheTicketStillRunning_CreatesNothing()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            apiConnection.UnfinishedRequests.Add(new ExternalRequest { Id = 3, TicketId = kTicketId });
            using ExternalRequestHandler handler = CreateHandler(apiConnection, new GlobalDeviceSpecificSettings());

            bool result = await handler.StartProvisioning(kTicketId);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CreatedRequests, Is.Empty);
            });
        }

        [Test]
        public async Task StartProvisioning_EverythingFinished_CreatesNothing()
        {
            ProvisioningApiConn apiConnection = new(Ticket(kDoneStateId));
            using ExternalRequestHandler handler = CreateHandler(apiConnection, new GlobalDeviceSpecificSettings());

            bool result = await handler.StartProvisioning(kTicketId);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CreatedRequests, Is.Empty);
            });
        }

        [Test]
        public async Task StartProvisioning_UnknownScopeTask_Fails()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, new GlobalDeviceSpecificSettings());

            bool result = await handler.StartProvisioning(kTicketId, 99);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(apiConnection.CreatedRequests, Is.Empty);
            });
        }

        [Test]
        public async Task StartProvisioning_ScopeTask_ProvisionsIt()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, new GlobalDeviceSpecificSettings());

            bool result = await handler.StartProvisioning(kTicketId, 1);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CreatedRequests, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task StartProvisioning_ManualTarget_CreatesNothing()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, ManualSettings());

            bool result = await handler.StartProvisioning(kTicketId);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CreatedRequests, Is.Empty);
            });
        }

        [Test]
        public async Task StartProvisioning_ManualTargetByButton_WritesItAndRemembersTheRelease()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, ManualSettings());

            bool result = await handler.StartProvisioning(kTicketId, null, null, true);

            Assert.That(result, Is.True);
            Assert.That(apiConnection.CreatedRequests, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(Variable(apiConnection.CreatedRequests[0], "implTaskId"), Is.EqualTo(kImplTaskId));
                Assert.That(Variable(apiConnection.CreatedRequests[0], "extQueryVariables"), Is.EqualTo("{\"ManagementId\":[1],\"ButtonRelease\":[0]}"));
            });
        }

        [Test]
        public async Task StartProvisioning_ScopeTaskByButton_RemembersItsNumber()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, ManualSettings());

            await handler.StartProvisioning(kTicketId, 1, null, true);

            Assert.That(apiConnection.CreatedRequests, Has.Count.EqualTo(1));
            Assert.That(Variable(apiConnection.CreatedRequests[0], "extQueryVariables"), Is.EqualTo("{\"ManagementId\":[1],\"ButtonRelease\":[1]}"));
        }

        [Test]
        public async Task StartProvisioning_AfterAReleasedRequest_WritesTheNextManualTargetOfTheReleasedRequestTask()
        {
            ProvisioningApiConn apiConnection = new(TicketWithTwoManualRequestTasks());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, ManualSettings());

            await handler.StartProvisioning(kTicketId, null, FinishedRequestReleasingTheFirstRequestTask());

            Assert.That(apiConnection.CreatedRequests, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(Variable(apiConnection.CreatedRequests[0], "implTaskId"), Is.EqualTo(kSecondImplTaskId));
                Assert.That(Variable(apiConnection.CreatedRequests[0], "extQueryVariables"), Is.EqualTo("{\"ManagementId\":[1],\"ButtonRelease\":[1]}"));
            });
        }

        [Test]
        public async Task StartProvisioning_AfterAReleasedRequest_LeavesManualTargetsOfOtherRequestTasksToAPerson()
        {
            ProvisioningApiConn apiConnection = new(TicketWithTwoManualRequestTasks(kDoneStateId));
            using ExternalRequestHandler handler = CreateHandler(apiConnection, ManualSettings());

            bool result = await handler.StartProvisioning(kTicketId, null, FinishedRequestReleasingTheFirstRequestTask());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CreatedRequests, Is.Empty);
            });
        }

        [Test]
        public async Task ProvisionImplTask_AutomaticTarget_CreatesARequestNothingFollows()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, new GlobalDeviceSpecificSettings());

            bool result = await handler.ProvisionImplTask(kTicketId, kImplTaskId);

            Assert.That(result, Is.True);
            Assert.That(apiConnection.CreatedRequests, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(Variable(apiConnection.CreatedRequests[0], "implTaskId"), Is.EqualTo(kImplTaskId));
                Assert.That(Variable(apiConnection.CreatedRequests[0], "extQueryVariables"), Is.EqualTo("{\"ManagementId\":[1],\"SingleImplTask\":[1]}"));
            });
        }

        [Test]
        public async Task ProvisionImplTask_WritesItBeforeOpenTasksOfLowerNumbers()
        {
            ProvisioningApiConn apiConnection = new(TicketWithTwoManualRequestTasks());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, new GlobalDeviceSpecificSettings());

            await handler.ProvisionImplTask(kTicketId, kOtherReqTaskImplTaskId);

            Assert.That(apiConnection.CreatedRequests, Has.Count.EqualTo(1));
            Assert.That(Variable(apiConnection.CreatedRequests[0], "implTaskId"), Is.EqualTo(kOtherReqTaskImplTaskId));
        }

        [Test]
        public async Task ProvisionImplTask_ManualTarget_IsWrittenByButtonOnly()
        {
            ProvisioningApiConn withoutButton = new(Ticket());
            ProvisioningApiConn byButton = new(Ticket());
            using ExternalRequestHandler handlerWithoutButton = CreateHandler(withoutButton, ManualSettings());
            using ExternalRequestHandler handlerByButton = CreateHandler(byButton, ManualSettings());

            await handlerWithoutButton.ProvisionImplTask(kTicketId, kImplTaskId);
            await handlerByButton.ProvisionImplTask(kTicketId, kImplTaskId, true);

            Assert.Multiple(() =>
            {
                Assert.That(withoutButton.CreatedRequests, Is.Empty);
                Assert.That(byButton.CreatedRequests, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task ProvisionImplTask_ExternalManagement_IsNotWritten()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, ExternalTicketSystemSettings());

            bool result = await handler.ProvisionImplTask(kTicketId, kImplTaskId, true);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CreatedRequests, Is.Empty);
            });
        }

        [Test]
        public async Task ProvisionImplTask_FinishedTask_IsNotWrittenAgain()
        {
            ProvisioningApiConn apiConnection = new(Ticket(kDoneStateId));
            using ExternalRequestHandler handler = CreateHandler(apiConnection, new GlobalDeviceSpecificSettings());

            bool result = await handler.ProvisionImplTask(kTicketId, kImplTaskId, true);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CreatedRequests, Is.Empty);
            });
        }

        [Test]
        public async Task ProvisionImplTask_RequestOfTheTicketStillRunning_CreatesNothingAndReportsIt()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            apiConnection.UnfinishedRequests.Add(new ExternalRequest { Id = 3, TicketId = kTicketId });
            using ExternalRequestHandler handler = CreateHandler(apiConnection, new GlobalDeviceSpecificSettings());

            bool result = await handler.ProvisionImplTask(kTicketId, kImplTaskId, true);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False, "the person who started it learns that nothing was written");
                Assert.That(apiConnection.CreatedRequests, Is.Empty);
                Assert.That(apiConnection.AlertDescriptions.Single(), Does.Contain($"implementation task {kImplTaskId}: not written, another external request"));
            });
        }

        [Test]
        public async Task ProvisionImplTask_UnknownImplementationTask_Fails()
        {
            ProvisioningApiConn apiConnection = new(Ticket());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, new GlobalDeviceSpecificSettings());

            bool result = await handler.ProvisionImplTask(kTicketId, 99);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(apiConnection.CreatedRequests, Is.Empty);
            });
        }

        [Test]
        public async Task StartProvisioning_AfterASingleImplementationTask_CreatesNothing()
        {
            ProvisioningApiConn apiConnection = new(TicketWithTwoManualRequestTasks());
            using ExternalRequestHandler handler = CreateHandler(apiConnection, new GlobalDeviceSpecificSettings());
            ExternalRequest previousRequest = new()
            {
                Id = 3,
                TicketId = kTicketId,
                ExtRequestState = ExtStates.ExtReqDone.ToString(),
                ExtQueryVariables = "{\"ManagementId\":[1],\"SingleImplTask\":[1]}"
            };

            bool result = await handler.StartProvisioning(kTicketId, null, previousRequest);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CreatedRequests, Is.Empty);
            });
        }
    }
}
