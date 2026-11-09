using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerApprovalsTest
    {
        [Test]
        public async Task SetApprovalEnv_SelectsOpenApproval()
        {
            WfHandler handler = new();
            handler.ActStateMatrix = new StateMatrix { LowestEndState = 4 };
            handler.ActReqTask = new WfReqTask
            {
                Approvals =
                [
                    new WfApproval { Id = 1, StateId = 1 },
                    new WfApproval { Id = 2, StateId = 5 }
                ]
            };

            await handler.SetApprovalEnv(createIfMissing: false);

            Assert.That(handler.ActApproval.Id, Is.EqualTo(1));
        }

        [Test]
        public async Task SetApprovalEnv_FallsBackToLastApproval()
        {
            WfHandler handler = new();
            handler.ActStateMatrix = new StateMatrix { LowestEndState = 4 };
            handler.ActReqTask = new WfReqTask
            {
                Approvals =
                [
                    new WfApproval { Id = 1, StateId = 4 },
                    new WfApproval { Id = 2, StateId = 6 }
                ]
            };

            await handler.SetApprovalEnv(createIfMissing: false);

            Assert.That(handler.ActApproval.Id, Is.EqualTo(2));
        }

        [Test]
        public async Task AddApproval_UsesExtParams()
        {
            WfHandler handler = new();
            handler.ActStateMatrix = new StateMatrix { LowestEndState = 9 };
            handler.ActReqTask = new WfReqTask { Id = 10 };
            handler.ActTicket = new WfTicket { TenantId = 3 };
            ApprovalParams approvalParams = new()
            {
                StateId = 7,
                ApproverGroup = "cn=group",
                Deadline = 1
            };
            string extParams = JsonSerializer.Serialize(approvalParams);

            await handler.AddApproval(extParams);

            WfApproval approval = handler.ActReqTask.Approvals[^1];
            Assert.That(approval.StateId, Is.EqualTo(7));
            Assert.That(approval.ApproverGroup, Is.EqualTo("cn=group"));
            Assert.That(approval.Deadline?.Date, Is.EqualTo(DateTime.Now.AddDays(1).Date));
        }

        [Test]
        public async Task AddApproval_DefaultsToLowestInputState_AndMarksInitial()
        {
            WfHandler handler = new();
            handler.ActStateMatrix = new StateMatrix { LowestInputState = 0, LowestEndState = 7 };
            handler.ActReqTask = new WfReqTask { Id = 10 };
            handler.ActTicket = new WfTicket { Priority = 2 };
            handler.PrioList =
            [
                new WfPriority { NumPrio = 2, ApprovalDeadline = 2 }
            ];

            await handler.AddApproval();

            WfApproval approval = handler.ActReqTask.Approvals[^1];
            Assert.That(approval.StateId, Is.EqualTo(0));
            Assert.That(approval.InitialApproval, Is.True);
            Assert.That(approval.Deadline?.Date, Is.EqualTo(DateTime.Now.AddDays(2).Date));
        }

        [Test]
        public void SetApprovalPopUpOpt_AndResetApprovalActions_ToggleFlags()
        {
            WfHandler handler = new();

            handler.SetApprovalPopUpOpt(ObjAction.displayAssign);
            Assert.That(handler.DisplayAssignApprovalMode, Is.True);
            Assert.That(handler.DisplayApprovalCommentMode, Is.False);

            handler.SetApprovalPopUpOpt(ObjAction.displayComment);
            Assert.That(handler.DisplayAssignApprovalMode, Is.False);
            Assert.That(handler.DisplayApprovalCommentMode, Is.True);

            handler.ResetApprovalActions();
            Assert.That(handler.DisplayAssignApprovalMode, Is.False);
            Assert.That(handler.DisplayApprovalCommentMode, Is.False);
        }

        [Test]
        public async Task ConfAddCommentToApproval_AddsCommentAndResetsFlag()
        {
            WfHandler handler = new();
            handler.ActApproval = new WfApproval { Id = 5 };
            handler.DisplayApprovalCommentMode = true;

            await handler.ConfAddCommentToApproval("comment");

            Assert.That(handler.ActApproval.Comments, Has.Count.EqualTo(1));
            Assert.That(handler.ActApproval.Comments[0].Comment.CommentText, Is.EqualTo("comment"));
            Assert.That(handler.ActApproval.Comments[0].Comment.Scope, Is.EqualTo(WfObjectScopes.Approval.ToString()));
            Assert.That(handler.DisplayApprovalCommentMode, Is.False);
        }

        [Test]
        public async Task ConfAddCommentToApproval_PersistsAndAssignsComment()
        {
            ApprovalApiConnection apiConnection = new();
            WfHandler handler = CreateApprovalHandlerWithDb(apiConnection);

            await handler.ConfAddCommentToApproval("persisted comment");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.NewCommentCallCount, Is.EqualTo(1));
                Assert.That(apiConnection.AssignCommentToApprovalCallCount, Is.EqualTo(1));
                Assert.That(handler.ActApproval.Comments.Single().Comment.CommentText, Is.EqualTo("persisted comment"));
                Assert.That(handler.DisplayApprovalCommentMode, Is.False);
            });
        }

        [Test]
        public async Task ApproveTask_WithComment_StoresTerminalMetadataAndComment()
        {
            WfHandler handler = CreateApprovalHandler(4);
            handler.userConfig.User.Dn = "cn=approver";
            WfStatefulObject targetState = new() { StateId = 4 };
            targetState.SetOptComment("approved with note");

            await handler.ApproveTask(targetState);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActApproval.StateId, Is.EqualTo(4));
                Assert.That(handler.ActApproval.ApprovalDate, Is.Not.Null);
                Assert.That(handler.ActApproval.ApproverDn, Is.EqualTo("cn=approver"));
                Assert.That(handler.ActReqTask.StateId, Is.EqualTo(4));
                Assert.That(handler.ActApproval.Comments, Has.Count.EqualTo(1));
                Assert.That(handler.ActApproval.Comments[0].Comment.CommentText, Is.EqualTo("approved with note"));
            });
        }

        [Test]
        public async Task ApproveTask_BeforeTerminalState_DoesNotStampApprovalOrAddComment()
        {
            WfHandler handler = CreateApprovalHandler(4);

            await handler.ApproveTask(new WfStatefulObject { StateId = 2 });

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActApproval.StateId, Is.EqualTo(2));
                Assert.That(handler.ActApproval.ApprovalDate, Is.Null);
                Assert.That(handler.ActApproval.ApproverDn, Is.Null.Or.Empty);
                Assert.That(handler.ActApproval.Comments, Is.Empty);
                Assert.That(handler.ActReqTask.StateId, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task AssignApprovalGroup_UpdatesGroupAndClearsDisplay()
        {
            WfHandler handler = new();
            WfStatefulObject statefulObject = new() { AssignedGroup = "cn=group" };
            handler.ActApproval = new WfApproval
            {
                AssignedGroup = "cn=old",
                CurrentHandler = new UiUser { Name = "handler" }
            };
            handler.ActReqTask = new WfReqTask
            {
                Approvals =
                [
                    handler.ActApproval
                ]
            };
            handler.DisplayAssignApprovalMode = true;

            await handler.AssignApprovalGroup(statefulObject);

            Assert.That(handler.ActApproval.AssignedGroup, Is.EqualTo("cn=group"));
            Assert.That(handler.ActApproval.RecentHandler, Is.EqualTo(handler.ActApproval.CurrentHandler));
            Assert.That(handler.DisplayAssignApprovalMode, Is.False);
        }

        [Test]
        public async Task AssignApproval_WithActionHandler_PersistsApprovalAndClosesDisplay()
        {
            ApprovalApiConnection apiConnection = new();
            WfHandler handler = CreateApprovalHandlerWithDb(apiConnection);
            handler.ActionHandler = new ActionHandler(apiConnection, handler);
            handler.DisplayAssignApprovalMode = true;

            await handler.AssignApprovalGroup(new WfStatefulObject { AssignedGroup = "cn=approver" });

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpdateApprovalCallCount, Is.EqualTo(1));
                Assert.That(handler.ActApproval.AssignedGroup, Is.EqualTo("cn=approver"));
                Assert.That(handler.ActApproval.RecentHandler, Is.EqualTo(handler.ActApproval.CurrentHandler));
                Assert.That(handler.DisplayAssignApprovalMode, Is.False);
            });
        }

        [Test]
        public async Task UpdateActApproval_PersistsApprovalAndUpdatesRequestTask()
        {
            ApprovalApiConnection apiConnection = new();
            WfHandler handler = CreateApprovalHandlerWithDb(apiConnection);
            handler.ActApproval.StateId = 3;

            MethodInfo method = typeof(WfHandler).GetMethod("UpdateActApproval", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(WfHandler).FullName, "UpdateActApproval");
            await (Task)(method.Invoke(handler, [false]) ?? throw new InvalidOperationException("UpdateActApproval returned null."));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpdateApprovalCallCount, Is.EqualTo(1));
                Assert.That(handler.ActReqTask.Approvals.Single().StateId, Is.EqualTo(3));
                Assert.That(handler.ActReqTask.Approvals.Single(), Is.SameAs(handler.ActApproval));
            });
        }

        private static WfHandler CreateApprovalHandlerWithDb(ApprovalApiConnection apiConnection)
        {
            WfHandler handler = CreateApprovalHandler(4);
            ActionHandler actionHandler = new(apiConnection, handler);
            WfDbAccess dbAccess = new(DefaultInit.DoNothing, handler.userConfig, apiConnection, actionHandler, false, WorkflowPhases.approval);
            typeof(WfHandler).GetField("dbAcc", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(handler, dbAccess);
            return handler;
        }

        private sealed class ApprovalApiConnection : SimulatedApiConnection
        {
            public int NewCommentCallCount { get; private set; }
            public int AssignCommentToApprovalCallCount { get; private set; }
            public int UpdateApprovalCallCount { get; private set; }

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == RequestQueries.newComment)
                {
                    NewCommentCallCount++;
                    return Task.FromResult((T)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewIdLong = 601 }]
                    });
                }
                if (query == RequestQueries.addCommentToApproval)
                {
                    AssignCommentToApprovalCallCount++;
                    return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = [] });
                }
                if (query == RequestQueries.updateApproval)
                {
                    UpdateApprovalCallCount++;
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = 5 });
                }
                if (query == RequestQueries.getTicketById)
                {
                    return Task.FromResult((T)(object)new WfTicket
                    {
                        Id = 20,
                        Tasks =
                        [
                            new WfReqTask
                            {
                                Id = 10,
                                Approvals = [new WfApproval { Id = 5, StateId = 1 }]
                            }
                        ]
                    });
                }
                if (query == ModellingQueries.addHistoryEntry || query == ModellingQueries.addHistoryEntryAsService)
                {
                    return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = [] });
                }
                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }

        private static WfHandler CreateApprovalHandler(int lowestEndState)
        {
            WfApproval approval = new() { Id = 5, TaskId = 10, StateId = 1 };
            WfReqTask reqTask = new()
            {
                Id = 10,
                TicketId = 20,
                TaskType = WfTaskType.access.ToString(),
                StateId = 1,
                Approvals = [approval]
            };
            WfHandler handler = new()
            {
                ActStateMatrix = new StateMatrix { LowestInputState = 0, LowestEndState = lowestEndState },
                MasterStateMatrix = new StateMatrix
                {
                    LowestEndState = 99,
                    MinTicketCompleted = 100,
                    PhaseActive = new() { { WorkflowPhases.planning, false } }
                },
                ActApproval = approval,
                ActReqTask = reqTask,
                ActTicket = new WfTicket { Id = 20, StateId = 1, Tasks = [reqTask] }
            };
            return handler;
        }
    }
}
