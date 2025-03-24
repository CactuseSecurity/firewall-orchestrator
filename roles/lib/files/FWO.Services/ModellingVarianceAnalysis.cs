using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Data.Workflow;

namespace FWO.Services
{
    /// <summary>
	/// Variance Analysis Class
	/// </summary>
    public partial class ModellingVarianceAnalysis(ApiConnection apiConnection, ExtStateHandler extStateHandler,
            UserConfig userConfig, FwoOwner owner, Action<Exception?, string, string, bool> displayMessageInUi)
    {
        private readonly ModellingNamingConvention namingConvention = System.Text.Json.JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
        private readonly ModellingAppZoneHandler AppZoneHandler = new(apiConnection, userConfig, owner, displayMessageInUi);
        private AppServerComparer appServerComparer = new(new());
        private List<Management> RelevantManagements = [];

        private List<WfReqTask> TaskList = [];
        private List<WfReqTask> AccessTaskList = [];
        private List<WfReqTask> DeleteTasksList = [];
        private int taskNumber = 0;
        private List<WfReqElement> elements = [];

        private ModellingVarianceResult varianceResult = new();
        
        private Dictionary<int, List<Rule>> allModelledRules = [];

        private readonly Dictionary<int, List<ModellingAppRole>> allExistingAppRoles = [];
        private readonly Dictionary<int, List<ModellingAppServer>> allExistingAppServers = [];
        private readonly Dictionary<int, List<ModellingAppServer>> alreadyCreatedAppServers = [];


        public async Task<ModellingVarianceResult> AnalyseRulesVsModelledConnections(List<ModellingConnection> connections, ModellingFilter modellingFilter)
        {
            await InitManagements();
            varianceResult = new() { Managements = RelevantManagements };
            if(await GetModelledRulesProductionState(modellingFilter))
            {
                foreach(var conn in connections.Where(c => !c.IsRequested).OrderBy(c => c.Id))
                {
                    AnalyseRules(conn);
                }
            }
            return varianceResult;
        }

        public async Task<List<WfReqTask>> AnalyseModelledConnectionsForRequest(List<ModellingConnection> connections)
        {
            // later: get rules + compare, bundle requests
            appServerComparer = new (namingConvention);
            await InitManagements();
            await GetNwObjectsProductionState();

            TaskList = [];
            AccessTaskList = [];
            DeleteTasksList = [];
            foreach (Management mgt in RelevantManagements)
            {
                await AnalyseAppZone(mgt);
                foreach(var conn in connections.Where(c => !c.IsRequested).OrderBy(c => c.Id))
                {
                    elements = [];
                    AnalyseNetworkAreas(conn);
                    AnalyseAppRoles(conn, mgt);
                    AnalyseAppServers(conn);
                    AnalyseServiceGroups(conn, mgt);
                    AnalyseServices(conn);
                    if (elements.Count > 0)
                    {
                        Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.ConnId, conn.Id.ToString() } };
                        AccessTaskList.Add(new()
                        {
                            Title = (conn.IsCommonService ? userConfig.GetText("new_common_service") : userConfig.GetText("new_connection")) + ": " + conn.Name ?? "",
                            TaskType = WfTaskType.access.ToString(),
                            ManagementId = mgt.Id,
                            OnManagement = mgt,
                            Elements = elements,
                            RuleAction = 1,  // Todo ??
                            Tracking = 1,  // Todo ??
                            AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo),
                            Comments = [new() { Comment = new() { CommentText = ConstructComment(conn) } }]
                        });
                    }
                }
            }
            TaskList.AddRange(AccessTaskList);
            TaskList.AddRange(DeleteTasksList);
            taskNumber = 1;
            foreach (WfReqTask task in TaskList)
            {
                task.TaskNumber = taskNumber++;
                task.Owners = [new() { Owner = owner }];
                task.StateId = extStateHandler.GetInternalStateId(ExtStates.ExtReqInitialized) ?? 0;
            }
            return TaskList;
        }

        private string ConstructComment(ModellingConnection conn)
        {
            string comment = userConfig.ModModelledMarker + conn.Id.ToString();
            if(conn.IsCommonService)
            {
                comment += ", ComSvc";
            }
            if(conn.ExtraConfigs.Count > 0 || conn.ExtraConfigsFromInterface.Count > 0)
            {
                comment += ", " + userConfig.GetText("impl_instructions") + ": " + 
                    string.Join(", ", conn.ExtraConfigs.ConvertAll(x => x.Display()).Concat(conn.ExtraConfigsFromInterface.ConvertAll(x => x.Display())));
            }
            return comment;
        }
    }
}
