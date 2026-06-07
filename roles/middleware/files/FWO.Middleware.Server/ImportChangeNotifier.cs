using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Report;
using FWO.Services;
using Newtonsoft.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the Import Change Notification
    /// </summary>
    public class ImportChangeNotifier
    {
        /// <summary>
        /// Api Connection
        /// </summary>
        protected readonly ApiConnection apiConnection;

        /// <summary>
        /// Global Config
        /// </summary>
        protected GlobalConfig globalConfig;

        private ReportBase? changeReport;

        private struct ImportToNotify
        {
            [JsonProperty("control_id"), JsonPropertyName("control_id")]
            public long ControlId { get; set; }

            [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
            public int MgmtId { get; set; }

            [JsonProperty("management"), JsonPropertyName("management")]
            public ImportManagement Mgmt { get; set; }

            [JsonProperty("stop_time"), JsonPropertyName("stop_time")]
            public DateTime StopTime { get; set; }

            [JsonProperty("security_relevant_changes_counter"), JsonPropertyName("security_relevant_changes_counter")]
            public int RelevantChanges { get; set; }
        };
        private struct ImportManagement
        {
            [JsonProperty("mgm_name"), JsonPropertyName("mgm_name")]
            public string MgmtName { get; set; }
        }

        private List<ImportToNotify> importsToNotify = [];

        private bool WorkInProgress = false;
        private readonly DeviceFilter deviceFilter = new();
        private List<int> importedManagements = [];
        private readonly UserConfig userConfig;
        private const string LogMessageTitle = "Import Change Notifier";


        /// <summary>
        /// Constructor for Import Change Notifier
        /// </summary>
        public ImportChangeNotifier(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            userConfig = new(globalConfig);
        }

        /// <summary>
        /// Run the Import Change Notifier
        /// </summary>
        public async Task Run()
        {
            try
            {
                if (!WorkInProgress)
                {
                    WorkInProgress = true;
                    if (await NewImportFound())
                    {
                        NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.ImportChange, globalConfig, apiConnection, []);
                        if (notificationService.Notifications.Count == 0)
                        {
                            Log.WriteInfo(LogMessageTitle, "No notification configured for import changes. Imports remain unnotified.");
                            WorkInProgress = false;
                            return;
                        }

                        if (notificationService.Notifications.Any(notification => notification.Layout != NotificationLayout.SimpleText))
                        {
                            await GenerateChangeReport();
                        }
                        foreach (FwoNotification notification in notificationService.Notifications)
                        {
                            await notificationService.SendNotification(notification, null, CreateBody(), changeReport);
                        }
                        await notificationService.UpdateNotificationsLastSent();
                        await SetImportsNotified();
                    }
                    WorkInProgress = false;
                }
            }
            catch (Exception)
            {
                WorkInProgress = false;
                throw;
            }
        }

        private async Task<bool> NewImportFound()
        {
            if (userConfig.GlobalConfig!.ImpChangeIncludeObjectChanges)
            {
                importsToNotify = await apiConnection.SendQueryAsync<List<ImportToNotify>>(ReportQueries.getImportsToNotifyForAnyChanges);
            }
            else
            {
                importsToNotify = await apiConnection.SendQueryAsync<List<ImportToNotify>>(ReportQueries.getImportsToNotifyForRuleChanges);
            }

            importedManagements = [];
            foreach (var impMgt in importsToNotify.Select(i => i.MgmtId).Where(m => !importedManagements.Contains(m)))
            {
                importedManagements.Add(impMgt);
            }
            
            return importsToNotify.Count > 0;
        }

        private async Task GenerateChangeReport()
        {
            try
            {
                changeReport = await ReportGenerator.GenerateFromTemplate(new ReportTemplate("", await SetFilters()), apiConnection, userConfig, DefaultInit.DoNothing);
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, $"Report generation leads to exception.", exception);
            }
        }

        private async Task<ReportParams> SetFilters()
        {
            deviceFilter.Managements = [];
            var result = await apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagementOrSuperMgm);
            List<ManagementSelect> selectedManagements = [];

            foreach (ManagementSelect management in result.Where(m => importedManagements.Contains(m.Id)))
            {
                if (management.IsSuperManager)
                {
                    selectedManagements.AddRange(management.subManagers);
                }
                else
                {
                    selectedManagements.Add(management);
                }
            }

            deviceFilter.Managements = [.. selectedManagements.DistinctBy(m => m.Id)];

            deviceFilter.ApplyFullDeviceSelection(true);

            return new((int)ReportType.Changes, deviceFilter)
            {
                IncludeObjects = userConfig.GlobalConfig!.ImpChangeIncludeObjectChanges,
                TimeFilter = new()
                {
                    TimeRangeType = TimeRangeType.Fixeddates,
                    StartTime = importsToNotify[0].StopTime,
                    EndTime = importsToNotify[^1].StopTime.AddSeconds(1)
                }
            };
        }

        private string CreateBody()
        {
            StringBuilder body = new();
            foreach (var mgmtId in importedManagements)
            {
                int mgmtCounter = 0;
                foreach (var imp in importsToNotify.Where(x => x.MgmtId == mgmtId))
                {
                    mgmtCounter += imp.RelevantChanges;
                }
                if (body.Length > 0)
                {
                    body.AppendLine();
                    body.AppendLine();
                }
                body.Append($"{importsToNotify.FirstOrDefault(x => x.MgmtId == mgmtId).Mgmt.MgmtName} (id={mgmtId}): {mgmtCounter} {userConfig.GetText("changes")}");
            }
            return body.ToString();
        }

        private async Task SetImportsNotified()
        {
            try
            {
                await apiConnection.SendQueryAsync<ReturnId>(ReportQueries.setImportsNotified, new { ids = importsToNotify.ConvertAll(x => x.ControlId) });
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, $"Could not mark imports as notified.", exception);
            }
        }
    }
}
