using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Test.Mocks;
using NUnit.Framework;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerStateChangesTest
    {
        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            FieldInfo? field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            StateMatrixDict dict = (StateMatrixDict)(field?.GetValue(handler) ?? new StateMatrixDict());
            dict.Matrices[taskType] = matrix;
        }

        /// <summary>
        /// Wires the db access the way production does: on the handler's own action handler, not on a
        /// second instance. State actions delegated from the db access therefore reach the same action
        /// handler the promote later flushes the email bundle through.
        /// </summary>
        private static void SetDbAccess(WfHandler handler, ApiConnection apiConnection)
        {
            ActionHandler actionHandler = handler.ActionHandler ?? new ActionHandler(apiConnection, handler);
            actionHandler.Init([]).GetAwaiter().GetResult();
            handler.ActionHandler = actionHandler;
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, handler.userConfig, apiConnection, actionHandler, true);
            typeof(WfHandler).GetField("dbAcc", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(handler, dbAccess);
        }

        private static async Task<string> CaptureConsoleOutput(Func<Task> action)
        {
            using StringWriter logOutput = new();
            TextWriter originalConsoleOut = Console.Out;
            try
            {
                Console.SetOut(logOutput);
                await action();
                return logOutput.ToString();
            }
            finally
            {
                Console.SetOut(originalConsoleOut);
            }
        }

        [Test]
        public async Task PromoteTicket_SetsStateAndCompletionAndResetsFlags()
        {
            WfHandler handler = new();
            handler.MasterStateMatrix = new StateMatrix
            {
                MinTicketCompleted = 2,
                LowestEndState = 10,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };
            handler.ActTicket = new WfTicket { Id = 1, StateId = 0 };
            handler.TicketList.Add(handler.ActTicket);
            handler.DisplayTicketMode = true;
            handler.EditTicketMode = true;
            handler.AddTicketMode = true;
            handler.DisplayPromoteTicketMode = true;
            handler.DisplaySaveTicketMode = true;

            bool ok = await handler.PromoteTicket(new WfStatefulObject { StateId = 2 });

            Assert.That(ok, Is.True);
            Assert.That(handler.ActTicket.StateId, Is.EqualTo(2));
            Assert.That(handler.ActTicket.CompletionDate, Is.Not.Null);
            Assert.That(handler.DisplayTicketMode, Is.False);
            Assert.That(handler.EditTicketMode, Is.False);
            Assert.That(handler.AddTicketMode, Is.False);
            Assert.That(handler.DisplayPromoteTicketMode, Is.False);
            Assert.That(handler.DisplaySaveTicketMode, Is.False);
        }

        [Test]
        public async Task PromoteTicketAndTasks_ReportsEmailFailureButStillSucceeds()
        {
            List<string> displayedMessages = [];
            MonitoringStateChangeApiConn apiConn = new();
            using TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.InternalServerError, "\"flush failed\""));
            WfHandler handler = BuildBundleFlushHandler(displayedMessages, apiConn, middlewareClient);
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { MinTicketCompleted = 99 });
            handler.ActTicket = new WfTicket
            {
                Id = 42,
                StateId = 0,
                Tasks = [new WfReqTask { Id = 7, TicketId = 42, StateId = 0, TaskType = WfTaskType.access.ToString() }]
            };
            handler.TicketList.Add(handler.ActTicket);
            SetDbAccess(handler, apiConn);

            bool ok = await handler.PromoteTicketAndTasks(new WfStatefulObject { StateId = 2 });

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True, "a failed email flush must not turn a completed state change into a failure");
                Assert.That(displayedMessages, Has.Some.Contains("bundled emails could not be sent"));
                Assert.That(handler.WorkflowEmailBundleId, Is.Null, "the bundle must be cleared afterwards");
            });
        }

        [Test]
        public async Task PromoteTicketAndTasks_SkipsFlushAndStaysSilentWhenNothingWasBundled()
        {
            List<string> displayedMessages = [];
            MonitoringStateChangeApiConn apiConn = new();
            using TestMiddlewareClient middlewareClient = new();
            RecordingFlushHandler flushHandler = new();
            middlewareClient.UseHandler(flushHandler);
            WfHandler handler = BuildBundleFlushHandler(displayedMessages, apiConn, middlewareClient);
            // A ticket without request tasks delegates nothing under the bundle, so no collector can
            // exist middleware side and the flush round trip would only risk a bogus delivery error.
            handler.ActTicket = new WfTicket { Id = 44, StateId = 0, Tasks = [] };
            handler.TicketList.Add(handler.ActTicket);
            SetDbAccess(handler, apiConn);

            bool ok = await handler.PromoteTicketAndTasks(new WfStatefulObject { StateId = 2 });

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(flushHandler.FlushOnlyRequestCount, Is.Zero, "an empty bundle must not cost a round trip");
                Assert.That(displayedMessages, Is.Empty, "nothing failed, so the user must not be told emails were lost");
                Assert.That(handler.WorkflowEmailBundleId, Is.Null);
            });
        }

        [Test]
        public async Task PromoteTicketAndTasks_ReportsFailedFlushOnlyOnce()
        {
            List<string> displayedMessages = [];
            MonitoringStateChangeApiConn apiConn = new();
            using TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.InternalServerError, "\"flush failed\""));
            WfHandler handler = BuildBundleFlushHandler(displayedMessages, apiConn, middlewareClient);
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { MinTicketCompleted = 99 });
            handler.ActTicket = new WfTicket
            {
                Id = 43,
                StateId = 0,
                Tasks = [new WfReqTask { Id = 9, TicketId = 43, StateId = 0, TaskType = WfTaskType.access.ToString() }]
            };
            handler.TicketList.Add(handler.ActTicket);
            SetDbAccess(handler, apiConn);

            await handler.PromoteTicketAndTasks(new WfStatefulObject { StateId = 2 });

            Assert.That(displayedMessages.Count(message => message.Contains("bundled emails could not be sent")),
                Is.EqualTo(1), "one delivery failure must not be reported twice");
            // Ids differ per test on purpose: middleware delegation is deduplicated by a static key over
            // scope, object, ticket, state and phase, so shared ids would silently drop the delegation.
        }

        [Test]
        public async Task PromoteTicketAndTasks_FlushesCapturedEmailsWhenTaskLoopThrows()
        {
            List<string> displayedMessages = [];
            MonitoringStateChangeApiConn apiConn = new();
            using TestMiddlewareClient middlewareClient = new();
            RecordingFlushHandler flushHandler = new();
            middlewareClient.UseHandler(flushHandler);
            WfHandler handler = BuildBundleFlushHandler(displayedMessages, apiConn, middlewareClient);
            // The first task has a matrix and delegates under the bundle, the second has none so the
            // request task loop throws afterwards - an abort after something was already captured.
            SetMatrix(handler, WfTaskType.access.ToString(), new StateMatrix { MinTicketCompleted = 99 });
            handler.ActTicket = new WfTicket
            {
                Id = 45,
                StateId = 0,
                Tasks =
                [
                    new WfReqTask { Id = 10, TicketId = 45, StateId = 0, TaskType = WfTaskType.access.ToString() },
                    new WfReqTask { Id = 11, TicketId = 45, StateId = 0, TaskType = WfTaskType.master.ToString() }
                ]
            };
            handler.TicketList.Add(handler.ActTicket);
            SetDbAccess(handler, apiConn);

            bool ok = await handler.PromoteTicketAndTasks(new WfStatefulObject { StateId = 2 });

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False, "the promote itself failed");
                Assert.That(flushHandler.FlushOnlyRequestCount, Is.EqualTo(1),
                    "captured emails were suppressed at their action, so an aborted promote must still flush them");
                Assert.That(handler.WorkflowEmailBundleId, Is.Null);
            });
        }

        private static WfHandler BuildBundleFlushHandler(List<string> displayedMessages, ApiConnection apiConnection,
            TestMiddlewareClient middlewareClient)
        {
            WfHandler handler = new((_, _, message, _) => displayedMessages.Add(message),
                new SimulatedUserConfig(), new ClaimsPrincipal(), apiConnection, middlewareClient, WorkflowPhases.request)
            {
                MasterStateMatrix = new StateMatrix
                {
                    MinTicketCompleted = 99,
                    LowestEndState = 10,
                    PhaseActive = new() { { WorkflowPhases.planning, false } }
                }
            };
            handler.ActionHandler = new ActionHandler(apiConnection, handler);
            handler.ActionHandler.Init([]).GetAwaiter().GetResult();
            return handler;
        }

        private sealed class RecordingFlushHandler : HttpMessageHandler
        {
            public int FlushOnlyRequestCount { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : "";
                if (body.Contains("\"EmailBundleFlushOnly\":true", StringComparison.OrdinalIgnoreCase)
                    || body.Contains("\"emailBundleFlushOnly\":true", StringComparison.OrdinalIgnoreCase))
                {
                    ++FlushOnlyRequestCount;
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"success\":true,\"messages\":[],\"errorMessage\":\"\"}", Encoding.UTF8, "application/json")
                };
            }
        }

        [Test]
        public async Task PromoteReqTask_SetsStartAndHandlerAndClearsFlag()
        {
            WfHandler handler = new();
            handler.MasterStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };
            handler.ActStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            handler.ActReqTask = new WfReqTask { Id = 7, TicketId = 10, StateId = 0 };
            handler.ActTicket = new WfTicket { Id = 10, Tasks = { handler.ActReqTask } };
            handler.DisplayPromoteReqTaskMode = true;

            await handler.PromoteReqTask(new WfStatefulObject { StateId = 2 });

            Assert.That(handler.ActReqTask.StateId, Is.EqualTo(2));
            Assert.That(handler.ActReqTask.Start, Is.Not.Null);
            Assert.That(handler.ActReqTask.CurrentHandler, Is.EqualTo(handler.userConfig.User));
            Assert.That(handler.DisplayPromoteReqTaskMode, Is.False);
        }

        [Test]
        public async Task PromoteReqTask_WithStartedHandlerDisabled_DoesNotSetStartOrHandler()
        {
            WfHandler handler = new();
            handler.MasterStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };
            handler.ActStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            handler.ActReqTask = new WfReqTask { Id = 7, TicketId = 10, StateId = 0 };
            handler.ActTicket = new WfTicket { Id = 10, Tasks = { handler.ActReqTask } };
            handler.DisplayPromoteReqTaskMode = true;

            await handler.PromoteReqTask(new WfStatefulObject { StateId = 2 }, setStartedHandler: false);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.StateId, Is.EqualTo(2));
                Assert.That(handler.ActReqTask.Start, Is.Null);
                Assert.That(handler.ActReqTask.CurrentHandler, Is.Null);
                Assert.That(handler.DisplayPromoteReqTaskMode, Is.False);
            });
        }

        [Test]
        public async Task PromoteImplTask_SetsStopAndUpdatesReqAndTicket()
        {
            WfHandler handler = new() { Phase = WorkflowPhases.implementation };
            handler.MasterStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            handler.ActStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            WfImplTask implTask = new() { Id = 3, ReqTaskId = 7, StateId = 0 };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                ImplementationTasks = { implTask }
            };
            handler.ActImplTask = implTask;
            handler.ActReqTask = reqTask;
            handler.ActTicket = new WfTicket { Id = 1, Tasks = { reqTask } };
            handler.DisplayPromoteImplTaskMode = true;

            await handler.PromoteImplTask(new WfStatefulObject { StateId = 5 });

            Assert.That(handler.ActImplTask.StateId, Is.EqualTo(5));
            Assert.That(handler.ActImplTask.Stop, Is.Not.Null);
            Assert.That(handler.ActReqTask.StateId, Is.EqualTo(5));
            Assert.That(handler.ActReqTask.Stop, Is.Not.Null);
            Assert.That(handler.ActTicket.StateId, Is.EqualTo(5));
            Assert.That(handler.DisplayPromoteImplTaskMode, Is.False);
        }

        [Test]
        public async Task PromoteImplTask_PromotesAllBundledRequestTasks()
        {
            WfHandler handler = CreateImplementationHandler(considerBundling: true);
            WfImplTask implTask = new() { Id = 3, ReqTaskId = 7, StateId = 2 };
            WfReqTask firstReqTask = CreateBundledReqTask(7, implTask);
            WfReqTask secondReqTask = CreateBundledReqTask(8);
            firstReqTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-7-8");
            secondReqTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-7-8");
            handler.ActImplTask = implTask;
            handler.ActReqTask = firstReqTask;
            handler.ActTicket = new WfTicket { Id = 1, Tasks = { firstReqTask, secondReqTask } };

            await handler.PromoteImplTask(new WfStatefulObject { StateId = 5 });

            Assert.Multiple(() =>
            {
                Assert.That(firstReqTask.StateId, Is.EqualTo(5));
                Assert.That(secondReqTask.StateId, Is.EqualTo(5));
                Assert.That(firstReqTask.Stop, Is.Not.Null);
                Assert.That(secondReqTask.Stop, Is.EqualTo(firstReqTask.Stop));
                Assert.That(handler.ActTicket.StateId, Is.EqualTo(5));
            });
        }

        [Test]
        public async Task PromoteImplTask_IgnoresBundleMarkersWhenBundlingDisabled()
        {
            WfHandler handler = CreateImplementationHandler(considerBundling: false);
            WfImplTask implTask = new() { Id = 3, ReqTaskId = 7, StateId = 2 };
            WfReqTask firstReqTask = CreateBundledReqTask(7, implTask);
            WfReqTask secondReqTask = CreateBundledReqTask(8);
            firstReqTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-7-8");
            secondReqTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-7-8");
            handler.ActImplTask = implTask;
            handler.ActReqTask = firstReqTask;
            handler.ActTicket = new WfTicket { Id = 1, Tasks = { firstReqTask, secondReqTask } };

            await handler.PromoteImplTask(new WfStatefulObject { StateId = 5 });

            Assert.Multiple(() =>
            {
                Assert.That(firstReqTask.StateId, Is.EqualTo(5));
                Assert.That(secondReqTask.StateId, Is.EqualTo(0));
                Assert.That(secondReqTask.Stop, Is.Null);
            });
        }

        [Test]
        public async Task StartWorkOnImplTask_PromotesAllBundledRequestTasks()
        {
            const string taskType = "access";
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };
            matrix.Matrix[0] = [2];
            WfHandler handler = new()
            {
                Phase = WorkflowPhases.implementation,
                userConfig = new SimulatedUserConfig { ReqConsiderBundling = true },
                MasterStateMatrix = new StateMatrix
                {
                    LowestInputState = 0,
                    LowestStartedState = 2,
                    LowestEndState = 5
                }
            };
            SetMatrix(handler, taskType, matrix);
            WfImplTask implTask = new() { Id = 3, TicketId = 1, ReqTaskId = 7, TaskType = taskType, StateId = 0 };
            WfReqTask firstReqTask = CreateBundledReqTask(7, implTask);
            WfReqTask secondReqTask = CreateBundledReqTask(8);
            firstReqTask.TaskType = taskType;
            secondReqTask.TaskType = taskType;
            firstReqTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-7-8");
            secondReqTask.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-7-8");
            handler.TicketList.Add(new WfTicket { Id = 1, Tasks = { firstReqTask, secondReqTask } });

            await handler.StartWorkOnImplTask(implTask, ObjAction.implement);

            Assert.Multiple(() =>
            {
                Assert.That(firstReqTask.StateId, Is.EqualTo(2));
                Assert.That(secondReqTask.StateId, Is.EqualTo(2));
                Assert.That(handler.ActTicket.StateId, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task AutoPromote_TicketScope_UsesProvidedState()
        {
            WfHandler handler = new();
            handler.MasterStateMatrix = new StateMatrix
            {
                MinTicketCompleted = 99,
                LowestEndState = 10,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };
            WfTicket ticket = new() { Id = 1, StateId = 0 };
            handler.TicketList.Add(ticket);

            await handler.AutoPromote(ticket, WfObjectScopes.Ticket, 3);

            Assert.That(handler.ActTicket.StateId, Is.EqualTo(3));
            Assert.That(handler.DisplayTicketMode, Is.False);
            Assert.That(handler.EditTicketMode, Is.False);
        }

        [Test]
        public async Task AutoPromote_RequestTaskScope_UpdatesStateAndHandler()
        {
            WfHandler handler = new();
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);

            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                StateId = 0,
                CurrentHandler = new FWO.Data.UiUser { DbId = 99 }
            };
            handler.TicketList.Add(new WfTicket { Id = 1, Tasks = { reqTask } });

            await handler.AutoPromote(reqTask, WfObjectScopes.RequestTask, 3);

            Assert.That(handler.ActReqTask.StateId, Is.EqualTo(3));
            Assert.That(handler.ActReqTask.CurrentHandler?.DbId, Is.EqualTo(99));
            Assert.That(handler.ActTicket.Tasks[0].StateId, Is.EqualTo(3));
            Assert.That(handler.ActTicket.StateId, Is.EqualTo(3));
        }

        [Test]
        public async Task AutoPromote_ImplTaskScope_UpdatesStateAndHandler()
        {
            WfHandler handler = new();
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);

            WfImplTask implTask = new()
            {
                Id = 3,
                TicketId = 1,
                ReqTaskId = 7,
                TaskType = WfTaskType.access.ToString(),
                StateId = 0,
                CurrentHandler = new FWO.Data.UiUser { DbId = 42 }
            };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                ImplementationTasks = { implTask }
            };
            handler.TicketList.Add(new WfTicket { Id = 1, Tasks = { reqTask } });

            await handler.AutoPromote(implTask, WfObjectScopes.ImplementationTask, 4);

            Assert.That(handler.ActImplTask.StateId, Is.EqualTo(4));
            Assert.That(handler.ActImplTask.CurrentHandler?.DbId, Is.EqualTo(42));
            Assert.That(handler.ActReqTask.ImplementationTasks[0].StateId, Is.EqualTo(4));
        }

        private static WfHandler CreateImplementationHandler(bool considerBundling)
        {
            return new()
            {
                Phase = WorkflowPhases.implementation,
                userConfig = new SimulatedUserConfig { ReqConsiderBundling = considerBundling },
                MasterStateMatrix = new StateMatrix
                {
                    LowestInputState = 0,
                    LowestStartedState = 2,
                    LowestEndState = 5
                },
                ActStateMatrix = new StateMatrix
                {
                    LowestInputState = 0,
                    LowestStartedState = 2,
                    LowestEndState = 5
                }
            };
        }

        private static WfReqTask CreateBundledReqTask(long id, WfImplTask? implTask = null)
        {
            WfReqTask reqTask = new()
            {
                Id = id,
                TicketId = 1
            };
            if (implTask != null)
            {
                reqTask.ImplementationTasks.Add(implTask);
            }
            return reqTask;
        }

        [Test]
        public async Task AutoPromote_ApprovalScope_UpdatesApprovalAndReqTask()
        {
            WfHandler handler = new();
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);
            handler.MasterStateMatrix = new StateMatrix
            {
                LowestEndState = 10,
                MinTicketCompleted = 99,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };

            WfApproval approval = new() { Id = 1, TaskId = 7, StateId = 1 };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                Approvals = { approval }
            };
            handler.TicketList.Add(new WfTicket { Id = 1, Tasks = { reqTask } });

            await handler.AutoPromote(approval, WfObjectScopes.Approval, 5);

            Assert.That(handler.ActApproval.StateId, Is.EqualTo(5));
            Assert.That(handler.ActApproval.ApprovalDate, Is.Not.Null);
            Assert.That(handler.ActReqTask.StateId, Is.EqualTo(5));
        }

        [Test]
        public async Task UpdateRequestTasksFromTicket_UpdatesApprovalStatesFromReqTask()
        {
            WfHandler handler = new();
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5,
                ApprovalLowestEndState = 5,
                PhaseActive = new() { { WorkflowPhases.planning, false } },
                MinImplTasksNeeded = 99
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);
            handler.ActTicket = new WfTicket { Id = 1, StateId = 4 };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                StateId = 1,
                Approvals =
                {
                    new WfApproval { Id = 1, TaskId = 7, StateId = 1 },
                    new WfApproval { Id = 2, TaskId = 7, StateId = 6 }
                }
            };
            handler.ActTicket.Tasks.Add(reqTask);

            MethodInfo? method = typeof(WfHandler).GetMethod("UpdateRequestTasksFromTicket", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)(method?.Invoke(handler, [false, true]) ?? throw new InvalidOperationException("Method not found."));

            Assert.That(reqTask.StateId, Is.EqualTo(4));
            Assert.That(reqTask.Approvals[0].StateId, Is.EqualTo(4));
            Assert.That(reqTask.Approvals[1].StateId, Is.EqualTo(6));
        }

        [Test]
        public async Task UpdateRequestTasksFromTicket_DoesNotUpdateApprovalsAtLowestEndState()
        {
            WfHandler handler = new();
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5,
                ApprovalLowestEndState = 5,
                PhaseActive = new() { { WorkflowPhases.planning, false } },
                MinImplTasksNeeded = 99
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);
            handler.ActStateMatrix = matrix;
            handler.ActTicket = new WfTicket { Id = 1, StateId = 6 };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                StateId = 1,
                Approvals =
                {
                    new WfApproval { Id = 1, TaskId = 7, StateId = 4 },
                    new WfApproval { Id = 2, TaskId = 7, StateId = 5 }
                }
            };
            handler.ActTicket.Tasks.Add(reqTask);

            MethodInfo? method = typeof(WfHandler).GetMethod("UpdateRequestTasksFromTicket", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)(method?.Invoke(handler, [false, true]) ?? throw new InvalidOperationException("Method not found."));

            Assert.That(reqTask.StateId, Is.EqualTo(6));
            Assert.That(reqTask.Approvals[0].StateId, Is.EqualTo(6));
            Assert.That(reqTask.Approvals[1].StateId, Is.EqualTo(5));
        }

        [Test]
        public async Task UpdateRequestTasksFromTicket_DowngradesRequestTaskAndReturnsFalse()
        {
            WfHandler handler = new();
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5,
                ApprovalLowestEndState = 5,
                PhaseActive = new() { { WorkflowPhases.planning, false } },
                MinImplTasksNeeded = 99
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);
            handler.ActTicket = new WfTicket { Id = 1, StateId = 1 };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                StateId = 4,
                Approvals =
                {
                    new WfApproval { Id = 1, TaskId = 7, StateId = 4 }
                }
            };
            handler.ActTicket.Tasks.Add(reqTask);

            MethodInfo? method = typeof(WfHandler).GetMethod("UpdateRequestTasksFromTicket", BindingFlags.NonPublic | BindingFlags.Instance);
            bool requestTaskActionsChangedState = await (Task<bool>)(method?.Invoke(handler, [false, true]) ?? throw new InvalidOperationException("Method not found."));

            Assert.That(reqTask.StateId, Is.EqualTo(1));
            Assert.That(reqTask.Approvals[0].StateId, Is.EqualTo(1));
            Assert.That(requestTaskActionsChangedState, Is.False);
        }

        [Test]
        public async Task UpdateRequestTasksFromTicket_DoesNotCreateImplTasksForClosedStateAboveImplementationEntry()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.oneTaskForAllDevices }
            };
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 1,
                LowestEndState = 49,
                ApprovalLowestEndState = 49,
                PhaseActive = new() { { WorkflowPhases.planning, false } },
                MinImplTasksNeeded = 49,
                MinTicketCompleted = 620
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);
            handler.ActTicket = new WfTicket { Id = 1, StateId = 630 };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                StateId = 1,
                Title = "Access"
            };
            handler.ActTicket.Tasks.Add(reqTask);

            MethodInfo? method = typeof(WfHandler).GetMethod("UpdateRequestTasksFromTicket", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)(method?.Invoke(handler, [true, true]) ?? throw new InvalidOperationException("Method not found."));

            Assert.Multiple(() =>
            {
                Assert.That(reqTask.StateId, Is.EqualTo(630));
                Assert.That(reqTask.ImplementationTasks, Is.Empty);
            });
        }

        [Test]
        public async Task UpdateRequestTasksFromTicket_CreatesImplTasksForImplementationRangeWhenEntryStateIsSkipped()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig { ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.oneTaskForAllDevices }
            };
            StateMatrix matrix = new()
            {
                LowestInputState = 0,
                LowestStartedState = 1,
                LowestEndState = 49,
                ApprovalLowestEndState = 49,
                PhaseActive = new() { { WorkflowPhases.planning, false } },
                MinImplTasksNeeded = 49,
                MinTicketCompleted = 620
            };
            SetMatrix(handler, WfTaskType.access.ToString(), matrix);
            handler.ActTicket = new WfTicket { Id = 1, StateId = 60 };
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.access.ToString(),
                StateId = 1,
                Title = "Access"
            };
            handler.ActTicket.Tasks.Add(reqTask);

            MethodInfo? method = typeof(WfHandler).GetMethod("UpdateRequestTasksFromTicket", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)(method?.Invoke(handler, [true, true]) ?? throw new InvalidOperationException("Method not found."));

            Assert.Multiple(() =>
            {
                Assert.That(reqTask.StateId, Is.EqualTo(60));
                Assert.That(reqTask.ImplementationTasks, Has.Count.EqualTo(1));
                Assert.That(reqTask.ImplementationTasks[0].DeviceId, Is.Null);
            });
        }

        [Test]
        public async Task UpdateActTicketStateFromReqTasks_CanDowngradeTicketFromRequestTasks()
        {
            WfHandler handler = new();
            handler.MasterStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5,
                MinTicketCompleted = 99,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };
            handler.ActTicket = new WfTicket
            {
                Id = 1,
                StateId = 4,
                Tasks =
                {
                    new WfReqTask
                    {
                        Id = 7,
                        TaskType = WfTaskType.access.ToString(),
                        StateId = 1
                    }
                }
            };

            MethodInfo? method = typeof(WfHandler).GetMethod("UpdateActTicketStateFromReqTasks", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)(method?.Invoke(handler, [true, true]) ?? throw new InvalidOperationException("Method not found."));

            Assert.That(handler.ActTicket.StateId, Is.EqualTo(1));
        }

        [Test]
        public async Task ChangeTicketStateForMonitoring_LocalOnly_DoesNotTriggerActions()
        {
            WfHandler handler = new();
            SetDbAccess(handler, new MonitoringStateChangeApiConn());
            handler.MasterStateMatrix = new StateMatrix
            {
                LowestInputState = 0,
                LowestStartedState = 2,
                LowestEndState = 5,
                MinTicketCompleted = 99,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            };
            WfTicket ticket = new() { Id = 1, StateId = 1 };
            ticket.ResetStateChanged();

            await handler.ChangeTicketStateForMonitoring(ticket, 2, MonitoringStateChangeMode.LocalOnly);

            Assert.Multiple(() =>
            {
                Assert.That(ticket.StateId, Is.EqualTo(2));
                Assert.That(ticket.StateChanged(), Is.True);
            });
        }

        [Test]
        public async Task ChangeReqTaskStateForMonitoring_CascadeParents_DoesNotCreateImplTasks()
        {
            WfHandler handler = new()
            {
                MasterStateMatrix = new StateMatrix
                {
                    LowestEndState = 2,
                    MinTicketCompleted = 99,
                    PhaseActive = new() { { WorkflowPhases.planning, false } }
                }
            };
            SetMatrix(handler, WfTaskType.group_create.ToString(), new StateMatrix
            {
                MinImplTasksNeeded = 2,
                MinTicketCompleted = 99,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            });
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 1,
                TaskType = WfTaskType.group_create.ToString(),
                StateId = 1
            };
            WfTicket ticket = new()
            {
                Id = 1,
                StateId = 1,
                Tasks = { reqTask }
            };

            await handler.ChangeReqTaskStateForMonitoring(ticket, reqTask, 2, MonitoringStateChangeMode.CascadeParents);

            WfReqTask syncedReqTask = handler.ActTicket.Tasks.Single(task => task.Id == reqTask.Id);
            Assert.Multiple(() =>
            {
                Assert.That(ticket.StateId, Is.EqualTo(2));
                Assert.That(handler.ActReqTask.StateId, Is.EqualTo(2));
                Assert.That(syncedReqTask.StateId, Is.EqualTo(2));
                Assert.That(handler.ActReqTask.ImplementationTasks, Is.Empty);
                Assert.That(syncedReqTask.ImplementationTasks, Is.Empty);
            });
        }

        [Test]
        [NonParallelizable]
        public async Task ChangeTicketStateForMonitoring_WritesWarningAuditLog()
        {
            WfHandler handler = new()
            {
                userConfig = new SimulatedUserConfig(),
                AuthUser = new ClaimsPrincipal(new ClaimsIdentity([new Claim("x-hasura-uuid", "admin-user")], "test")),
                MasterStateMatrix = new StateMatrix
                {
                    Matrix = new() { [1] = [2] },
                    MinTicketCompleted = 99,
                    PhaseActive = new() { { WorkflowPhases.planning, false } }
                }
            };
            WfTicket ticket = new() { Id = 42, StateId = 1 };
            ticket.ResetStateChanged();

            string logOutput = await CaptureConsoleOutput(async () =>
                await handler.ChangeTicketStateForMonitoring(ticket, 2, MonitoringStateChangeMode.LocalOnly));

            Assert.Multiple(() =>
            {
                Assert.That(logOutput, Does.Contain("Warning - Workflow Monitoring"));
                Assert.That(logOutput, Does.Contain("admin-user"));
                Assert.That(logOutput, Does.Contain("Ticket 42"));
                Assert.That(logOutput, Does.Contain("from state 1 to 2"));
                Assert.That(logOutput, Does.Contain("LocalOnly"));
            });
        }

        [Test]
        [NonParallelizable]
        public async Task AutoCreateInitialImplTasksForMonitoring_WritesWarningAuditLogWhenTaskCreated()
        {
            WfHandler handler = new()
            {
                AuthUser = new ClaimsPrincipal(new ClaimsIdentity([new Claim("x-hasura-uuid", "admin-user")], "test")),
                userConfig = new SimulatedUserConfig
                {
                    ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.oneTaskForAllDevices,
                    ReqConsiderBundling = false
                }
            };
            SetMatrix(handler, WfTaskType.group_create.ToString(), new StateMatrix
            {
                MinImplTasksNeeded = 3,
                MinTicketCompleted = 99,
                PhaseActive = new() { { WorkflowPhases.planning, false } }
            });
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = 42,
                StateId = 4,
                TaskType = WfTaskType.group_create.ToString()
            };
            WfTicket ticket = new() { Id = 42, Tasks = [reqTask] };

            string logOutput = await CaptureConsoleOutput(async () =>
                await handler.AutoCreateInitialImplTasksForMonitoring(ticket, reqTask));

            Assert.Multiple(() =>
            {
                Assert.That(logOutput, Does.Contain("Warning - Workflow Monitoring"));
                Assert.That(logOutput, Does.Contain("admin-user"));
                Assert.That(logOutput, Does.Contain("implementation task creation"));
                Assert.That(logOutput, Does.Contain("created 1 implementation task"));
                Assert.That(logOutput, Does.Contain("request task 7"));
                Assert.That(logOutput, Does.Contain("ticket 42"));
            });
        }

        private sealed class MonitoringStateChangeApiConn : SimulatedApiConnection
        {
            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == RequestQueries.updateTicketState || query == RequestQueries.updateRequestTaskState)
                {
                    long id = Convert.ToInt64(variables?.GetType().GetProperty("id")?.GetValue(variables));
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = id });
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }
    }
}
