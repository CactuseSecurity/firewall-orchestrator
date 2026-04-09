using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.DeviceAutoDiscovery;
using FWO.Logging;
using FWO.Services;
using Quartz;
using System.Linq;
using FWO.Services.Logging;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Quartz Job for autodiscovery
    /// </summary>
    [DisallowConcurrentExecution]
    public class AutoDiscoverJob : IJob
    {
        private const string LogMessageTitle = "Autodiscovery";
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;
        private long? lastMgmtAlertId;

        /// <summary>
        /// Creates a new autodiscovery job.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        public AutoDiscoverJob(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <inheritdoc />
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
                        await AlertHelper.AddLogEntry(apiConnection, 0, globalConfig.GetText("scheduled_autodiscovery"),
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
                        await AlertHelper.AddLogEntry(apiConnection, 1, globalConfig.GetText("scheduled_autodiscovery"),
                            $"Ran into exception while handling management {superManagement.Name} (id: {superManagement.Id}): " + excMgm.Message,
                            GlobalConst.kAutodiscovery, superManagement.Id);
                    }
                }
            }
            catch (Exception exc)
            {
                await AlertHelper.LogErrorsWithAlert(apiConnection, globalConfig, 1, LogMessageTitle, GlobalConst.kAutodiscovery, AlertCode.Autodiscovery, exc);
            }
        }

        private async Task<long?> SetAlert(ActionItem action)
        {
            string title = "Supermanagement: " + action.Supermanager;
            lastMgmtAlertId = await AlertHelper.SetAlert(apiConnection, title, action.ActionType ?? "", GlobalConst.kAutodiscovery, AlertCode.Autodiscovery,
                new AlertHelper.AdditionalAlertData
                {
                    MgmtId = action.ManagementId,
                    JsonData = action.JsonData?.ToString(),
                    DevId = action.DeviceId,
                    RefAlertId = action.RefAlertId,
                    CompareDesc = true
                });
            if (!AutodiscoveryLogMapper.TryMapPromptAction(action, out AutodiscoveryLogMapper.PromptLogData? logData))
            {
                Log.WriteWarning("Logging", $"Unmapped autodiscovery action type: {action.ActionType}");
                return lastMgmtAlertId;
            }
            if (logData != null)
            {
                await PromptLogHelper.LogPrompt(
                    promptEvent: PromptLogEvent.Created,
                    obj: logData.Object,
                    operation: logData.Operation,
                    userId: "AutodiscoveryJob",
                    dateTime: DateTime.Now,
                    origin: ChangeLogOrigin.Autodiscovery,
                    logData.Fields);
            }
            return lastMgmtAlertId;
        }
    }
}
