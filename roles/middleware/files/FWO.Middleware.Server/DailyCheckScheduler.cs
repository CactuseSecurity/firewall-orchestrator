using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Timers;
using FWO.Middleware.RequestParameters;
using FWO.Recert;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the scheduler for the daily checks
	/// </summary>
    public class DailyCheckScheduler : SchedulerBase
    {
        private int DailyCheckSleepTime = 86400000; // 24 hours in milliseconds

        private System.Timers.Timer DailyCheckScheduleTimer = new();
        private System.Timers.Timer DailyCheckTimer = new();


		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<DailyCheckScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig config = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new DailyCheckScheduler(apiConnection, config);
        }

        private DailyCheckScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeDailyCheckConfigChanges)
        {
            if(globalConfig.RecRefreshStartup)
            {
                #pragma warning disable CS4014
                RefreshRecert(); // no need to wait
                #pragma warning restore CS4014
            }
        }

		/// <summary>
		/// set scheduling timer from fixed value
		/// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            DailyCheckScheduleTimer.Stop();
            globalConfig.SubscriptionUpdateHandler(config.ToArray());
            DailyCheckTimer.Interval = DailyCheckSleepTime;
            StartScheduleTimer();
        }

		/// <summary>
		/// start the scheduling timer
		/// </summary>
        protected override void StartScheduleTimer()
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
                await CheckDemoData();
                await CheckImports();
                if(globalConfig.RecRefreshDaily)
                {
                    await RefreshRecert();
                }
                await CheckRecerts();
            }
            catch(Exception exc)
            {
                Log.WriteError("DailyCheck", $"Ran into exception: ", exc);
                await AddLogEntry(2, globalConfig.GetText("daily_checks"), globalConfig.GetText("ran_into_exception") + exc.Message, GlobalConst.kDailyCheck);
                await SetAlert(globalConfig.GetText("daily_checks"), globalConfig.GetText("ran_into_exception") + exc.Message,GlobalConst.kDailyCheck, AlertCode.DailyCheckError);
            }
        }

        private async Task RefreshRecert()
        {
            Log.WriteDebug("DailyCheck scheduler", "Refresh recert ownerships");
            await RecertRefresh.RecalcRecerts(apiConnection);
        }

        private async Task CheckRecerts()
        {
            if(globalConfig.RecCheckActive)
            {
                RecertCheck recertCheck = new RecertCheck(apiConnection, globalConfig);
                int emailsSent = await recertCheck.CheckRecertifications();
                await AddLogEntry(0, globalConfig.GetText("daily_recert_check"), emailsSent + globalConfig.GetText("emails_sent"), GlobalConst.kDailyCheck);
            }
        }

        private async Task CheckDemoData()
        {
            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementsDetails);
            bool sampleManagementExisting = false;
            foreach (var management in managements)
            {
                if (management.Name.EndsWith("_demo"))
                {
                    sampleManagementExisting = true;
                }
            }

            List<ImportCredential> credentials = await apiConnection.SendQueryAsync<List<ImportCredential>>(DeviceQueries.getCredentialsWithoutSecrets);
            bool sampleCredentialExisting = false;
            foreach (var credential in credentials)
            {
                if (credential.Name.EndsWith("_demo"))
                {
                    sampleCredentialExisting = true;
                }
            }

            List<UiUser> users = await apiConnection.SendQueryAsync<List<UiUser>>(AuthQueries.getUsers);
            bool sampleUserExisting = false;
            foreach (var user in users)
            {
                if (user.Name.EndsWith("_demo"))
                {
                    sampleUserExisting = true;
                }
            }

            List<Tenant> tenants = await apiConnection.SendQueryAsync<List<Tenant>>(AuthQueries.getTenants);
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

            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
            bool sampleOwnerExisting = false;
            foreach (var owner in owners)
            {
                if (owner.Name.EndsWith("_demo"))
                {
                    sampleOwnerExisting = true;
                }
            }

            string description = "";
            if(sampleManagementExisting || sampleCredentialExisting || sampleUserExisting || sampleTenantExisting || sampleGroupExisting || sampleOwnerExisting)
            {
                description = globalConfig.GetText("sample_data_found_in") + (sampleManagementExisting ? globalConfig.GetText("managements") + " " : "") +
                                                        (sampleCredentialExisting ? globalConfig.GetText("import_credential") + " " : "") +
                                                        (sampleUserExisting ? globalConfig.GetText("users") + " " : "") +
                                                        (sampleTenantExisting ? globalConfig.GetText("tenants") + " " : "") +
                                                        (sampleGroupExisting ? globalConfig.GetText("groups") + " " : "") +
                                                        (sampleOwnerExisting ? globalConfig.GetText("owners") : "");
                await SetAlert(globalConfig.GetText("sample_data"), description, GlobalConst.kDailyCheck, AlertCode.SampleDataExisting);
            }
            await AddLogEntry(description != "" ? 1 : 0, globalConfig.GetText("daily_sample_data_check"), description != "" ? description : globalConfig.GetText("no_sample_data_found"), GlobalConst.kDailyCheck);
        }

        private async Task CheckImports()
        {
            List<ImportStatus> importStati = await apiConnection.SendQueryAsync<List<ImportStatus>>(MonitorQueries.getImportStatus);
            int importIssues = 0;
            object jsonData;
            foreach(ImportStatus imp in importStati.Where(x => !x.ImportDisabled))
            {
                if (imp.LastIncompleteImport != null && imp.LastIncompleteImport.Length > 0) // import running
                {
                    if (imp.LastIncompleteImport[0].StartTime < DateTime.Now.AddHours(-globalConfig.MaxImportDuration))  // too long
                    {
                        jsonData = imp.LastIncompleteImport;
                        await SetAlert(globalConfig.GetText("import"), globalConfig.GetText("E7011"),GlobalConst.kDailyCheck, AlertCode.ImportRunningTooLong, imp.MgmId, jsonData);
                        importIssues++;
                    }
                }
                else if (imp.LastImport == null || imp.LastImport.Length == 0) // no import at all
                {
                    jsonData = imp;
                    await SetAlert(globalConfig.GetText("import"), globalConfig.GetText("E7012"), GlobalConst.kDailyCheck, AlertCode.NoImport, imp.MgmId, jsonData);
                    importIssues++;
                }
                else if (imp.LastImportAttempt != null && imp.LastImportAttempt < DateTime.Now.AddHours(-globalConfig.MaxImportInterval))
                // too long ago (not working for legacy devices as LastImportAttempt is not written)
                {
                    jsonData = imp;
                    await SetAlert(globalConfig.GetText("import"), globalConfig.GetText("E7013"), GlobalConst.kDailyCheck, AlertCode.SuccessfulImportOverdue, imp.MgmId, jsonData);
                    importIssues++;
                }
            }
            await AddLogEntry(importIssues != 0 ? 1 : 0, globalConfig.GetText("daily_importer_check"),
                importIssues != 0 ? importIssues + globalConfig.GetText("import_issues_found") : globalConfig.GetText("no_import_issues_found"), GlobalConst.kDailyCheck);
        }
    }
}
