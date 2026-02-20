using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public partial class WfHandler
    {
        public bool DisplayTicketMode = false;
        public bool EditTicketMode = false;
        public bool AddTicketMode = false;
        public bool DisplayPromoteTicketMode = false;
        public bool DisplaySaveTicketMode = false;

        // Tickets

        public async Task<WfTicket?> ResolveTicket(long ticketId)
        {
            WfTicket? ticket = null;
            if (dbAcc != null)
            {
                ticket = await dbAcc.FetchTicket(ticketId, userConfig.ReqOwnerBased ? AllOwners.ConvertAll(x => x.Id) : null);
                if (ticket != null)
                {
                    SetTicketEnv(ticket);
                }
            }
            return ticket;
        }

        public async Task<string> HandleInjectedTicketId(WorkflowPhases phase, long ticketId)
        {
            WfTicket? ticket = await ResolveTicket(ticketId);
            if (ticket != null)
            {
                if (ticket.StateId < MasterStateMatrix.LowestEndState)
                {
                    await SelectTicket(ticket, ObjAction.edit, true);
                }
                else if (MasterStateMatrix.IsLastActivePhase)
                {
                    await SelectTicket(ticket, ObjAction.display, true);
                }
                else
                {
                    (WorkflowPhases newPhase, bool foundNewPhase) = await FindNewPhase(phase, ticket.StateId);
                    if (foundNewPhase)
                    {
                        return newPhase.ToString();
                    }
                }
            }
            return "";
        }

        public async Task<List<WfTicket>> GetOpenTickets(string taskType, int cutOffPeriod = 0, SchedulerInterval interval = SchedulerInterval.Days)
        {
            if (dbAcc != null)
            {
                DateTime cutOffDate = interval switch
                {
                    SchedulerInterval.Days => DateTime.Now.AddDays(-cutOffPeriod),
                    SchedulerInterval.Weeks => DateTime.Now.AddDays(-cutOffPeriod * GlobalConst.kDaysPerWeek),
                    SchedulerInterval.Months => DateTime.Now.AddMonths(-cutOffPeriod),
                    _ => throw new NotSupportedException("Time interval is not supported."),
                };
                return await dbAcc.GetTicketsByParameters(taskType, StateMatrix(taskType).LowestInputState, StateMatrix(taskType).LowestEndState, cutOffDate);
            }
            return [];
        }

        public async Task SelectTicket(WfTicket ticket, ObjAction action, bool reload = false)
        {
            if (ReloadTasks && reload && dbAcc != null)
            {
                ticket = await dbAcc.FetchTicket(ticket.Id) ?? ticket;
                TicketList[TicketList.FindIndex(x => x.Id == ticket.Id)] = ticket;
            }
            SetTicketEnv(ticket);
            SetTicketOpt(action);
        }

        public void SetTicketEnv(WfTicket ticket)
        {
            ActTicket = ticket;
            ResetImplTaskList();
            ActStateMatrix = MasterStateMatrix;
        }

        public void SetTicketOpt(ObjAction action)
        {
            ResetTicketActions();
            DisplayTicketMode = action == ObjAction.display || action == ObjAction.edit || action == ObjAction.add;
            EditTicketMode = action == ObjAction.edit || action == ObjAction.add;
            AddTicketMode = action == ObjAction.add;
        }

        public void SetTicketPopUpOpt(ObjAction action)
        {
            DisplayPromoteTicketMode = action == ObjAction.displayPromote;
            DisplaySaveTicketMode = action == ObjAction.displaySaveTicket;
        }

        public void ResetTicketActions()
        {
            DisplayTicketMode = false;
            EditTicketMode = false;
            AddTicketMode = false;
            DisplayPromoteTicketMode = false;
            DisplaySaveTicketMode = false;
        }

        public async Task<long> SaveTicket(WfStatefulObject ticket)
        {
            try
            {
                if (dbAcc != null)
                {
                    ActTicket.StateId = ticket.StateId;
                    PrepareTicketData();

                    if (AddTicketMode)
                    {
                        // insert new ticket
                        ActTicket.CreationDate = DateTime.Now;
                        ActTicket.Requester = userConfig.User;
                        ActTicket = await dbAcc.AddTicketToDb(ActTicket);
                        TicketList.Add(ActTicket);
                    }
                    else
                    {
                        // Update existing ticket
                        ActTicket = await dbAcc.UpdateTicketInDb(ActTicket);
                        TicketList[TicketList.FindIndex(x => x.Id == ActTicket.Id)] = ActTicket;
                    }

                    // update of request tasks and creation of impl tasks may be necessary
                    await UpdateRequestTasksFromTicket();

                    //check for further promotion (req tasks may be promoted)
                    await UpdateActTicketStateFromReqTasks();

                    ResetTicketActions();
                    return ActTicket.Id;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("save_request"), "", true);
            }
            return 0;
        }

        public async Task ConfAddCommentToTicket(string commentText)
        {
            WfComment comment = new()
            {
                Scope = WfObjectScopes.Ticket.ToString(),
                CreationDate = DateTime.Now,
                Creator = userConfig.User,
                CommentText = commentText
            };
            if (dbAcc != null)
            {
                long commentId = await dbAcc.AddCommentToDb(comment);
                if (commentId != 0)
                {
                    await dbAcc.AssignCommentToTicketInDb(ActTicket.Id, commentId);
                }
            }
            ActTicket.Comments.Add(new WfCommentDataHelper(comment) { });
        }

        private async Task<(WorkflowPhases, bool)> FindNewPhase(WorkflowPhases phase, int stateId)
        {
            bool foundNewPhase = false;
            if (apiConnection != null)
            {
                GlobalStateMatrix glbStateMatrix = GlobalStateMatrix.Create();
                await glbStateMatrix.Init(apiConnection, WfTaskType.master);
                bool cont = true;
                while (cont)
                {
                    bool newPhase = MasterStateMatrix.getNextActivePhase(ref phase);
                    if (newPhase)
                    {
                        foundNewPhase = true;
                    }
                    cont = stateId >= glbStateMatrix.GlobalMatrix[phase].LowestEndState && newPhase;
                }
            }
            return (phase, foundNewPhase);
        }

        private void PrepareTicketData()
        {
            if (ActTicket.Sanitize())
            {
                DisplayMessageInUi(null, userConfig.GetText("save_request"), userConfig.GetText("U0001"), true);
            }
            foreach (WfReqTask reqTask in ActTicket.Tasks)
            {
                if (reqTask.StateId < ActTicket.StateId)
                {
                    reqTask.StateId = ActTicket.StateId;
                }
            }

            if (ActTicket.Deadline == null)
            {
                int? tickDeadline = PrioList.FirstOrDefault(x => x.NumPrio == ActTicket.Priority)?.TicketDeadline;
                ActTicket.Deadline = tickDeadline != null && tickDeadline > 0 ? DateTime.Now.AddDays((int)tickDeadline) : null;
            }
        }
    }
}
