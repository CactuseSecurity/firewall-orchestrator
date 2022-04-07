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
        private GlobalConfig globalConfig;
        private int DailyCheckSleepTime = 86400000; // 24 hours in milliseconds

        private System.Timers.Timer DailyCheckScheduleTimer = new();
        private System.Timers.Timer DailyCheckTimer = new();

        private List<Alert> openAlerts = new List<Alert>();

        public static async Task<DailyCheckScheduler> CreateAsync(APIConnection apiConnection)
        {
            GlobalConfig config = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new DailyCheckScheduler(apiConnection, config);
        }

        private DailyCheckScheduler(APIConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            globalConfig.OnChange += GlobalConfig_OnChange;

            startDailyCheckScheduleTimer();
        }

        private void GlobalConfig_OnChange(Config.Api.Config globalConfig, ConfigItem[] _)
        {
            DailyCheckTimer.Interval = DailyCheckSleepTime;
            DailyCheckScheduleTimer.Stop();
            startDailyCheckScheduleTimer();
        }

        public void startDailyCheckScheduleTimer()
        {
            DateTime? startTime = null;
            try
            {
                startTime = DateTime.Now.Date.Add(globalConfig.DailyCheckStartAt.TimeOfDay);
                if(startTime < DateTime.Now)
                {
                    startTime = ((DateTime)startTime).AddDays(1);
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("DailyCheck scheduler", "Could not calculate start time.", exception);
            }
            TimeSpan interval = (startTime ?? DateTime.Now.AddMilliseconds(1)) - DateTime.Now;
        
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
                await AddDailyCheckLogEntry(2, globalConfig.GetText("daily_checks"), globalConfig.GetText("ran_into_exception") + exc.Message);
                await setAlert(GlobalConfig.kDailyCheck, AlertCode.DailyCheckError, globalConfig.GetText("daily_checks"), globalConfig.GetText("ran_into_exception") + exc.Message);
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
                description = globalConfig.GetText("sample_data_found_in") + (sampleManagementExisting ? globalConfig.GetText("managements") + " " : "") +
                                                        (sampleUserExisting ? globalConfig.GetText("users") + " " : "") +
                                                        (sampleTenantExisting ? globalConfig.GetText("tenants") + " " : "") +
                                                        (sampleGroupExisting ? globalConfig.GetText("groups") : "");
                await setAlert(GlobalConfig.kDailyCheck, AlertCode.SampleDataExisting, globalConfig.GetText("sample_data"), description);
            }
            await AddDailyCheckLogEntry((description != "" ? 1 : 0), globalConfig.GetText("daily_sample_data_check"), (description != "" ? description : globalConfig.GetText("no_sample_data_found")));
        }

        private async Task CheckImports()
        {
            List<ImportStatus> importStati = await apiConnection.SendQueryAsync<List<ImportStatus>>(FWO.ApiClient.Queries.DeviceQueries.getImportStatus);
            int importIssues = 0;
            object jsonData;
            foreach(ImportStatus imp in importStati.Where(x => !x.ImportDisabled))
            {
                if (imp.LastIncompleteImport != null && imp.LastIncompleteImport.Length > 0) // import running
                {
                    if (imp.LastIncompleteImport[0].StartTime < DateTime.Now.AddHours(-globalConfig.MaxImportDuration))  // too long
                    {
                        jsonData = imp.LastIncompleteImport;
                        await setAlert(GlobalConfig.kDailyCheck, AlertCode.ImportRunningTooLong, globalConfig.GetText("import"), globalConfig.GetText("E7011"), imp.MgmId, jsonData);
                        importIssues++;
                    }
                }
                else if (imp.LastImport == null || imp.LastImport.Length == 0) // no import at all
                {
                    jsonData = imp;
                    await setAlert(GlobalConfig.kDailyCheck, AlertCode.NoImport, globalConfig.GetText("import"), globalConfig.GetText("E7012"), imp.MgmId, jsonData);
                    importIssues++;
                }
                else if (imp.LastImportAttempt != null && imp.LastImportAttempt < DateTime.Now.AddHours(-globalConfig.MaxImportInterval)) // too long ago
                {
                    jsonData = imp;
                    await setAlert(GlobalConfig.kDailyCheck, AlertCode.SuccessfulImportOverdue, globalConfig.GetText("import"), globalConfig.GetText("E7013"), imp.MgmId, jsonData);
                    importIssues++;
                }
            }
            await AddDailyCheckLogEntry((importIssues != 0 ? 1 : 0), globalConfig.GetText("daily_importer_check"), (importIssues != 0 ? importIssues + globalConfig.GetText("import_issues_found") : globalConfig.GetText("no_import_issues_found")));
        }

        public async Task setAlert(string source, AlertCode alertCode, string title, string description, int? mgmtId = null, object? JsonData = null, int? devId = null)
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
                string? mgmtIdString = ""; 
                if (mgmtId != null)
                {
                    mgmtIdString = mgmtId.ToString();
                }
                string? devIdString = ""; 
                if (devId != null)
                {
                    devIdString = devId.ToString();
                }
                string jsonString = ""; 
                if (JsonData != null)
                    jsonString = JsonSerializer.Serialize(JsonData);
                Log.WriteAlert ($"source: \"{source}\"", 
                    $"userId: \"0\", title: \"{title}\", description: \"{description}\", " +
                    $"mgmId: \"{mgmtIdString}\", devId: \"{devIdString}\", jsonData: \"{jsonString}\", alertCode: \"{alertCode.ToString()}\"");
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
                    source = GlobalConfig.kDailyCheck,
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
                Log.WriteError("Write Log", $"Could not write daily check log to db: ", exc);
            }
        }
    }
}
