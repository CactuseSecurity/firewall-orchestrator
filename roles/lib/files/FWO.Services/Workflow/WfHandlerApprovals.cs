using FWO.Data;
using FWO.Data.Workflow;
using System.Text.Json;

namespace FWO.Services.Workflow
{
    public partial class WfHandler
    {
        public bool DisplayAssignApprovalMode = false;
        public bool DisplayApprovalCommentMode = false;

        // approvals

        public async Task SetApprovalEnv(WfApproval? approval = null, bool createIfMissing = true)
        {
            if (approval != null)
            {
                ActApproval = new WfApproval(approval);
            }
            else
            {
                if (ActReqTask.Approvals.Count == 0 && createIfMissing)
                {
                    await AddApproval();
                }
                ActApproval = ActReqTask.Approvals.FirstOrDefault(x => x.StateId < ActStateMatrix.LowestEndState)
                    ?? ActReqTask.Approvals.Last() ?? new();  // todo: select own approvals
            }
        }

        public void SetApprovalPopUpOpt(ObjAction action)
        {
            DisplayAssignApprovalMode = action == ObjAction.displayAssign;
            DisplayApprovalCommentMode = action == ObjAction.displayComment;
        }

        public async Task SelectApprovalPopUp(WfApproval approval, ObjAction action)
        {
            await SetApprovalEnv(approval);
            SetApprovalPopUpOpt(action);
        }

        public void ResetApprovalActions()
        {
            DisplayAssignApprovalMode = false;
            DisplayApprovalCommentMode = false;
        }

        public async Task AddApproval(string extParams = "")
        {
            ApprovalParams approvalParams = new();
            if (extParams != "")
            {
                approvalParams = JsonSerializer.Deserialize<ApprovalParams>(extParams) ?? throw new JsonException("Extparams could not be parsed.");
            }

            DateTime? deadline = null;
            if (extParams != "")
            {
                deadline = approvalParams.Deadline > 0 ? DateTime.Now.AddDays(approvalParams.Deadline) : null;
            }
            else
            {
                int? appDeadline = PrioList.FirstOrDefault(x => x.NumPrio == ActTicket.Priority)?.ApprovalDeadline;
                deadline = appDeadline != null && appDeadline > 0 ? DateTime.Now.AddDays((int)appDeadline) : null;
            }

            WfApproval approval = new()
            {
                TaskId = ActReqTask.Id,
                StateId = extParams != "" ? approvalParams.StateId : ActStateMatrix.LowestInputState,
                ApproverGroup = extParams != "" ? approvalParams.ApproverGroup : "", // todo: get from owner ???,
                TenantId = ActTicket.TenantId, // ??
                Deadline = deadline,
                InitialApproval = ActReqTask.Approvals.Count == 0
            };
            if (!approval.InitialApproval && dbAcc != null)
            {
                // todo: checks if new approval allowed (only one open per group?, ...)
                approval.Id = await dbAcc.AddApprovalToDb(approval);
                DisplayMessageInUi(null, userConfig.GetText("add_approval"), userConfig.GetText("U8002"), false);
            }
            ActReqTask.Approvals.Add(approval);
        }

        public async Task ApproveTask(WfStatefulObject approval)
        {
            try
            {
                ActApproval.StateId = approval.StateId;
                if (ActApproval.StateId >= ActStateMatrix.LowestEndState)
                {
                    ActApproval.ApprovalDate = DateTime.Now;
                    ActApproval.ApproverDn = userConfig.User.Dn;
                }
                if (approval.OptComment() != null && approval.OptComment() != "")
                {
                    await ConfAddCommentToApproval(approval.OptComment()!);
                }

                if (ActApproval.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_approval"), userConfig.GetText("U0001"), true);
                }
                await UpdateActApproval();
                await UpdateActReqTaskStateFromApprovals();
                SyncActTicketFromReqTask(ActReqTask);
                await UpdateActTicketStateFromReqTasks();
                ApproveReqTaskMode = false;
                DisplayApproveMode = false;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("save_approval"), "", true);
            }
        }

        public async Task AssignApprovalGroup(WfStatefulObject statefulObject)
        {
            ActApproval.AssignedGroup = statefulObject.AssignedGroup;
            await AssignApproval(statefulObject);
        }

        public async Task AssignApprovalBack() // Todo: implement callers
        {
            ActApproval.AssignedGroup = ActApproval.RecentHandler?.Dn;
            await AssignApproval(ActApproval);
        }

        public async Task ConfAddCommentToApproval(string commentText)
        {
            WfComment comment = new()
            {
                Scope = WfObjectScopes.Approval.ToString(),
                CreationDate = DateTime.Now,
                Creator = userConfig.User,
                CommentText = commentText
            };
            if (dbAcc != null)
            {
                long commentId = await dbAcc.AddCommentToDb(comment);
                if (commentId != 0)
                {
                    await dbAcc.AssignCommentToApprovalInDb(ActApproval.Id, commentId);
                }
            }
            ActApproval.Comments.Add(new WfCommentDataHelper(comment) { });
            DisplayApprovalCommentMode = false;
        }

        private async Task AssignApproval(WfStatefulObject statefulObject)
        {
            ActApproval.RecentHandler = ActApproval.CurrentHandler;
            await UpdateActApproval();
            if (ActionHandler != null)
            {
                await ActionHandler.DoOnAssignmentActions(statefulObject, ActApproval.AssignedGroup);
            }
            DisplayAssignApprovalMode = false;
        }

        private async Task UpdateActApproval()
        {
            if (dbAcc != null)
            {
                await dbAcc.UpdateApprovalInDb(ActApproval);
            }
            ActReqTask.Approvals[ActReqTask.Approvals.FindIndex(x => x.Id == ActApproval.Id)] = ActApproval;
        }

        private async Task UpdateActReqTaskStateFromApprovals()
        {
            if (ActReqTask.Approvals.Count > 0)
            {
                List<int> approvalStates = [];
                foreach (var approval in ActReqTask.Approvals)
                {
                    approvalStates.Add(approval.StateId);
                }
                ActReqTask.StateId = ActStateMatrix.getDerivedStateFromSubStates(approvalStates);
            }
            await UpdateActReqTaskState();

            // in the case impl tasks are already existing
            foreach (var implTask in ActReqTask.ImplementationTasks)
            {
                implTask.StateId = ActReqTask.StateId;
                if (dbAcc != null)
                {
                    await dbAcc.UpdateImplTaskStateInDb(implTask);
                }
            }
        }
    }
}
