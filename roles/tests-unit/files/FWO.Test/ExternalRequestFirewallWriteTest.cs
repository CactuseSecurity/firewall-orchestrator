using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Middleware.Server;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the external requests writing an implementation task to the firewall: the sender writes the task with the
    /// write library and sets the request state from the result, the handler takes the result over into the
    /// implementation task and its history, and continues with the next entry of the provisioning work list.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class ExternalRequestFirewallWriteTest
    {
        private const long kTicketId = 50;
        private const long kRequestId = 3;
        private const long kFirstReqTaskId = 101;
        private const long kSecondReqTaskId = 102;
        private const long kImplTaskId = 10;
        private const long kNextImplTaskId = 11;
        private const long kSecondReqTaskImplTaskId = 20;
        private const int kManagementId = 1;
        private const int kOpenStateId = 210;
        private const int kDoneStateId = 631;
        private const int kRejectedStateId = 700;
        private const int kMaxAttempts = 3;
        private const int kCyclesBetweenAttempts = 5;

        private static readonly List<WfExtState> kExtStates =
        [
            new() { Id = 1, Name = ExtStates.ExtReqInitialized.ToString(), StateId = 1 },
            new() { Id = 2, Name = ExtStates.ExtReqRequested.ToString(), StateId = 3 },
            new() { Id = 3, Name = ExtStates.ExtReqDone.ToString(), StateId = kDoneStateId },
            new() { Id = 4, Name = ExtStates.ExtReqRejected.ToString(), StateId = kRejectedStateId }
        ];
        private static readonly List<long> kFirstImplTaskOnly = [kImplTaskId];
        private static readonly List<string?> kDoneOnly = [ExtStates.ExtReqDone.ToString()];
        private static readonly List<string?> kRejectedOnly = [ExtStates.ExtReqRejected.ToString()];
        private static readonly List<string?> kDiscardedOnly = [ExtStates.ExtReqDiscarded.ToString()];

        /// <summary>
        /// Returns the ticket of the test and the open requests, keeps the task states written in the ticket, and
        /// records all calls.
        /// </summary>
        private sealed class FirewallWriteApiConn(WfTicket ticket) : ExtTicketHandlerTestApiConn
        {
            public List<ExternalRequest> OpenRequests { get; } = [];

            public List<(string Query, object? Variables)> Calls { get; } = [];

            /// <summary>Rules with the gateways they take effect on, returned for the query of the enforcing gateways.</summary>
            public List<Rule> EnforcingRules { get; } = [];

            /// <summary>Requests of the implementation task writing a shared rule, returned newest first.</summary>
            public List<ExternalRequest> WriterRequests { get; } = [];

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Calls.Add((query, variables));
                if (query == ExtRequestQueries.getAndLockOpenRequests)
                {
                    return (QueryResponseType)(object)new ExternalRequestDataHelper { ExternalRequests = OpenRequests };
                }
                if (query == ExtRequestQueries.getRequestsOfImplTask)
                {
                    return (QueryResponseType)(object)WriterRequests;
                }
                if (query == RuleQueries.getRuleEnforcingGateways)
                {
                    return (QueryResponseType)(object)EnforcingRules;
                }
                if (query == ExtRequestQueries.getUnfinishedRequestsOfTicket)
                {
                    return (QueryResponseType)(object)new List<ExternalRequest>();
                }
                if (typeof(QueryResponseType) == typeof(WfTicket))
                {
                    return (QueryResponseType)(object)ticket;
                }
                if (typeof(QueryResponseType) == typeof(List<WfExtState>))
                {
                    return (QueryResponseType)(object)kExtStates;
                }
                if (typeof(QueryResponseType) == typeof(ConfigItem[]))
                {
                    return (QueryResponseType)(object)SimulatedUserConfig.GetAsConfigs();
                }
                if (typeof(QueryResponseType) == typeof(List<UiText>))
                {
                    return (QueryResponseType)(object)new List<UiText>();
                }
                if (query == RequestQueries.updateImplementationTaskState || query == RequestQueries.updateRequestTaskState)
                {
                    return (QueryResponseType)(object)new ReturnId { UpdatedIdLong = ApplyState(query, variables) };
                }
                return await base.SendQueryAsync<QueryResponseType>(query, variables, operationName, chunkingOptions);
            }

            public List<object> VariablesOf(string query)
            {
                return [.. Calls.Where(call => call.Query == query && call.Variables != null).Select(call => call.Variables!)];
            }

            private long ApplyState(string query, object? variables)
            {
                long id = Convert.ToInt64(Variable(variables, "id") ?? 0L);
                int stateId = Convert.ToInt32(Variable(variables, "state") ?? 0);
                WfStatefulObject? task = query == RequestQueries.updateImplementationTaskState
                    ? ticket.Tasks.SelectMany(reqTask => reqTask.ImplementationTasks).FirstOrDefault(implTask => implTask.Id == id)
                    : ticket.Tasks.FirstOrDefault(reqTask => reqTask.Id == id);
                if (task != null)
                {
                    task.StateId = stateId;
                }
                return id;
            }
        }

        private static WfTicket Ticket(int firstImplTaskStateId = kOpenStateId)
        {
            WfReqTask first = ReqTask(kFirstReqTaskId, 1);
            AddImplTask(first, kImplTaskId, 1, firstImplTaskStateId);
            AddImplTask(first, kNextImplTaskId, 2, kOpenStateId);
            WfReqTask second = ReqTask(kSecondReqTaskId, 2);
            AddImplTask(second, kSecondReqTaskImplTaskId, 1, kOpenStateId);
            return new WfTicket { Id = kTicketId, Title = "Ticket", StateId = kOpenStateId, Tasks = [first, second] };
        }

        private static WfReqTask ReqTask(long id, int taskNumber)
        {
            return new WfReqTask
            {
                Id = id,
                Title = $"Group {taskNumber}",
                TicketId = kTicketId,
                TaskNumber = taskNumber,
                TaskType = WfTaskType.group_create.ToString(),
                ManagementId = kManagementId,
                StateId = kOpenStateId,
                AdditionalInfo = "{\"ConnId\":\"1\",\"GrpName\":\"ARxx12345-100\"}"
            };
        }

        private static void AddImplTask(WfReqTask reqTask, long id, int taskNumber, int stateId)
        {
            reqTask.ImplementationTasks.Add(new WfImplTask
            {
                Id = id,
                Title = $"Implementation {id}",
                ReqTaskId = reqTask.Id,
                TicketId = kTicketId,
                TaskNumber = taskNumber,
                TaskType = reqTask.TaskType,
                ManagementId = kManagementId,
                StateId = stateId
            });
        }

        private const string kSingleImplTaskVariables = "{\"ManagementId\":[1],\"SingleImplTask\":[1]}";

        private static ExternalRequest Request(long? implTaskId = kImplTaskId, string? state = null, int attempts = 0, int waitCycles = 0,
            string extQueryVariables = "{\"ManagementId\":[1]}")
        {
            return new ExternalRequest
            {
                Id = kRequestId,
                TicketId = kTicketId,
                TaskNumber = 1,
                ImplTaskId = implTaskId,
                ExtRequestType = WfTaskType.group_create.ToString(),
                ExtRequestState = state ?? ExtStates.ExtReqInitialized.ToString(),
                ExtQueryVariables = extQueryVariables,
                Attempts = attempts,
                WaitCycles = waitCycles,
                Locked = true
            };
        }

        private static ExternalRequestHandler CreateHandler(FirewallWriteApiConn apiConnection, GlobalDeviceSpecificSettings? settings = null)
        {
            return new ExternalRequestHandler(new SimulatedUserConfig(), apiConnection, null).WithProvisioningSettings(settings ?? new GlobalDeviceSpecificSettings());
        }

        private sealed class RecordingWriter(ModifyFirewallResult result)
        {
            public List<long> WrittenImplTaskIds { get; } = [];

            public List<IReadOnlyList<int>?> InstallOnGatewayIds { get; } = [];

            public Task<ModifyFirewallResult> Write(WfImplTask implTask, IReadOnlyList<int>? installOnGatewayIds)
            {
                WrittenImplTaskIds.Add(implTask.Id);
                InstallOnGatewayIds.Add(installOnGatewayIds);
                return Task.FromResult(result);
            }
        }

        private static async Task<RecordingWriter> RunSender(FirewallWriteApiConn apiConnection, ExternalRequest request, ModifyFirewallResult result,
            GlobalDeviceSpecificSettings? settings = null)
        {
            apiConnection.OpenRequests.Add(request);
            RecordingWriter writer = new(result);
            using ExternalRequestSender sender = new(apiConnection, new SimulatedGlobalConfig(), null, writer.Write)
            {
                HandlerFactory = () => CreateHandler(apiConnection, settings)
            };
            List<string> failedRequests = await sender.Run();
            Assert.That(failedRequests, Is.Empty);
            return writer;
        }

        private static ModifyFirewallResult Success()
        {
            List<FirewallStepResult> steps = [new(FirewallWriteOperation.GroupCreate, "ARxx12345-100", true, 200, "ok", null)];
            return ModifyFirewallResult.Succeeded("written", steps);
        }

        private static ModifyFirewallResult Failure(FirewallWriteErrorKind kind)
        {
            return ModifyFirewallResult.Failed("write failed", new FirewallWriteError(kind, "native message"));
        }

        private static object? Variable(object? variables, string name)
        {
            return variables?.GetType().GetProperty(name)?.GetValue(variables);
        }

        private static List<string?> States(FirewallWriteApiConn apiConnection, string query)
        {
            return [.. apiConnection.VariablesOf(query).Select(variables => Variable(variables, "extRequestState")?.ToString())];
        }

        private static WfImplTask ImplTask(WfTicket ticket, long implTaskId)
        {
            return ticket.Tasks.SelectMany(reqTask => reqTask.ImplementationTasks).First(implTask => implTask.Id == implTaskId);
        }

        [Test]
        public async Task Run_SuccessfulWrite_SetsTheImplementationTaskDoneAndContinuesWithTheNextOne()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);

            RecordingWriter writer = await RunSender(apiConnection, Request(), Success());

            List<object> createdRequests = apiConnection.VariablesOf(ExtRequestQueries.addExtRequest);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(writer.WrittenImplTaskIds, Is.EqualTo(kFirstImplTaskOnly));
                Assert.That(States(apiConnection, ExtRequestQueries.updateExtRequestCreation), Is.EqualTo(kDoneOnly));
                Assert.That(ImplTask(ticket, kImplTaskId).StateId, Is.EqualTo(kDoneStateId));
                Assert.That(States(apiConnection, ExtRequestQueries.updateExtRequestFinal), Does.Contain(ExtStates.ExtReqAcknowledged.ToString()));
                Assert.That(apiConnection.History, Has.Some.Contains("Implemented Implementation 10 on"));
                Assert.That(createdRequests, Has.Count.EqualTo(1));
                Assert.That(Variable(createdRequests[0], "implTaskId"), Is.EqualTo(kNextImplTaskId));
                Assert.That(Variable(createdRequests[0], "waitCycles"), Is.EqualTo(0));
                Assert.That(Variable(createdRequests[0], "ownerId"), Is.Null, "a ticket without owner creates its requests without owner");
            }
        }

        [Test]
        public async Task Run_RuleDelete_NamesTheGatewaysTheRuleTakesEffectOn()
        {
            WfTicket ticket = Ticket();
            WfImplTask implTask = ImplTask(ticket, kImplTaskId);
            implTask.TaskType = WfTaskType.rule_delete.ToString();
            implTask.ImplElements.Add(new WfImplElement { Field = ElemFieldType.rule.ToString(), RuleUid = "r1" });
            FirewallWriteApiConn apiConnection = new(ticket);
            DeviceWrapper gatewayB = new() { Content = new Device { Id = 2, Name = "gw-b" } };
            DeviceWrapper gatewayC = new() { Content = new Device { Id = 3, Name = "gw-c" } };
            DeviceWrapper[] gateways = [gatewayB, gatewayC];
            apiConnection.EnforcingRules.Add(new Rule { Uid = "r1", EnforcingGateways = gateways });

            await RunSender(apiConnection, Request(), Success());

            object creation = apiConnection.VariablesOf(ExtRequestQueries.updateExtRequestCreation).First();
            Assert.That(Variable(creation, "creationResponse")?.ToString(), Does.EndWith("The rule takes effect on the gateways: gw-b, gw-c."));
        }

        [Test]
        public async Task Run_ObjectTask_NamesNoGateways()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);

            await RunSender(apiConnection, Request(), Success());

            object creation = apiConnection.VariablesOf(ExtRequestQueries.updateExtRequestCreation).First();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Variable(creation, "creationResponse")?.ToString(), Does.Not.Contain("takes effect on"));
                Assert.That(apiConnection.Calls.Any(call => call.Query == RuleQueries.getRuleEnforcingGateways), Is.False);
            }
        }

        private const string kCoveredByFirstTask = "{\"ManagementId\":[1],\"CoveredBy\":[1]}";
        private static readonly List<int> kSharedRuleGateways = [200, 300];

        private static ExternalRequest WriterRequest(string state, string coveredTasks) => new()
        {
            Id = 1,
            TicketId = kTicketId,
            ImplTaskId = kImplTaskId,
            ExtRequestState = state,
            ExtQueryVariables = $"{{\"ManagementId\":[1],\"InstallOnGateways\":[200,300],\"CoveredTasks\":{coveredTasks}}}"
        };

        [Test]
        public async Task Run_TaskCoveredByAWrittenSharedRule_IsDoneWithoutWriting()
        {
            WfTicket ticket = Ticket(kDoneStateId);
            FirewallWriteApiConn apiConnection = new(ticket);
            apiConnection.WriterRequests.Add(WriterRequest(ExtStates.ExtReqAcknowledged.ToString(), "[2]"));

            RecordingWriter writer = await RunSender(apiConnection, Request(kNextImplTaskId, extQueryVariables: kCoveredByFirstTask), Success());

            object creation = apiConnection.VariablesOf(ExtRequestQueries.updateExtRequestCreation).First();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(writer.WrittenImplTaskIds, Is.Empty);
                Assert.That(Variable(creation, "extRequestState"), Is.EqualTo(ExtStates.ExtReqDone.ToString()));
                Assert.That(Variable(creation, "creationResponse"), Is.EqualTo("Covered by the rule of implementation task 1; nothing written."));
                Assert.That(ImplTask(ticket, kNextImplTaskId).StateId, Is.EqualTo(kDoneStateId));
            }
        }

        [Test]
        public async Task Run_TaskCoveredByARejectedSharedRule_IsRejectedWithoutWriting()
        {
            WfTicket ticket = Ticket(kRejectedStateId);
            FirewallWriteApiConn apiConnection = new(ticket);
            apiConnection.WriterRequests.Add(WriterRequest(ExtStates.ExtReqAckRejected.ToString(), "[2]"));

            RecordingWriter writer = await RunSender(apiConnection, Request(kNextImplTaskId, extQueryVariables: kCoveredByFirstTask), Success());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(writer.WrittenImplTaskIds, Is.Empty);
                Assert.That(States(apiConnection, ExtRequestQueries.updateExtRequestCreation), Is.EqualTo(kRejectedOnly));
                Assert.That(ImplTask(ticket, kNextImplTaskId).StateId, Is.EqualTo(kRejectedStateId));
            }
        }

        [Test]
        public async Task Run_TaskAddedAfterTheSharedRuleWasWritten_WritesItsOwnRule()
        {
            WfTicket ticket = Ticket(kDoneStateId);
            FirewallWriteApiConn apiConnection = new(ticket);
            apiConnection.WriterRequests.Add(WriterRequest(ExtStates.ExtReqAcknowledged.ToString(), "[3]"));

            RecordingWriter writer = await RunSender(apiConnection, Request(kNextImplTaskId, extQueryVariables: kCoveredByFirstTask), Success());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(writer.WrittenImplTaskIds.Single(), Is.EqualTo(kNextImplTaskId));
                Assert.That(writer.InstallOnGatewayIds.Single(), Is.Null);
            }
        }

        [Test]
        public async Task Run_TaskCoveredByAnUnwrittenSharedRule_IsDiscarded()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);

            RecordingWriter writer = await RunSender(apiConnection, Request(kNextImplTaskId, extQueryVariables: kCoveredByFirstTask), Success());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(writer.WrittenImplTaskIds, Is.Empty);
                Assert.That(ImplTask(ticket, kNextImplTaskId).StateId, Is.EqualTo(kOpenStateId));
                Assert.That(apiConnection.VariablesOf(ExtRequestQueries.updateExtRequestCreation), Is.Empty);
            }
        }

        [Test]
        public async Task Run_WriterOfASharedRule_InstallsItOnTheGatewaysOfAllItsTasks()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);
            const string writerVariables = "{\"ManagementId\":[1],\"InstallOnGateways\":[200,300],\"CoveredTasks\":[2]}";

            RecordingWriter writer = await RunSender(apiConnection, Request(extQueryVariables: writerVariables), Success());

            Assert.That(writer.InstallOnGatewayIds.Single(), Is.EqualTo(kSharedRuleGateways));
        }

        [Test]
        public async Task Run_RetryableError_IsRetriedAfterSomeCycles()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);

            await RunSender(apiConnection, Request(), Failure(FirewallWriteErrorKind.Unreachable));

            object creation = apiConnection.VariablesOf(ExtRequestQueries.updateExtRequestCreation).Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Variable(creation, "extRequestState"), Is.EqualTo(ExtStates.ExtReqFailed.ToString()));
                Assert.That(Variable(creation, "attempts"), Is.EqualTo(1));
                Assert.That(Variable(creation, "waitCycles"), Is.EqualTo(kCyclesBetweenAttempts));
                Assert.That(ImplTask(ticket, kImplTaskId).StateId, Is.EqualTo(kOpenStateId));
                Assert.That(apiConnection.VariablesOf(ExtRequestQueries.updateExtRequestFinal), Is.Empty);
            }
        }

        [Test]
        public async Task Run_RetryableErrorInTheLastAttempt_RejectsTheTaskAndTheFollowingRequestTasks()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);

            await RunSender(apiConnection, Request(attempts: kMaxAttempts - 1), Failure(FirewallWriteErrorKind.Unreachable));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(States(apiConnection, ExtRequestQueries.updateExtRequestCreation), Is.EqualTo(kRejectedOnly));
                Assert.That(ImplTask(ticket, kImplTaskId).StateId, Is.EqualTo(kRejectedStateId));
                Assert.That(ticket.Tasks.Single(task => task.Id == kSecondReqTaskId).StateId, Is.EqualTo(kRejectedStateId));
                Assert.That(States(apiConnection, ExtRequestQueries.updateExtRequestFinal), Does.Contain(ExtStates.ExtReqAckRejected.ToString()));
                Assert.That(apiConnection.History, Has.Some.Contains("Rejected Implementation 10 on"));
                Assert.That(apiConnection.VariablesOf(ExtRequestQueries.addExtRequest), Is.Empty);
            }
        }

        [Test]
        public async Task Run_RetryableError_UsesTheRetriesOfTheManagement()
        {
            GlobalDeviceSpecificSettings settings = new() { RequestMaxAttempts = 3, RequestCyclesBetweenAttempts = 7 };
            FirewallWriteApiConn retried = new(Ticket());
            FirewallWriteApiConn rejected = new(Ticket());

            await RunSender(retried, Request(attempts: 1), Failure(FirewallWriteErrorKind.Unreachable), settings);
            await RunSender(rejected, Request(attempts: 2), Failure(FirewallWriteErrorKind.Unreachable), settings);

            object retry = retried.VariablesOf(ExtRequestQueries.updateExtRequestCreation).Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Variable(retry, "extRequestState"), Is.EqualTo(ExtStates.ExtReqFailed.ToString()));
                Assert.That(Variable(retry, "waitCycles"), Is.EqualTo(2 * 7));
                Assert.That(States(rejected, ExtRequestQueries.updateExtRequestCreation), Is.EqualTo(kRejectedOnly));
            }
        }

        [Test]
        public async Task Run_ErrorThatDoesNotPass_RejectsAtOnce()
        {
            FirewallWriteApiConn apiConnection = new(Ticket());

            await RunSender(apiConnection, Request(), Failure(FirewallWriteErrorKind.Rejected));

            object creation = apiConnection.VariablesOf(ExtRequestQueries.updateExtRequestCreation).Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Variable(creation, "extRequestState"), Is.EqualTo(ExtStates.ExtReqRejected.ToString()));
                Assert.That(Variable(creation, "creationResponse")?.ToString(), Does.StartWith("write failed"));
                Assert.That(Variable(creation, "creationResponse")?.ToString(), Does.EndWith("Firewall: native message"),
                    "the reason given by the firewall is shown in the request");
            }
        }

        [Test]
        public async Task Run_SuccessfulWrite_ShowsNoFirewallMessage()
        {
            FirewallWriteApiConn apiConnection = new(Ticket());

            await RunSender(apiConnection, Request(), Success());

            object creation = apiConnection.VariablesOf(ExtRequestQueries.updateExtRequestCreation).Single();
            Assert.That(Variable(creation, "creationResponse")?.ToString(), Does.Not.Contain("Firewall:"));
        }

        [Test]
        public async Task Run_CallsThatCountAsDone_SayWhy()
        {
            List<FirewallStepResult> steps =
            [
                new(FirewallWriteOperation.HostCreate, "h1", true, 400, $"{FirewallWriteClient.kAlreadyPresentNote} duplicate"),
                new(FirewallWriteOperation.RuleDelete, "r1", true, 404, $"{FirewallWriteClient.kAlreadyDeletedNote} not found"),
                new(FirewallWriteOperation.Publish, "mgmt", true, 200, "{}")
            ];
            FirewallWriteApiConn apiConnection = new(Ticket());

            await RunSender(apiConnection, Request(), ModifyFirewallResult.Succeeded("written", steps));

            object creation = apiConnection.VariablesOf(ExtRequestQueries.updateExtRequestCreation).Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Variable(creation, "extRequestState"), Is.EqualTo(ExtStates.ExtReqDone.ToString()));
                Assert.That(Variable(creation, "creationResponse"),
                    Is.EqualTo("written (HostCreate h1: 400 already present as requested; RuleDelete r1: 404 already deleted; Publish mgmt: 200)"));
            }
        }

        [Test]
        public async Task Run_ImplementationTaskAlreadyFinished_IsNotWrittenAgain()
        {
            WfTicket ticket = Ticket(kDoneStateId);
            FirewallWriteApiConn apiConnection = new(ticket);

            RecordingWriter writer = await RunSender(apiConnection, Request(), Success());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(writer.WrittenImplTaskIds, Is.Empty);
                Assert.That(States(apiConnection, ExtRequestQueries.updateExtRequestCreation), Is.EqualTo(kDoneOnly));
                Assert.That(apiConnection.VariablesOf(RequestQueries.updateImplementationTaskState), Is.Empty);
                Assert.That(apiConnection.History, Is.Empty);
                Assert.That(Variable(apiConnection.VariablesOf(ExtRequestQueries.addExtRequest).Single(), "implTaskId"), Is.EqualTo(kNextImplTaskId));
            }
        }

        [Test]
        public async Task Run_WaitingRequest_CountsDownWithoutWriting()
        {
            FirewallWriteApiConn apiConnection = new(Ticket());

            RecordingWriter writer = await RunSender(apiConnection, Request(waitCycles: 2), Success());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(writer.WrittenImplTaskIds, Is.Empty);
                Assert.That(Variable(apiConnection.VariablesOf(ExtRequestQueries.updateExternalRequestWaitCycles).Single(), "waitCycles"), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task Run_RequestWhoseImplementationTaskWasDeleted_IsDiscarded()
        {
            FirewallWriteApiConn apiConnection = new(Ticket(kDoneStateId));

            RecordingWriter writer = await RunSender(apiConnection, Request(implTaskId: null), Success());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(writer.WrittenImplTaskIds, Is.Empty);
                Assert.That(States(apiConnection, ExtRequestQueries.updateExtRequestFinal), Is.EqualTo(kDiscardedOnly));
                Assert.That(Variable(apiConnection.VariablesOf(ExtRequestQueries.addExtRequest).Single(), "implTaskId"), Is.EqualTo(kNextImplTaskId));
            }
        }

        [Test]
        public async Task Run_RequestInAStateWithoutWrite_IsLeftAsItIs()
        {
            FirewallWriteApiConn apiConnection = new(Ticket());

            RecordingWriter writer = await RunSender(apiConnection, Request(state: ExtStates.ExtReqRequested.ToString()), Success());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(writer.WrittenImplTaskIds, Is.Empty);
                Assert.That(apiConnection.VariablesOf(ExtRequestQueries.updateExtRequestCreation), Is.Empty);
            }
        }

        [Test]
        public async Task HandleStateChange_PatchedToDone_TakesTheStateOverIntoTheImplementationTask()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);
            using ExternalRequestHandler handler = CreateHandler(apiConnection);

            bool result = await handler.PatchState(Request(state: ExtStates.ExtReqDone.ToString()));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(ImplTask(ticket, kImplTaskId).StateId, Is.EqualTo(kDoneStateId));
                Assert.That(ticket.Tasks.Single(task => task.Id == kFirstReqTaskId).StateId, Is.Not.EqualTo(kDoneStateId));
                Assert.That(Variable(apiConnection.VariablesOf(ExtRequestQueries.addExtRequest).Single(), "implTaskId"), Is.EqualTo(kNextImplTaskId));
            }
        }

        [Test]
        public async Task HandleStateChange_SingleImplementationTaskDone_NothingFollows()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);
            using ExternalRequestHandler handler = CreateHandler(apiConnection);

            await handler.PatchState(Request(state: ExtStates.ExtReqDone.ToString(), extQueryVariables: kSingleImplTaskVariables));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ImplTask(ticket, kImplTaskId).StateId, Is.EqualTo(kDoneStateId));
                Assert.That(States(apiConnection, ExtRequestQueries.updateExtRequestFinal), Does.Contain(ExtStates.ExtReqAcknowledged.ToString()));
                Assert.That(apiConnection.VariablesOf(ExtRequestQueries.addExtRequest), Is.Empty);
            }
        }

        [Test]
        public async Task HandleStateChange_SingleImplementationTaskRejected_LeavesTheFollowingRequestTasks()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);
            using ExternalRequestHandler handler = CreateHandler(apiConnection);

            await handler.PatchState(Request(state: ExtStates.ExtReqRejected.ToString(), extQueryVariables: kSingleImplTaskVariables));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ImplTask(ticket, kImplTaskId).StateId, Is.EqualTo(kRejectedStateId));
                Assert.That(ImplTask(ticket, kNextImplTaskId).StateId, Is.EqualTo(kOpenStateId), "the other tasks are left to the person who started the write");
                Assert.That(ticket.Tasks.Single(task => task.Id == kSecondReqTaskId).StateId, Is.EqualTo(kOpenStateId));
                Assert.That(apiConnection.VariablesOf(ExtRequestQueries.addExtRequest), Is.Empty);
            }
        }

        [Test]
        public async Task HandleStateChange_Rejected_RejectsTheOpenImplementationTasksOfTheRequestTaskWithReason()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);
            using ExternalRequestHandler handler = CreateHandler(apiConnection);

            await handler.PatchState(Request(state: ExtStates.ExtReqRejected.ToString()));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ImplTask(ticket, kImplTaskId).StateId, Is.EqualTo(kRejectedStateId));
                Assert.That(ImplTask(ticket, kNextImplTaskId).StateId, Is.EqualTo(kRejectedStateId));
                Assert.That(ImplTask(ticket, kNextImplTaskId).Stop, Is.Not.Null);
                Assert.That(apiConnection.History, Has.Some.Contains("Rejected Implementation 11 on")
                    .And.Contains("not written, implementation task 1 of the same request task was rejected"));
                Assert.That(apiConnection.VariablesOf(ExtRequestQueries.addExtRequest), Is.Empty);
            }
        }

        [Test]
        public async Task HandleStateChange_RejectedSharedRule_RejectsTheCoveredTasksWithReason()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);
            using ExternalRequestHandler handler = CreateHandler(apiConnection);

            await handler.PatchState(Request(state: ExtStates.ExtReqRejected.ToString(),
                extQueryVariables: "{\"ManagementId\":[1],\"InstallOnGateways\":[200,300],\"CoveredTasks\":[2]}"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ImplTask(ticket, kNextImplTaskId).StateId, Is.EqualTo(kRejectedStateId));
                Assert.That(apiConnection.History, Has.Some.Contains("Rejected Implementation 11 on")
                    .And.Contains("covered by the rule of implementation task 1, which was rejected"));
            }
        }

        [Test]
        public async Task HandleStateChange_Rejected_NamesTheRejectedRequestTaskInTheFollowingOnes()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);
            using ExternalRequestHandler handler = CreateHandler(apiConnection);

            await handler.PatchState(Request(state: ExtStates.ExtReqRejected.ToString()));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ticket.Tasks.Single(task => task.Id == kSecondReqTaskId).StateId, Is.EqualTo(kRejectedStateId));
                Assert.That(apiConnection.History, Has.Some.Contains("Rejected Group 2 on").And.Contains("not processed, request task 1 was rejected"));
                Assert.That(ImplTask(ticket, kSecondReqTaskImplTaskId).StateId, Is.EqualTo(kRejectedStateId),
                    "the implementation tasks of a following request task take its state over");
            }
        }

        [Test]
        public async Task HandleStateChange_FinishedImplementationTask_KeepsItsStateWhenAnotherIsRejected()
        {
            WfTicket ticket = Ticket();
            ImplTask(ticket, kNextImplTaskId).StateId = kDoneStateId;
            FirewallWriteApiConn apiConnection = new(ticket);
            using ExternalRequestHandler handler = CreateHandler(apiConnection);

            await handler.PatchState(Request(state: ExtStates.ExtReqRejected.ToString()));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ImplTask(ticket, kNextImplTaskId).StateId, Is.EqualTo(kDoneStateId));
                Assert.That(apiConnection.History, Has.None.Contains("Rejected Implementation 11 on"));
            }
        }

        [Test]
        public async Task HandleStateChange_UnfinishedState_ChangesNothing()
        {
            WfTicket ticket = Ticket();
            FirewallWriteApiConn apiConnection = new(ticket);
            using ExternalRequestHandler handler = CreateHandler(apiConnection);

            await handler.HandleStateChange(Request(state: ExtStates.ExtReqFailed.ToString()));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ImplTask(ticket, kImplTaskId).StateId, Is.EqualTo(kOpenStateId));
                Assert.That(apiConnection.VariablesOf(ExtRequestQueries.updateExtRequestFinal), Is.Empty);
                Assert.That(apiConnection.VariablesOf(ExtRequestQueries.addExtRequest), Is.Empty);
            }
        }

        [Test]
        public async Task HandleStateChange_UnknownImplementationTask_IsAcknowledgedAndContinues()
        {
            FirewallWriteApiConn apiConnection = new(Ticket());
            using ExternalRequestHandler handler = CreateHandler(apiConnection);

            await handler.HandleStateChange(Request(implTaskId: 99, state: ExtStates.ExtReqDone.ToString()));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(States(apiConnection, ExtRequestQueries.updateExtRequestFinal), Does.Contain(ExtStates.ExtReqAcknowledged.ToString()));
                Assert.That(Variable(apiConnection.VariablesOf(ExtRequestQueries.addExtRequest).Single(), "implTaskId"), Is.EqualTo(kImplTaskId));
            }
        }
    }
}
