using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.DeviceAutoDiscovery;
using FWO.Logging;
using Quartz;
using System.Linq;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Quartz Job for autodiscovery
    /// </summary>
    public class AutoDiscoverJob : IJob
    {
        private const string LogMessageTitle = "Autodiscovery";
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;
        private long? lastMgmtAlertId;

        public AutoDiscoverJob(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementsDetails);
                foreach (Management superManagement in managements.Where(x => x.DeviceType.CanBeSupermanager() || x.DeviceType.CanBeAutodiscovered(x)))
                {
                    try
                    {
                        AutoDiscoveryBase autodiscovery = new(superManagement, apiConnection);

                        List<Management> diffList = await autodiscovery.Run();
                        List<ActionItem> actions = autodiscovery.ConvertToActions(diffList);

                        int changeCounter = 0;

                        foreach (ActionItem action in actions)
                        {
                            if (action.ActionType == ActionCode.AddGatewayToNewManagement.ToString())
                            {
                                action.RefAlertId = lastMgmtAlertId;
                            }
                            action.AlertId = await SetAlert(action);
                            changeCounter++;
                        }
                        await SchedulerJobHelper.AddLogEntry(apiConnection, globalConfig, 0, globalConfig.GetText("scheduled_autodiscovery"),
                            changeCounter > 0 ? changeCounter + globalConfig.GetText("changes_found") : globalConfig.GetText("found_no_changes"),
                            GlobalConst.kAutodiscovery, superManagement.Id);
                    }
                    catch (Exception excMgm)
                    {
                        Log.WriteError(LogMessageTitle, $"Ran into exception while auto-discovering management {superManagement.Name} (id: {superManagement.Id}) ", excMgm);
                        ActionItem actionException = new()
                        {
                            Number = 0,
                            ActionType = ActionCode.WaitForTempLoginFailureToPass.ToString(),
                            ManagementId = superManagement.Id,
                            Supermanager = superManagement.Name,
                            JsonData = excMgm.Message
                        };
                        await SetAlert(actionException);
                        await SchedulerJobHelper.AddLogEntry(apiConnection, globalConfig, 1, globalConfig.GetText("scheduled_autodiscovery"),
                            $"Ran into exception while handling management {superManagement.Name} (id: {superManagement.Id}): " + excMgm.Message,
                            GlobalConst.kAutodiscovery, superManagement.Id);
                    }
                }
            }
            catch (Exception exc)
            {
                await SchedulerJobHelper.LogErrorsWithAlert(apiConnection, globalConfig, 1, LogMessageTitle, GlobalConst.kAutodiscovery, AlertCode.Autodiscovery, exc);
            }
        }

        private async Task<long?> SetAlert(ActionItem action)
        {
            string title = "Supermanagement: " + action.Supermanager;
            lastMgmtAlertId = await SchedulerJobHelper.SetAlert(apiConnection, title, action.ActionType ?? "", GlobalConst.kAutodiscovery, AlertCode.Autodiscovery,
                new SchedulerJobHelper.AdditionalAlertData { MgmtId = action.ManagementId, JsonData = action.JsonData?.ToString(), DevId = action.DeviceId, RefAlertId = action.RefAlertId }, true);
            return lastMgmtAlertId;
        }
    }
}
