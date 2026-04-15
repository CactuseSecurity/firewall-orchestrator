using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Middleware.Client;
using System.Security.Authentication;
using System.Text.Json;

namespace FWO.Services.Workflow
{
    public enum ObjAction
    {
        display,
        edit,
        add,
        approve,
        plan,
        implement,
        review,
        displayAssign,
        displayApprovals,
        displayApprove,
        displayPromote,
        displayDelete,
        displaySaveTicket,
        displayComment,
        displayCleanup,
        displayPathAnalysis
    }

    public partial class WfHandler
    {
        public List<WfTicket> TicketList { get; set; } = [];
        public WfTicket ActTicket { get; set; } = new();
        public WfReqTask ActReqTask { get; set; } = new();
        public WfImplTask ActImplTask { get; set; } = new();
        public WfApproval ActApproval { get; set; } = new();

        public WorkflowPhases Phase = WorkflowPhases.request;
        public List<Device> Devices = [];
        public List<FwoOwner> AllOwners { get; set; } = [];
        public List<WfPriority> PrioList = [];
        public List<WfImplTask> AllTicketImplTasks = [];
        public List<WfImplTask> AllVisibleImplTasks = [];
        public StateMatrix ActStateMatrix = new();
        public StateMatrix MasterStateMatrix = new();
        public ActionHandler? ActionHandler;
        public bool ReadOnlyMode = false;
        public bool ApproveReqTaskMode = false;
        public bool DisplayApproveMode = false;
        public bool DisplayPromoteReqTaskMode = false;
        public bool DisplayPromoteImplTaskMode = false;

        public bool InitDone = false;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;
        public UserConfig userConfig;
        public System.Security.Claims.ClaimsPrincipal? AuthUser;
        private readonly ApiConnection? apiConnection;
        public readonly MiddlewareClient? MiddlewareClient;
        public readonly IRequestedRulePolicyChecker? RequestedRulePolicyChecker;
        private readonly StateMatrixDict stateMatrixDict = new();
        private WfDbAccess? dbAcc;

        private ObjAction contOption = ObjAction.display;
        private bool InitOngoing = false;
        private readonly bool usedInMwServer = false;
        private readonly List<UserGroup>? UserGroups = null;
        private bool ReloadTasks = false;


        public WfHandler()
        {
            userConfig = new();
        }

        /// <summary>
        /// constructor for use in UI
        /// </summary>
        public WfHandler(Action<Exception?, string, string, bool> displayMessageInUi, UserConfig userConfig,
            System.Security.Claims.ClaimsPrincipal authUser, ApiConnection apiConnection, MiddlewareClient middlewareClient, WorkflowPhases phase,
            IRequestedRulePolicyChecker? requestedRulePolicyChecker = null)
        {
            DisplayMessageInUi = displayMessageInUi;
            this.userConfig = userConfig;
            this.apiConnection = apiConnection;
            Phase = phase;
            MiddlewareClient = middlewareClient;
            RequestedRulePolicyChecker = requestedRulePolicyChecker;
            AuthUser = authUser;
        }

        /// <summary>
        /// constructor for use in middleware server
        /// </summary>
        public WfHandler(UserConfig userConfig, ApiConnection apiConnection, WorkflowPhases phase, List<UserGroup>? userGroups,
            IRequestedRulePolicyChecker? requestedRulePolicyChecker = null)
        {
            DisplayMessageInUi = LogMessage;
            this.userConfig = userConfig;
            this.apiConnection = apiConnection;
            Phase = phase;
            UserGroups = userGroups;
            RequestedRulePolicyChecker = requestedRulePolicyChecker;
            usedInMwServer = true;
        }

        public async Task<bool> Init(bool fetchData = false, List<int>? ownerIds = null, bool allStates = false, bool fullTickets = false)
        {
            try
            {
                if (!InitOngoing && apiConnection != null)
                {
                    Log.WriteDebug("Init start:  ", $"{DateTime.Now:hh:mm:ss,fff}");
                    InitOngoing = true;
                    if (usedInMwServer)
                    {
                        apiConnection.SetRole(Roles.MiddlewareServer);
                    }
                    else if (AuthUser != null)
                    {
                        apiConnection.SetProperRole(AuthUser, [Roles.Admin, Roles.FwAdmin, Roles.Requester, Roles.Approver, Roles.Planner, Roles.Implementer, Roles.Reviewer, Roles.Modeller, Roles.Auditor]);
                    }
                    else
                    {
                        throw new AuthenticationException("No AuthUser set");
                    }
                    ActionHandler = new(apiConnection, this, UserGroups, usedInMwServer, RequestedRulePolicyChecker);
                    await ActionHandler.Init();
                    dbAcc = new WfDbAccess(DisplayMessageInUi, userConfig, apiConnection, ActionHandler, AuthUser == null || AuthUser.IsInRole(Roles.Admin) || AuthUser.IsInRole(Roles.Auditor)) { };
                    Devices = await apiConnection.SendQueryAsync<List<Device>>(DeviceQueries.getDeviceDetails);
                    AllOwners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
                    await stateMatrixDict.Init(Phase, apiConnection);
                    MasterStateMatrix = stateMatrixDict.Matrices[WfTaskType.master.ToString()];
                    if (fetchData)
                    {
                        TicketList = await dbAcc.FetchTickets(MasterStateMatrix, ownerIds, allStates, fullTickets);
                    }
                    ReloadTasks = !fullTickets;
                    PrioList = System.Text.Json.JsonSerializer.Deserialize<List<WfPriority>>(userConfig.ReqPriorities) ?? throw new JsonException("Config data could not be parsed.");
                    apiConnection.SwitchBack();
                    Log.WriteDebug("Init stop:   ", $"{DateTime.Now:hh:mm:ss,fff}");
                    InitOngoing = false;
                    InitDone = true;
                    return true;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("init_environment"), "", true);
            }
            return false;
        }

        public void FilterForRequester()
        {
            List<WfTicket> filteredTicketList = [];
            foreach (var ticket in TicketList)
            {
                if (userConfig.User.DbId == ticket.Requester?.DbId)
                {
                    filteredTicketList.Add(ticket);
                }
            }
            TicketList = filteredTicketList;
        }

        public StateMatrix StateMatrix(string taskType)
        {
            try
            {
                return stateMatrixDict.Matrices[taskType];
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("state_matrix"), "", true);
                return new();
            }
        }

        public void SetContinueEnv(ObjAction action)
        {
            contOption = action;
        }

        public async Task<bool> CheckRuleUid(int? deviceId, string? ruleUid)
        {
            if (dbAcc != null)
            {
                return await dbAcc.FindRuleUid(deviceId, ruleUid);
            }
            return false;
        }

        private static void LogMessage(Exception? exception = null, string title = "", string message = "", bool ErrorFlag = false)
        {
            if (exception == null)
            {
                if (ErrorFlag)
                {
                    Log.WriteWarning(title, message);
                }
                else
                {
                    Log.WriteInfo(title, message);
                }
            }
            else
            {
                Log.WriteError(title, message, exception);
            }
        }
    }
}
