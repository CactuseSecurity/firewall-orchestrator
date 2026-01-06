using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Recert;
using FWO.Services;
using System.Timers;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the scheduler for the daily checks
    /// </summary>
    public class DailyCheckScheduler : SchedulerBase
    {
        private const string LogMessageTitle = "Daily Check";

        /// <summary>
        /// Async Constructor needing the connection
        /// </summary>
        public static async Task<DailyCheckScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig config = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new DailyCheckScheduler(apiConnection, config);
        }

        private DailyCheckScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeDailyCheckConfigChanges, SchedulerInterval.Days, "DailyCheck")
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
            ScheduleTimer.Stop();
            globalConfig.SubscriptionUpdateHandler([.. config]);
            StartScheduleTimer(1, globalConfig.DailyCheckStartAt);
        }

        /// <summary>
        /// define the processing to be done
        /// </summary>
        protected override async void Process(object? _, ElapsedEventArgs __)
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
                await CheckUnansweredInterfaceRequests();
            }
            catch(Exception exc)
            {
                await LogErrorsWithAlert(2, LogMessageTitle, GlobalConst.kDailyCheck, AlertCode.DailyCheckError, exc);
            }
        }

        private async Task RefreshRecert()
        {
            Log.WriteDebug(LogMessageTitle, "Refresh recert ownerships");
            await RecertRefresh.RecalcRecerts(apiConnection);
        }

        private async Task CheckRecerts()
        {
            if(globalConfig.RecCheckActive)
            {
                RecertCheck recertCheck = new (apiConnection, globalConfig);
                int emailsSent = await recertCheck.CheckRecertifications();
                Log.WriteDebug(LogMessageTitle, $"Recert Check: Sent {emailsSent} emails.");
                await AddLogEntry(0, globalConfig.GetText("daily_recert_check"), emailsSent + globalConfig.GetText("emails_sent"), GlobalConst.kDailyCheck);
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
                await SetAlert(globalConfig.GetText("sample_data"), description, GlobalConst.kDailyCheck, AlertCode.SampleDataExisting);
                await AddLogEntry(1, globalConfig.GetText("daily_sample_data_check"), description, GlobalConst.kDailyCheck);
            }
            else
            {
                await AddLogEntry(0, globalConfig.GetText("daily_sample_data_check"), globalConfig.GetText("no_sample_data_found"), GlobalConst.kDailyCheck);
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
                if (imp.LastIncompleteImport != null && imp.LastIncompleteImport.Length > 0) // import running
                {
                    if (imp.LastIncompleteImport[0].StartTime < DateTime.Now.AddHours(-globalConfig.MaxImportDuration))  // too long
                    {
                        jsonData = imp.LastIncompleteImport;
                        await SetAlert(globalConfig.GetText("import"), globalConfig.GetText("E7011"), GlobalConst.kDailyCheck, AlertCode.ImportRunningTooLong, new() { MgmtId = imp.MgmId, JsonData = jsonData });
                        importIssues++;
                    }
                }
                else if (imp.LastImport == null || imp.LastImport.Length == 0) // no import at all
                {
                    jsonData = imp;
                    await SetAlert(globalConfig.GetText("import"), globalConfig.GetText("E7012"), GlobalConst.kDailyCheck, AlertCode.NoImport, new() { MgmtId = imp.MgmId, JsonData = jsonData });
                    importIssues++;
                }
                else if (imp.LastImportAttempt != null && imp.LastImportAttempt < DateTime.Now.AddHours(-globalConfig.MaxImportInterval))
                // too long ago (not working for legacy devices as LastImportAttempt is not written)
                {
                    jsonData = imp;
                    await SetAlert(globalConfig.GetText("import"), globalConfig.GetText("E7013"), GlobalConst.kDailyCheck, AlertCode.SuccessfulImportOverdue, new() { MgmtId = imp.MgmId, JsonData = jsonData });
                    importIssues++;
                }
            }
            await AddLogEntry(importIssues != 0 ? 1 : 0, globalConfig.GetText("daily_importer_check"),
                importIssues != 0 ? importIssues + globalConfig.GetText("import_issues_found") : globalConfig.GetText("no_import_issues_found"), GlobalConst.kDailyCheck);
        }

        private async Task CheckUnansweredInterfaceRequests()
        {
            int emailsSent = 0;
            List<UserGroup> OwnerGroups = await MiddlewareServerServices.GetInternalGroups(apiConnection);
            WfHandler wfHandler = new (new(globalConfig), apiConnection, WorkflowPhases.implementation, OwnerGroups);
            await wfHandler.Init();
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, OwnerGroups);

            foreach(var notification in notificationService.Notifications)
            {
                List<WfTicket>? unansweredTickets = await wfHandler.GetOpenTickets(WfTaskType.new_interface.ToString(), 
                    (notification.RepeatOffsetAfterDeadline ?? 0) + (notification.InitialOffsetAfterDeadline ?? 0), 
                    notification.RepeatIntervalAfterDeadline);
                foreach(var ticket in unansweredTickets)
                {
                    FwoOwner? owner = ticket.Tasks.FirstOrDefault(r => r.TaskType == WfTaskType.new_interface.ToString())?.Owners.FirstOrDefault()?.Owner;
                    if(owner != null)
                    {
                        emailsSent += await notificationService.SendNotification(notification, owner, ticket.CreationDate, await PrepareBody(ticket, owner));
                    }
                }
            }
            await notificationService.UpdateNotificationsLastSent();
            Log.WriteDebug(LogMessageTitle, $"Unanswered Interface Requests Check: Sent {emailsSent} emails.");
        }

        private async Task<string> PrepareBody(WfTicket ticket, FwoOwner owner)
        {
            WfReqTask? reqTask = ticket.Tasks.FirstOrDefault(r => r.TaskType == WfTaskType.new_interface.ToString());
            FwoOwner? requestingOwner = await GetRequestingOwner(reqTask?.GetAddInfoIntValue(AdditionalInfoKeys.ReqOwner));

            return globalConfig.ModUnansweredReqEmailBody
                .Replace(Placeholder.REQUESTER, ticket.Requester?.Name)
                .Replace(Placeholder.REQUESTING_APPNAME, requestingOwner?.Name)
                .Replace(Placeholder.REQUESTING_APPID, requestingOwner?.ExtAppId)
                .Replace(Placeholder.APPNAME, owner.Name)
                .Replace(Placeholder.APPID, owner.ExtAppId)
                .Replace(Placeholder.INTERFACE_LINK, ConstructLink(owner, reqTask));
        }

        private async Task<FwoOwner?> GetRequestingOwner(int? ownerId)
        {
            FwoOwner? reqOwner = null;
            if(ownerId != null)
            {
                try
                {
                    reqOwner = await apiConnection.SendQueryAsync<FwoOwner>(OwnerQueries.getOwnerById, new { id = ownerId });
                }
                catch(Exception exc)
                {
                    await LogErrorsWithAlert(1, $"Unanswered Interface Requests Check", GlobalConst.kDailyCheck, AlertCode.DailyCheckError, exc);
                }
            }
            return reqOwner;
        }

        private string ConstructLink(FwoOwner owner, WfReqTask? reqTask)
        {
            int? connId = reqTask?.GetAddInfoIntValue(AdditionalInfoKeys.ConnId);
            string interfaceUrl = $"{globalConfig.UiHostName}/{PageName.Modelling}/{owner.ExtAppId}/{connId}";
            return $"<a target=\"_blank\" href=\"{interfaceUrl}\">{globalConfig.GetText("interface")}: {reqTask?.Title}</a>";
        }
    }
}
