using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FWO.Services
{
    public static class AlertHelper
    {
        public struct AdditionalAlertData
        {
            public int? MgmtId { get; set; }
            public object? JsonData { get; set; }
            public int? DevId { get; set; }
            public long? RefAlertId { get; set; }
        }

        public static async Task LogErrorsWithAlert(ApiConnection apiConnection, GlobalConfig globalConfig, int severity, string title, string source, AlertCode alertCode, Exception exc)
        {
            try
            {
                Log.WriteError(title, "Ran into exception: ", exc);
                string titletext = $"Error encountered while trying {title}";
                await AddLogEntry(apiConnection, severity, title, globalConfig.GetText("ran_into_exception") + exc.Message, source);
                await SetAlert(apiConnection, title, titletext, source, alertCode, new AdditionalAlertData());
            }
            catch (Exception exception)
            {
                Log.WriteError(title, "something went really wrong", exception);
            }
        }

        public static async Task AddLogEntry(ApiConnection apiConnection, int severity, string cause, string description, string source, int? mgmtId = null)
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

        public static Task<long?> SetAlert(ApiConnection apiConnection, string title, string description, string source, AlertCode alertCode,
            AdditionalAlertData additionalAlertData, bool compareDesc = false, bool compareTitle = false, int userId = 0)
        {
            return SetAlert(apiConnection, title, description, source, alertCode, additionalAlertData.MgmtId, additionalAlertData.JsonData,
                additionalAlertData.DevId, additionalAlertData.RefAlertId, compareDesc, compareTitle, userId);
        }

        public static async Task<long?> SetAlert(ApiConnection apiConnection, string title, string description, string source, AlertCode alertCode,
            int? mgmtId = null, object? jsonData = null, int? devId = null, long? refAlertId = null, bool compareDesc = false, bool compareTitle = false, int userId = 0)
        {
            long? alertId = null;
            try
            {
                List<Alert> openAlerts = await apiConnection.SendQueryAsync<List<Alert>>(MonitorQueries.getOpenAlerts);
                var Variables = new
                {
                    source = source,
                    userId = userId,
                    title = title,
                    description = description,
                    mgmId = mgmtId,
                    devId = devId,
                    alertCode = (int)alertCode,
                    jsonData = jsonData,
                    refAlert = refAlertId
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(MonitorQueries.addAlert, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    // Acknowledge older alert for same problem
                    alertId = returnIds[0].NewIdLong;
                    Alert? existingAlert = openAlerts.FirstOrDefault(x => x.AlertCode == alertCode
                        && (x.ManagementId == mgmtId || (x.ManagementId == null && mgmtId == null))
                        && (userId == 0 || x.UserId == userId)
                        && (!compareDesc || x.Description == description)
                        && (!compareTitle || x.Title == title));
                    if (existingAlert != null)
                    {
                        await AcknowledgeAlert(apiConnection, existingAlert.Id, userId);
                    }
                }
                else
                {
                    Log.WriteError("Write Alert", "Log could not be written to database");
                }
                LogAlert(title, description, source, alertCode, mgmtId, jsonData, devId);
            }
            catch (Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for {source}: ", exc);
                LogAlert(title, description, source, alertCode, mgmtId, jsonData, devId);
            }
            return alertId;
        }

        public static async Task AcknowledgeAlert(ApiConnection apiConnection, long alertId, int ackUser = 0)
        {
            try
            {
                var Variables = new
                {
                    id = alertId,
                    ackUser = ackUser,
                    ackTime = DateTime.Now
                };
                await apiConnection.SendQueryAsync<ReturnId>(MonitorQueries.acknowledgeAlert, Variables);
            }
            catch (Exception exception)
            {
                Log.WriteError("Acknowledge Alert", $"Could not acknowledge alert for {alertId}: ", exception);
            }
        }

        private static void LogAlert(string title, string description, string source, AlertCode alertCode, int? mgmtId, object? jsonData, int? devId)
        {
            string? mgmtIdString = mgmtId?.ToString() ?? "";
            string? devIdString = devId?.ToString() ?? "";
            string jsonString = jsonData != null ? JsonSerializer.Serialize(jsonData) : "";
            Log.WriteAlert($"source: \"{source}\"", $"userId: \"0\", title: \"{title}\", description: \"{description}\", " +
                $"mgmId: \"{mgmtIdString}\", devId: \"{devIdString}\", jsonData: \"{jsonString}\", alertCode: \"{alertCode}\"");
        }
    }
}
