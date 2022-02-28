using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Timers;
using System.Text.Json;
using FWO.Middleware.RequestParameters;

namespace FWO.Middleware.Server
{
    public class DailyCheckScheduler
    {
        private readonly APIConnection apiConnection;
        private ConfigDbAccess config;
        private int DailyCheckSleepTime = 86400000; // 24 hours in milliseconds
        private string DailyCheckStartAt = DateTime.Now.TimeOfDay.ToString();
        private readonly ApiSubscription<List<ConfigItem>> configChangeSubscription;

        private System.Timers.Timer DailyCheckScheduleTimer = new();
        private System.Timers.Timer DailyCheckTimer = new();

        private List<Alert> openAlerts = new List<Alert>();

    
        public DailyCheckScheduler(APIConnection apiConnection)
        {
            this.apiConnection = apiConnection;
    
            config = new ConfigDbAccess(apiConnection);
            try
            {
                DailyCheckStartAt = config.Get<string>(GlobalConfig.kDailyCheckStartAt);
            }
            catch (KeyNotFoundException) {}
            
            configChangeSubscription = apiConnection.GetSubscription<List<ConfigItem>>(ApiExceptionHandler, OnConfigUpdate, ConfigQueries.subscribeDailyCheckConfigChanges);

            startDailyCheckScheduleTimer();
        }

        public void startDailyCheckScheduleTimer()
        {
            DateTime startTime = DateTime.Now;
            try
            {
                startTime = Convert.ToDateTime(DailyCheckStartAt);
                if(startTime < DateTime.Now)
                {
                    startTime = startTime.AddDays(1);
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("DailyCheck scheduler", "Could not calculate start time.", exception);
            }
            TimeSpan interval = startTime - DateTime.Now;
        
            DailyCheckScheduleTimer = new();
            DailyCheckScheduleTimer.Elapsed += DailyCheck;
            DailyCheckScheduleTimer.Elapsed += StartDailyCheckTimer;
            DailyCheckScheduleTimer.Interval = interval.TotalMilliseconds;
            DailyCheckScheduleTimer.AutoReset = false;
            DailyCheckScheduleTimer.Start();
            Log.WriteDebug("DailyCheck scheduler", "DailyCheckScheduleTimer started.");
        }

        private void StartDailyCheckTimer(object? _, ElapsedEventArgs __)
        {
            DailyCheckTimer.Stop();
            DailyCheckTimer = new();
            DailyCheckTimer.Elapsed += DailyCheck;
            DailyCheckTimer.Interval = DailyCheckSleepTime;
            DailyCheckTimer.AutoReset = true;
            DailyCheckTimer.Start();
            Log.WriteDebug("DailyCheck scheduler", "DailyCheckTimer started.");
        }

        private void OnConfigUpdate(List<ConfigItem> configItems)
        {
            foreach (ConfigItem configItem in configItems)
            {
                if(configItem.Key == GlobalConfig.kDailyCheckStartAt && configItem.Value != null && configItem.Value != "")
                {
                    DailyCheckStartAt = configItem.Value;
                }
            }
            DailyCheckTimer.Interval = DailyCheckSleepTime;
            DailyCheckScheduleTimer.Stop();
            startDailyCheckScheduleTimer();
        }

        private void ApiExceptionHandler(Exception exception)
        {
            Log.WriteError("DailyCheck scheduler", "Api subscription lead to exception. Retry subscription.", exception);
        }

        private async void DailyCheck(object? _, ElapsedEventArgs __)
        {
            try
            {
                openAlerts = await apiConnection.SendQueryAsync<List<Alert>>(MonitorQueries.getOpenAlerts);
                await CheckDemoData();
                await CheckImports();
            }
            catch(Exception exc)
            {
                Log.WriteError("DailyCheck", $"Ran into exception: ", exc);
                await AddDailyCheckLogEntry(1, "Scheduled Daily Check", $"Ran into exception: " + exc.Message);
            }
        }

        private async Task CheckDemoData()
        {
            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(FWO.ApiClient.Queries.DeviceQueries.getManagementsDetails);
            bool sampleManagementExisting = false;
            foreach (var management in managements)
            {
                if (management.Name.EndsWith("_demo"))
                {
                    sampleManagementExisting = true;
                }
            }

            List<UiUser> users = await apiConnection.SendQueryAsync<List<UiUser>>(FWO.ApiClient.Queries.AuthQueries.getUsers);
            bool sampleUserExisting = false;
            foreach (var user in users)
            {
                if (user.Name.EndsWith("_demo"))
                {
                    sampleUserExisting = true;
                }
            }

            List<Tenant> tenants = await apiConnection.SendQueryAsync<List<Tenant>>(FWO.ApiClient.Queries.AuthQueries.getTenants);
            bool sampleTenantExisting = false;
            foreach (var tenant in tenants)
            {
                if (tenant.Name.EndsWith("_demo"))
                {
                    sampleTenantExisting = true;
                }
            }

            bool sampleGroupExisting = false;
            List<Ldap> connectedLdaps = apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            foreach (Ldap currentLdap in connectedLdaps)
            {
                if (currentLdap.IsInternal() && currentLdap.HasGroupHandling())
                {
                    List<GroupGetReturnParameters> groups = currentLdap.GetAllInternalGroups();
                    foreach (var ldapUserGroup in groups)
                    {
                        if ((new DistName(ldapUserGroup.GroupDn)).Group.EndsWith("_demo"))
                        {
                            sampleGroupExisting = true;
                        }
                    }
                }
            }

            string description = "";
            if(sampleManagementExisting || sampleUserExisting || sampleTenantExisting || sampleGroupExisting)
            {
                description = $"Sample data found in: {(sampleManagementExisting ? "Managements" : "")}"+
                                                        $"{(sampleUserExisting ? " Users" : "")}"+
                                                        $"{(sampleTenantExisting ? " Tenants" : "")}"+
                                                        $"{(sampleGroupExisting ? " Groups" : "")}";
                await setAlert("daily check", AlertCode.SampleDataExisting, "Sample Data", description);
            }
            await AddDailyCheckLogEntry(0, "Scheduled Daily Sample Data Check", (description != "" ? description : "no sample data found"));
        }

        private async Task CheckImports()
        {
            List<ImportStatus> importStati = await apiConnection.SendQueryAsync<List<ImportStatus>>(FWO.ApiClient.Queries.DeviceQueries.getImportStatus);
            int importIssues = 0;
            string jsonData = "";
            foreach(ImportStatus imp in importStati.Where(x => !x.ImportDisabled))
            {
                if (imp.LastIncompleteImport != null && imp.LastIncompleteImport.Length > 0) // import running
                {
                    if (imp.LastIncompleteImport[0].StartTime < DateTime.Now.AddHours(-4))  // too long
                    {
                        jsonData = JsonSerializer.Serialize(imp.LastIncompleteImport);
                        await setAlert("daily check", AlertCode.ImportRunningTooLong, "Import", "Import running too long", imp.MgmId, jsonData);
                        importIssues++;
                    }
                }
                else if (imp.LastImport == null || imp.LastImport.Length == 0) // no import at all
                {
                    jsonData = JsonSerializer.Serialize(imp);
                    await setAlert("daily check", AlertCode.NoImport, "Import", "No Import for active management", imp.MgmId, jsonData);
                    importIssues++;
                }
                else if (imp.LastSuccessfulImport != null && imp.LastSuccessfulImport.Length > 0 && imp.LastSuccessfulImport[0].StartTime < DateTime.Now.AddHours(-12)) // too long ago
                {
                    jsonData = JsonSerializer.Serialize(imp);
                    await setAlert("daily check", AlertCode.SuccessfulImportOverdue, "Import", "Last successful import too long ago", imp.MgmId, jsonData);
                    importIssues++;
                }
            }
            await AddDailyCheckLogEntry(0, "Scheduled Daily Importer Check", (importIssues != 0 ? $"found {importIssues} import issues" : "no import issues found"));
        }

        public async Task setAlert(string source, AlertCode alertCode, string title, string description, int? mgmtId = null, string? JsonData = null, int? devId = null)
        {
            try
            {
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
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(MonitorQueries.addAlert, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    // Acknowledge older alert for same problem
                    Alert? existingAlert = openAlerts.FirstOrDefault(x => x.AlertCode == alertCode && x.ManagementId == mgmtId);
                    if(existingAlert != null)
                    {
                        await AcknowledgeAlert(existingAlert.Id);
                    }
                }
                else
                {
                    Log.WriteError("Write Alert", "Log could not be written to database");
                }
            }
            catch(Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for Daily Check: ", exc);
            }
        }

        public async Task AcknowledgeAlert(long alertId)
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
                Log.WriteError("Acknowledge Alert", $"Could not acknowledge alert for Daily Check: ", exception);
            }
        }

        public async Task AddDailyCheckLogEntry(int severity, string cause, string description)
        {
            try
            {
                var Variables = new
                {
                    source = "DailyCheck",
                    discoverUser = 0,
                    severity = severity,
                    suspectedCause = cause,
                    description = description,
                    mgmId = (int?)null,
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
            catch(Exception exc)
            {
                Log.WriteError("Write Log", $"Could not write log: ", exc);
            }
        }
    }
}
