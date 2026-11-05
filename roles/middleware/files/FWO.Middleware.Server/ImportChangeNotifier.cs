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

        private ReportBase? natChangeReport;

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
                    await NotifyImportChanges(cancellationToken);
                }
            }
            finally
            {
                WorkInProgress = false;
            }
        }

        /// <summary>
        /// Sends the rule change notifications and the NAT rule change notifications for the imports found and marks the imports as notified.
        /// NAT rule change notifications are only sent if the NAT changes report contains changes (or could not be generated).
        /// If the rule changes report is empty because the imports only contain NAT rule changes, the rule change notifications
        /// carry the NAT changes report instead.
        /// </summary>
        /// <param name="cancellationToken">Stops before the first notification is sent; imports then remain unnotified.</param>
        private async Task NotifyImportChanges(CancellationToken cancellationToken)
        {
            NotificationService ruleChangeNotificationService = await NotificationService.CreateAsync(NotificationClient.ImportChange, globalConfig, apiConnection);
            NotificationService natChangeNotificationService = await NotificationService.CreateAsync(NotificationClient.ImportNatChange, globalConfig, apiConnection);
            if (ruleChangeNotificationService.Notifications.Count == 0 && natChangeNotificationService.Notifications.Count == 0)
            {
                Log.WriteInfo(LogMessageTitle, "No notification configured for import changes. Imports remain unnotified.");
                return;
            }

            changeReport = null;
            natChangeReport = null;
            if (ruleChangeNotificationService.Notifications.Any(notification => notification.Layout != NotificationLayout.SimpleText))
            {
                changeReport = await GenerateChangeReport(ReportType.Changes, cancellationToken);
            }
            bool onlyNatChangesPossible = changeReport != null && !HasAnyChanges(changeReport);
            if (natChangeNotificationService.Notifications.Count > 0 || onlyNatChangesPossible)
            {
                // the NAT changes report is needed to decide whether there are any NAT rule changes to notify about
                natChangeReport = await GenerateChangeReport(ReportType.NatChanges, cancellationToken);
            }
            // last checkpoint: once the first notification is sent, all of them are sent and the imports marked as notified
            cancellationToken.ThrowIfCancellationRequested();
            if (natChangeReport != null && UseNatChangesReportForRuleChanges(changeReport, natChangeReport))
            {
                await SendNotifications(ruleChangeNotificationService, CreateNatChangesBody(natChangeReport), natChangeReport);
            }
            else
            {
                await SendNotifications(ruleChangeNotificationService, CreateBody(), changeReport);
            }
            if (HasNatChangesToNotify(natChangeReport))
            {
                string natBody = natChangeReport != null ? CreateNatChangesBody(natChangeReport) : CreateBody();
                await SendNotifications(natChangeNotificationService, natBody, natChangeReport);
            }
            await SetImportsNotified();
        }

        /// <summary>
        /// NAT rule change notifications are sent if the NAT changes report contains changes. If the report could not be
        /// generated, they are sent anyway (without report), as NAT rule changes cannot be ruled out.
        /// </summary>
        /// <param name="natReport">Generated NAT changes report or null if its generation failed.</param>
        /// <returns>True if the NAT rule change notifications have to be sent.</returns>
        private static bool HasNatChangesToNotify(ReportBase? natReport)
        {
            return natReport == null || !natReport.NoChangesFound();
        }

        /// <summary>
        /// An empty rule changes report is replaced by the NAT changes report if the imports only contain NAT rule changes.
        /// </summary>
        /// <param name="changeReport">Generated rule changes report or null if it was not needed or its generation failed.</param>
        /// <param name="natReport">Generated NAT changes report or null if it was not needed or its generation failed.</param>
        /// <returns>True if the rule change notifications have to carry the NAT changes report.</returns>
        private static bool UseNatChangesReportForRuleChanges(ReportBase? changeReport, ReportBase? natReport)
        {
            return changeReport != null && !HasAnyChanges(changeReport) && natReport != null && HasAnyChanges(natReport);
        }

        /// <summary>
        /// Checks whether a change report contains any rule, object, service or user changes.
        /// </summary>
        /// <param name="report">Generated change report.</param>
        /// <returns>True if the report contains at least one change.</returns>
        private static bool HasAnyChanges(ReportBase report)
        {
            return report.ReportData.ManagementData.Any(management =>
                (management.RuleChanges?.Length ?? 0) > 0 ||
                (management.ObjectChanges?.Length ?? 0) > 0 ||
                (management.ServiceChanges?.Length ?? 0) > 0 ||
                (management.UserChanges?.Length ?? 0) > 0);
        }

        private static async Task SendNotifications(NotificationService notificationService, string body, ReportBase? report)
        {
            if (notificationService.Notifications.Count == 0)
            {
                return;
            }
            foreach (FwoNotification notification in notificationService.Notifications)
            {
                await notificationService.SendNotification(notification, null, body, report);
            }
            await notificationService.UpdateNotificationsLastSent();
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

        private async Task<ReportBase?> GenerateChangeReport(ReportType reportType, CancellationToken cancellationToken)
        {
            try
            {
                return await ReportGenerator.GenerateFromTemplate(new ReportTemplate("", await SetFilters(reportType)), apiConnection, userConfig, DefaultInit.DoNothing, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, $"Generation of {reportType} report leads to exception.", exception);
                return null;
            }
        }

        private async Task<ReportParams> SetFilters(ReportType reportType)
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

            return new((int)reportType, deviceFilter)
            {
                // object changes are part of the rule changes report only, the NAT changes report would just repeat them
                IncludeObjects = reportType != ReportType.NatChanges && userConfig.GlobalConfig!.ImpChangeIncludeObjectChanges,
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

        /// <summary>
        /// Creates the notification body for NAT rule changes, listing the number of NAT rule changes per management.
        /// </summary>
        /// <param name="report">Generated NAT changes report.</param>
        /// <returns>Notification body.</returns>
        private string CreateNatChangesBody(ReportBase report)
        {
            StringBuilder body = new();
            foreach (ManagementReport management in report.ReportData.ManagementData.Where(m => m.RuleChanges != null && m.RuleChanges.Length > 0))
            {
                if (body.Length > 0)
                {
                    body.AppendLine();
                    body.AppendLine();
                }
                body.Append($"{management.Name} (id={management.Id}): {management.RuleChanges!.Length} {userConfig.GetText("nat_rule_changes")}");
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
