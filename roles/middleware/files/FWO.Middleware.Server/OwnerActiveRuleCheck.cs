using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Checks whether an owner still has active rule-owner assignments and sends notifications when needed.
    /// </summary>
    public class OwnerActiveRuleCheck(ApiConnection apiConnection, GlobalConfig globalConfig) : RuleNotificationBodyBase(globalConfig)
    {
        /// <summary>
        /// Fetches all active rules for an owner and sends configured decommission notifications for them.
        /// </summary>
        /// <param name="owner">Owner whose active rules should be checked.</param>
        /// <returns>The number of sent notifications.</returns>
        public async Task<int> CheckActiveRules(FwoOwner owner)
        {
            if (owner.Id <= 0)
            {
                return 0;
            }

            List<Rule> activeRules = await apiConnection.SendQueryAsync<List<Rule>>(
                RuleQueries.getActiveRulesByOwner,
                new { ownerId = owner.Id });
            if (activeRules.Count == 0)
            {
                return 0;
            }

            List<UserGroup> ownerGroups = await MiddlewareServerServices.GetInternalGroups(apiConnection);
            NotificationService notificationService = await NotificationService.CreateAsync(
                NotificationClient.AppDecomm,
                GlobalConfig,
                apiConnection,
                ownerGroups);

            string body = BuildRuleBody(
                owner,
                GlobalConfig.OwnerActiveRuleEmailBody,
                "",
                activeRules.OrderBy(rule => rule.Uid, StringComparer.OrdinalIgnoreCase));
            int emailsSent = 0;
            foreach (FwoNotification notification in notificationService.Notifications.Where(n => n.OwnerId == null || n.OwnerId == owner.Id))
            {
                emailsSent += await notificationService.SendNotification(notification, owner, body);
            }

            await notificationService.UpdateNotificationsLastSent();
            return emailsSent;
        }
    }
}
