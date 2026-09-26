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
    public class ImportChangeNotifier : IDisposable
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
            /// <summary>
            /// Gets the ControlId value.
            /// </summary>
            [JsonProperty("control_id"), JsonPropertyName("control_id")]
            public long ControlId { get; set; }

            /// <summary>
            /// Gets the MgmtId value.
            /// </summary>
            [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
            public int MgmtId { get; set; }

            /// <summary>
            /// Gets the Mgmt value.
            /// </summary>
            [JsonProperty("management"), JsonPropertyName("management")]
            public ImportManagement Mgmt { get; set; }

            /// <summary>
            /// Gets the StopTime value.
            /// </summary>
            [JsonProperty("stop_time"), JsonPropertyName("stop_time")]
            public DateTime StopTime { get; set; }

            /// <summary>
            /// Gets the RelevantChanges value.
            /// </summary>
            [JsonProperty("security_relevant_changes_counter"), JsonPropertyName("security_relevant_changes_counter")]
            public int RelevantChanges { get; set; }
        };
        private struct ImportManagement
        {
            /// <summary>
            /// Gets the MgmtName value.
            /// </summary>
            [JsonProperty("mgm_name"), JsonPropertyName("mgm_name")]
            public string MgmtName { get; set; }
        }

        private List<ImportToNotify> importsToNotify = [];

        private bool WorkInProgress = false;
        private readonly DeviceFilter deviceFilter = new();
        private List<int> importedManagements = [];
        private readonly UserConfig userConfig;
        private bool disposed = false;
        private const string LogMessageTitle = "Import Change Notifier";


        /// <summary>
        /// Constructor for Import Change Notifier
        /// </summary>
        public ImportChangeNotifier(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection, globalConfig.DefaultLanguage);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources used by the notifier.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    userConfig.Dispose();
                }
                disposed = true;
            }
        }

        /// <summary>
        /// Run the Import Change Notifier
        /// </summary>
        /// <param name="cancellationToken">Stops before the first notification is sent; imports then remain unnotified.</param>
        public async Task Run(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (WorkInProgress)
            {
                return;
            }
            WorkInProgress = true;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await NewImportFound())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.ImportChange, globalConfig, apiConnection);
                    if (notificationService.Notifications.Count == 0)
                    {
                        Log.WriteInfo(LogMessageTitle, "No notification configured for import changes. Imports remain unnotified.");
                        return;
                    }

                    if (notificationService.Notifications.Any(notification => notification.Layout != NotificationLayout.SimpleText))
                    {
                        await GenerateChangeReport(cancellationToken);
                    }
                    // last checkpoint: once the first notification is sent, all of them are sent and the imports marked as notified
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (FwoNotification notification in notificationService.Notifications)
                    {
                        await notificationService.SendNotification(notification, null, CreateBody(), changeReport);
                    }
                    await notificationService.UpdateNotificationsLastSent();
                    await SetImportsNotified();
                }
            }
            finally
            {
                WorkInProgress = false;
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

        private async Task GenerateChangeReport(CancellationToken cancellationToken)
        {
            try
            {
                changeReport = await ReportGenerator.GenerateFromTemplate(new ReportTemplate("", await SetFilters()), apiConnection, userConfig, DefaultInit.DoNothing, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
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
