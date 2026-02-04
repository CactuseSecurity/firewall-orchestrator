using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Logging;
using System.Linq;
using System.Text.Json;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Shared helper functions for Quartz jobs (logging and alerts).
    /// </summary>
    internal static class SchedulerJobHelper
    {
        internal struct AdditionalAlertData
        {
            public int? MgmtId { get; set; }
            public object? JsonData { get; set; }
            public int? DevId { get; set; }
            public long? RefAlertId { get; set; }
        }

        internal static async Task LogErrorsWithAlert(ApiConnection apiConnection, GlobalConfig globalConfig, int severity, string title, string source, AlertCode alertCode, Exception exc)
        {
            try
            {
                Log.WriteError(title, "Ran into exception: ", exc);
                string titletext = $"Error encountered while trying {title}";
                await AddLogEntry(apiConnection, globalConfig, severity, title, globalConfig.GetText("ran_into_exception") + exc.Message, source);
                await SetAlert(apiConnection, title, titletext, source, alertCode, new AdditionalAlertData());
            }
            catch (Exception exception)
            {
                Log.WriteError(title, "something went really wrong", exception);
            }
        }

        internal static async Task AddLogEntry(ApiConnection apiConnection, GlobalConfig globalConfig, int severity, string cause, string description, string source, int? mgmtId = null)
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
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(MonitorQueries.addLogEntry, Variables)).ReturnIds;
                if (returnIds == null)
                {
                    Log.WriteError("Write Log", "Log could not be written to database");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Write Log", "Could not write log: ", exc);
            }
        }

        internal static async Task<long?> SetAlert(ApiConnection apiConnection, string title, string description, string source, AlertCode alertCode,
            AdditionalAlertData additionalAlertData, bool compareDesc = false)
        {
            long? alertId = null;
            try
            {
                List<Alert> openAlerts = await apiConnection.SendQueryAsync<List<Alert>>(MonitorQueries.getOpenAlerts);
                var Variables = new
                {
                    source = source,
                    userId = 0,
                    title = title,
                    description = description,
                    mgmId = additionalAlertData.MgmtId,
                    devId = additionalAlertData.DevId,
                    alertCode = (int)alertCode,
                    jsonData = additionalAlertData.JsonData,
                    refAlert = additionalAlertData.RefAlertId
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(MonitorQueries.addAlert, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    alertId = returnIds[0].NewIdLong;
                    Alert? existingAlert = openAlerts.FirstOrDefault(x => x.AlertCode == alertCode &&
                        (x.ManagementId == additionalAlertData.MgmtId || (x.ManagementId == null && additionalAlertData.MgmtId == null))
                        && (!compareDesc || x.Description == description));
                    if (existingAlert != null)
                    {
                        await AcknowledgeAlert(apiConnection, existingAlert.Id);
                    }
                }
                else
                {
                    Log.WriteError("Write Alert", "Log could not be written to database");
                }
                LogAlert(title, description, source, alertCode, additionalAlertData.MgmtId, additionalAlertData.JsonData, additionalAlertData.DevId);
            }
            catch (Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for {source}: ", exc);
                LogAlert(title, description, source, alertCode, additionalAlertData.MgmtId, additionalAlertData.JsonData, additionalAlertData.DevId);
            }
            return alertId;
        }

        private static async Task AcknowledgeAlert(ApiConnection apiConnection, long alertId)
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

        private static void LogAlert(string title, string description, string source, AlertCode alertCode, int? mgmtId, object? JsonData, int? devId)
        {
            string? mgmtIdString = mgmtId?.ToString() ?? "";
            string? devIdString = devId?.ToString() ?? "";
            string jsonString = JsonData != null ? JsonSerializer.Serialize(JsonData) : "";
            Log.WriteAlert($"source: \"{source}\"", $"userId: \"0\", title: \"{title}\", description: \"{description}\", mgmId: \"{mgmtIdString}\", devId: \"{devIdString}\", jsonData: \"{jsonString}\", alertCode: \"{alertCode}\"");
        }
    }
}
