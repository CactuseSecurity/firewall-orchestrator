using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Text.Json;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the scheduler for the import change notifications
	/// </summary>
    public class SchedulerBase
    {
		/// <summary>
		/// API connection
		/// </summary>
        protected readonly ApiConnection apiConnection;

		/// <summary>
		/// Global config
		/// </summary>
        protected GlobalConfig globalConfig;

        private List<Alert> openAlerts = new List<Alert>();

    
		/// <summary>
		/// Constructor starting the Schedule timer
		/// </summary>
        protected SchedulerBase(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            globalConfig.OnChange += GlobalConfig_OnChange;
            StartScheduleTimer();
        }

		/// <summary>
		/// set scheduling timer from config values, to be overwritten for specific scheduler
		/// </summary>
        protected virtual void GlobalConfig_OnChange(Config.Api.Config globalConfig, ConfigItem[] _)
        {}

		/// <summary>
		/// start the scheduling timer, to be overwritten for specific scheduler
		/// </summary>
        protected virtual void StartScheduleTimer()
        {}

		/// <summary>
		/// set an alert in error case with 
		/// </summary>
        protected async Task<long?> SetAlert(string title, string description, string source, AlertCode alertCode,
            int? mgmtId = null, object? JsonData = null, int? devId = null, long? refAlertId = null, bool compareDesc = false)
        {
            long? alertId = null;
            try
            {
                openAlerts = await apiConnection.SendQueryAsync<List<Alert>>(MonitorQueries.getOpenAlerts);
                var Variables = new
                {
                    source = source,
                    userId = 0,
                    title = title,
                    description = description,
                    mgmId = mgmtId,
                    devId = devId,
                    alertCode = (int)alertCode,
                    jsonData = JsonData,
                    refAlert = refAlertId
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(MonitorQueries.addAlert, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    alertId = returnIds[0].NewId;
                    // Acknowledge older alert for same problem
                    Alert? existingAlert = openAlerts.FirstOrDefault(x => x.AlertCode == alertCode && x.ManagementId == mgmtId
                        && compareDesc ? x.Description == description : true);
                    if(existingAlert != null)
                    {
                        await AcknowledgeAlert(existingAlert.Id);
                    }
                }
                else
                {
                    Log.WriteError("Write Alert", "Log could not be written to database");
                }
                LogAlert(title, description, source, alertCode, mgmtId, JsonData, devId);
            }
            catch(Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for {source}: ", exc);
            }
            return alertId;
        }

        private void LogAlert(string title, string description, string source, AlertCode alertCode, int? mgmtId, object? JsonData, int? devId)
        {
            string? mgmtIdString = mgmtId?.ToString() ?? ""; 
            string? devIdString = devId?.ToString() ?? ""; 
            string jsonString = JsonData != null ? JsonSerializer.Serialize(JsonData) : ""; 
            Log.WriteAlert ($"source: \"{source}\"", $"userId: \"0\", title: \"{title}\", description: \"{description}\", " +
                $"mgmId: \"{mgmtIdString}\", devId: \"{devIdString}\", jsonData: \"{jsonString}\", alertCode: \"{alertCode}\"");
        }

        private async Task AcknowledgeAlert(long alertId)
        {
            try
            {
                var Variables = new
                {
                    id = alertId,
                    ackUser = 0,
                    ackTime = DateTime.Now
                };
                await apiConnection.SendQueryAsync<ReturnId>(MonitorQueries.acknowledgeAlert, Variables);
            }
            catch (Exception exception)
            {
                Log.WriteError("Acknowledge Alert", $"Could not acknowledge alert for {alertId}: ", exception);
            }
        }

		/// <summary>
		/// Write Log to Database. Can be overwritten, if more than basic columns are to be filled
		/// </summary>
        protected virtual async Task AddLogEntry(int severity, string cause, string description, string source, int? mgmtId = null)
        {
            try
            {
                var Variables = new
                {
                    source = source,
                    discoverUser = 0,
                    severity = severity,
                    suspectedCause = cause,
                    description = description,
                    mgmId = mgmtId,
                    devId = (int?)null,
                    importId = (long?)null,
                    objectType = (string?)null,
                    objectName = (string?)null,
                    objectUid = (string?)null,
                    ruleUid = (string?)null,
                    ruleId = (long?)null
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(MonitorQueries.addLogEntry, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    Log.WriteError("Write Log", "Log could not be written to database");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Write Log", $"Could not write log: ", exc);
            }
        }
    }
}
