using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Recert;
using Quartz;
using System.Linq;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Quartz Job for daily checks
    /// </summary>
    public class DailyCheckJob : IJob
    {
        private const string LogMessageTitle = "Daily Check";
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;

        /// <summary>
        /// Creates a new daily check job.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        public DailyCheckJob(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <inheritdoc />
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await CheckDemoData();
                await CheckImports();
                if (globalConfig.RecRefreshDaily)
                {
                    await RefreshRecert();
                }
                await CheckRecerts();
            }
            catch (Exception exc)
            {
                //await SchedulerJobHelper.LogErrorsWithAlert(apiConnection, globalConfig, 2, LogMessageTitle, GlobalConst.kDailyCheck, AlertCode.DailyCheckError, exc);
            }
        }

        private async Task RefreshRecert()
        {
            Log.WriteDebug(LogMessageTitle, "Refresh recert ownerships");
            await RecertRefresh.RecalcRecerts(apiConnection);
        }

        private async Task CheckRecerts()
        {
            if (globalConfig.RecCheckActive)
            {
                RecertCheck recertCheck = new(apiConnection, globalConfig);
                int emailsSent = await recertCheck.CheckRecertifications();
                Log.WriteDebug(LogMessageTitle, $"Recert Check: Sent {emailsSent} emails.");
                //await SchedulerJobHelper.AddLogEntry(apiConnection, globalConfig, 0, globalConfig.GetText("daily_recert_check"), emailsSent + globalConfig.GetText("emails_sent"), GlobalConst.kDailyCheck);
            }
        }

        private struct DemoDataFlags
        {
            public bool SampleManagementExisting;
            public bool SampleCredentialExisting;
            public bool SampleUserExisting;
            public bool SampleTenantExisting;
            public bool SampleGroupExisting;
            public bool SampleOwnerExisting;

            public readonly bool AnyFlagSet()
            {
                return SampleManagementExisting || SampleCredentialExisting || SampleUserExisting
                    || SampleTenantExisting || SampleGroupExisting || SampleOwnerExisting;
            }
        }

        private async Task CheckDemoData()
        {
            DemoDataFlags demoDataFlags = await CheckDemoDataExisting();

            if (demoDataFlags.AnyFlagSet())
            {
                string description = globalConfig.GetText("sample_data_found_in") + (demoDataFlags.SampleManagementExisting ? globalConfig.GetText("managements") + " " : "") +
                                                        (demoDataFlags.SampleCredentialExisting ? globalConfig.GetText("import_credential") + " " : "") +
                                                        (demoDataFlags.SampleUserExisting ? globalConfig.GetText("users") + " " : "") +
                                                        (demoDataFlags.SampleTenantExisting ? globalConfig.GetText("tenants") + " " : "") +
                                                        (demoDataFlags.SampleGroupExisting ? globalConfig.GetText("groups") + " " : "") +
                                                        (demoDataFlags.SampleOwnerExisting ? globalConfig.GetText("owners") : "");
                //await SchedulerJobHelper.SetAlert(apiConnection, globalConfig.GetText("sample_data"), description, GlobalConst.kDailyCheck, AlertCode.SampleDataExisting, new SchedulerJobHelper.AdditionalAlertData());
                //await SchedulerJobHelper.AddLogEntry(apiConnection, globalConfig, 1, globalConfig.GetText("daily_sample_data_check"), description, GlobalConst.kDailyCheck);
            }
            else
            {
                //await SchedulerJobHelper.AddLogEntry(apiConnection, globalConfig, 0, globalConfig.GetText("daily_sample_data_check"), globalConfig.GetText("no_sample_data_found"), GlobalConst.kDailyCheck);
            }
        }

        private async Task<DemoDataFlags> CheckDemoDataExisting()
        {
            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementsDetails);
            List<ImportCredential> credentials = await apiConnection.SendQueryAsync<List<ImportCredential>>(DeviceQueries.getCredentialsWithoutSecrets);
            List<UiUser> users = await apiConnection.SendQueryAsync<List<UiUser>>(AuthQueries.getUsers);
            List<Tenant> tenants = await apiConnection.SendQueryAsync<List<Tenant>>(AuthQueries.getTenants);
            bool sampleGroupExisting = false;
            List<Ldap> connectedLdaps = apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            foreach (Ldap currentLdap in connectedLdaps.Where(l => l.IsInternal() && l.HasGroupHandling()))
            {
                List<GroupGetReturnParameters> groups = await currentLdap.GetAllInternalGroups();
                sampleGroupExisting |= groups.Exists(g => new DistName(g.GroupDn).Group.EndsWith(GlobalConst.k_demo));
            }
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);

            return new()
            {
                SampleManagementExisting = managements.Exists(m => m.Name.EndsWith(GlobalConst.k_demo)),
                SampleCredentialExisting = credentials.Exists(c => c.Name.EndsWith(GlobalConst.k_demo)),
                SampleUserExisting = users.Exists(u => u.Name.EndsWith(GlobalConst.k_demo)),
                SampleTenantExisting = tenants.Exists(t => t.Name.EndsWith(GlobalConst.k_demo)),
                SampleGroupExisting = sampleGroupExisting,
                SampleOwnerExisting = owners.Exists(o => o.Name.EndsWith(GlobalConst.k_demo))
            };
        }

        private async Task CheckImports()
        {
            List<ImportStatus> importStati = await apiConnection.SendQueryAsync<List<ImportStatus>>(MonitorQueries.getImportStatus);
            int importIssues = 0;
            object jsonData;
            foreach (ImportStatus imp in importStati.Where(x => !x.ImportDisabled))
            {
                if (imp.LastIncompleteImport != null && imp.LastIncompleteImport.Length > 0)
                {
                    if (imp.LastIncompleteImport[0].StartTime < DateTime.Now.AddHours(-globalConfig.MaxImportDuration))
                    {
                        jsonData = imp.LastIncompleteImport;
                        //await SchedulerJobHelper.SetAlert(apiConnection, globalConfig.GetText("import"), globalConfig.GetText("E7011"), GlobalConst.kDailyCheck, AlertCode.ImportRunningTooLong, new SchedulerJobHelper.AdditionalAlertData { MgmtId = imp.MgmId, JsonData = jsonData });
                        importIssues++;
                    }
                }
                else if (imp.LastImport == null || imp.LastImport.Length == 0)
                {
                    jsonData = imp;
                    //await SchedulerJobHelper.SetAlert(apiConnection, globalConfig.GetText("import"), globalConfig.GetText("E7012"), GlobalConst.kDailyCheck, AlertCode.NoImport, new SchedulerJobHelper.AdditionalAlertData { MgmtId = imp.MgmId, JsonData = jsonData });
                    importIssues++;
                }
                else if (imp.LastImportAttempt != null && imp.LastImportAttempt < DateTime.Now.AddHours(-globalConfig.MaxImportInterval))
                {
                    jsonData = imp;
                    //await SchedulerJobHelper.SetAlert(apiConnection, globalConfig.GetText("import"), globalConfig.GetText("E7013"), GlobalConst.kDailyCheck, AlertCode.SuccessfulImportOverdue, new SchedulerJobHelper.AdditionalAlertData { MgmtId = imp.MgmId, JsonData = jsonData });
                    importIssues++;
                }
            }
            //await SchedulerJobHelper.AddLogEntry(apiConnection, globalConfig, importIssues != 0 ? 1 : 0, globalConfig.GetText("daily_importer_check"),
            //importIssues != 0 ? importIssues + globalConfig.GetText("import_issues_found") : globalConfig.GetText("no_import_issues_found"), GlobalConst.kDailyCheck);
        }
    }
}
