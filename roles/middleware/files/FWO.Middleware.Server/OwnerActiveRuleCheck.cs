using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report;
using System.Text;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Checks whether an owner still has active rule-owner assignments and sends notifications when needed.
    /// </summary>
    public class OwnerActiveRuleCheck(ApiConnection apiConnection, GlobalConfig globalConfig) : RuleNotificationBodyBase(globalConfig)
    {
        /// <summary>
        /// Fetches owners with a decommission date and sends due decommission notifications for each of them.
        /// </summary>
        /// <returns>The number of sent notifications.</returns>
        public async Task<int> CheckActiveRulesByScheduler()
        {
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
            int emailsSent = 0;

            foreach (FwoOwner owner in owners.Where(o => o.DecommDate != null))
            {
                emailsSent += await CheckActiveRules(owner, NotificationDeadline.DecommissionDate);
            }

            return emailsSent;
        }

        /// <summary>
        /// Fetches all active rules for an owner and sends immediate decommission notifications for them.
        /// </summary>
        /// <param name="owner">Owner whose active rules should be checked.</param>
        /// <returns>The number of sent notifications.</returns>
        public async Task<int> CheckActiveRulesSync(FwoOwner owner)
        {
            return await CheckActiveRules(owner, NotificationDeadline.None);
        }

        /// <summary>
        /// Fetches all active rules for an owner and sends matching notifications.
        /// </summary>
        /// <param name="owner">Owner whose active rules should be checked.</param>
        /// <param name="deadline">Notification deadline to handle.</param>
        /// <returns>The number of sent notifications.</returns>
        private async Task<int> CheckActiveRules(FwoOwner owner, NotificationDeadline deadline)
        {
            if (owner.Id <= 0)
            {
                return 0;
            }

            List<Rule> activeRules = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getActiveRulesByOwner, new { ownerId = owner.Id });
            if (activeRules.Count == 0)
            {
                return 0;
            }

            List<UserGroup> ownerGroups = await MiddlewareServerServices.GetInternalGroups(apiConnection);
            NotificationService notificationService = await NotificationService.CreateAsync(
                NotificationClient.AppDecomm, GlobalConfig, apiConnection, ownerGroups);

            string body = BuildRuleBody(owner, GlobalConfig.OwnerActiveRuleEmailBody, "",
                activeRules.OrderBy(rule => rule.Uid, StringComparer.OrdinalIgnoreCase));
            int emailsSent = 0;
            foreach (FwoNotification notification in notificationService.Notifications.Where(n => (n.OwnerId == null || n.OwnerId == owner.Id) && n.Deadline == deadline))
            {
                ReportBase? report = notification.Layout == NotificationLayout.HtmlInBody
                    ? CreateNotificationReport(owner, body)
                    : null;
                string notificationContent = notification.Layout == NotificationLayout.HtmlInBody
                    ? ""
                    : body;
                emailsSent += deadline == NotificationDeadline.None
                    ? await notificationService.SendNotification(notification, owner, notificationContent, report)
                    : await notificationService.SendNotificationIfDue(notification, owner, null, notificationContent, report);
            }

            await notificationService.UpdateNotificationsLastSent();
            return emailsSent;
        }

        /// <summary>
        /// Creates an HTML report wrapper for active-rule notifications so HtmlInBody layouts render a full report body.
        /// </summary>
        /// <param name="owner">Owner receiving the notification.</param>
        /// <param name="body">Pre-rendered notification HTML body.</param>
        /// <returns>HTML report for notification delivery.</returns>
        private ReportBase CreateNotificationReport(FwoOwner owner, string body)
        {
            return new OwnerActiveRuleNotificationReport(new UserConfig(GlobalConfig), owner, body);
        }

        /// <summary>
        /// Minimal report wrapper for owner active-rule notifications.
        /// </summary>
        private sealed class OwnerActiveRuleNotificationReport(UserConfig userConfig, FwoOwner owner, string body)
            : ReportBase(new(""), userConfig, FWO.Basics.ReportType.Rules)
        {
            /// <inheritdoc />
            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<FWO.Data.Report.ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            /// <inheritdoc />
            public override string ExportToCsv()
            {
                return "";
            }

            /// <inheritdoc />
            public override string ExportToJson()
            {
                return "";
            }

            /// <inheritdoc />
            public override string ExportToHtml()
            {
                return GenerateHtmlFrameBase(userConfig.GetText("OwnerActiveRules"), "", DateTime.Now, new StringBuilder(body), ownerFilter: owner.Name);
            }

            /// <inheritdoc />
            public override string SetDescription()
            {
                return "";
            }
        }
    }
}
