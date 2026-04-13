using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Rule expiry check class
    /// </summary>
    public class RuleExpiryCheck : RuleNotificationBodyBase
    {
        private readonly ApiConnection apiConnection;

        /// <summary>
        /// Constructor for rule expiry check class
        /// </summary>
        public RuleExpiryCheck(ApiConnection apiConnection, GlobalConfig globalConfig) : base(globalConfig)
        {
            this.apiConnection = apiConnection;
        }

        /// <summary>
        /// Rule expiry check
        /// </summary>
        public async Task<int> CheckRuleExpiry()
        {
            int emailsSent = 0;
            Dictionary<string, string> ruleExpiryInitiatorKeys = ParseRuleExpiryInitiatorKeys(GlobalConfig.RuleExpiryInitiatorKeys);
            List<UserGroup> ownerGroups = await MiddlewareServerServices.GetInternalGroups(apiConnection);
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.RuleTimer, GlobalConfig, apiConnection, ownerGroups);
            List<RuleOwnerWithRuleTimes> rulesByOwner = await apiConnection.SendQueryAsync<List<RuleOwnerWithRuleTimes>>(
                RuleQueries.getTimeBasedRulesByOwner);

            var ownerBuckets = rulesByOwner
                .Where(item => item.Owner != null && item.Rule != null)
                .GroupBy(item => item.Owner.Id);

            foreach (var ownerBucket in ownerBuckets)
            {
                FwoOwner owner = ownerBucket.First().Owner;
                List<RuleExpiryInfo> timedEntries = ownerBucket
                    .SelectMany(item => item.Rule.GetRuleTimesWithEndDate(ruleExpiryInitiatorKeys))
                    .DistinctBy(item => $"{item.Id}:{item.TimeObjectId}")
                    .OrderBy(item => item.EndTime)
                    .ToList();

                if (timedEntries.Count == 0)
                {
                    continue;
                }

                foreach (var notification in notificationService.Notifications.Where(n => n.OwnerId == null || n.OwnerId == owner.Id))
                {
                    List<RuleExpiryInfo> dueEntries = timedEntries
                        .Where(entry => NotificationService.IsNotificationDue(owner, entry.EndTime, notification))
                        .ToList();

                    if (dueEntries.Count == 0)
                    {
                        continue;
                    }

                    string timeIntervalText = BuildTimeIntervalText(notification);
                    string body = BuildRuleBody(
                        owner,
                        GlobalConfig.RuleExpiryEmailBody,
                        timeIntervalText,
                        dueEntries.OrderBy(item => item.EndTime),
                        [GlobalConfig.GetText("deadline"), GlobalConfig.GetText("ruleExpiryInitiator")],
                        expiryInfo => [expiryInfo.EndTime.ToString("yyyy-MM-dd"), expiryInfo.ExpiryInitiator]);
                    emailsSent += await notificationService.SendNotification(notification, owner, body, null, timeIntervalText);
                }
            }

            await notificationService.UpdateNotificationsLastSent();
            return emailsSent;
        }

        private static Dictionary<string, string> ParseRuleExpiryInitiatorKeys(string serializedKeys)
        {
            if (string.IsNullOrWhiteSpace(serializedKeys))
            {
                return [];
            }

            try
            {
                Dictionary<string, string>? parsedKeys = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(serializedKeys);
                if (parsedKeys == null)
                {
                    return [];
                }

                return parsedKeys
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                    .ToDictionary(
                        pair => pair.Key.Trim().ToLowerInvariant(),
                        pair => pair.Value?.Trim() ?? "",
                        StringComparer.Ordinal);
            }
            catch (System.Text.Json.JsonException)
            {
                return [];
            }
        }

        private string BuildTimeIntervalText(FwoNotification notification)
        {
            int intervalValue;
            SchedulerInterval intervalUnit;

            if ((notification.InitialOffsetAfterDeadline ?? 0) > 0)
            {
                intervalValue = notification.InitialOffsetAfterDeadline ?? 0;
                intervalUnit = notification.RepeatIntervalAfterDeadline ?? SchedulerInterval.Days;
            }
            else
            {
                intervalValue = notification.OffsetBeforeDeadline ?? 0;
                intervalUnit = notification.IntervalBeforeDeadline ?? SchedulerInterval.Days;
            }

            if (intervalValue <= 0)
            {
                return "0";
            }

            return $"{intervalValue} {GetLocalizedIntervalUnit(intervalUnit)}";
        }

        private string GetLocalizedIntervalUnit(SchedulerInterval intervalUnit)
        {
            string intervalTextKey = intervalUnit switch
            {
                SchedulerInterval.Days => "Days",
                SchedulerInterval.Weeks => "Weeks",
                SchedulerInterval.Months => "Months",
                _ => "Days"
            };
            return GlobalConfig.GetText(intervalTextKey);
        }

        private sealed class RuleOwnerWithRuleTimes
        {
            [JsonProperty("owner"), JsonPropertyName("owner")]
            public FwoOwner Owner { get; set; } = new();

            [JsonProperty("rule"), JsonPropertyName("rule")]
            public RuleWithExpiry Rule { get; set; } = new();
        }

        private sealed class RuleWithExpiry : Rule
        {
            public List<RuleExpiryInfo> GetRuleTimesWithEndDate(IReadOnlyDictionary<string, string> initiatorTexts)
            {
                return RuleTimes
                    .Select(ruleTime => new RuleExpiryInfo(this)
                    {
                        TimeObjectId = ruleTime.TimeObjId,
                        EndTime = ruleTime.TimeObj!.EndTime!.Value,
                        ExpiryInitiator = DetermineExpiryInitiator(ruleTime.TimeObj.Name, initiatorTexts)
                    })
                    .ToList();
            }

            private static string DetermineExpiryInitiator(string? timeObjectName, IReadOnlyDictionary<string, string> initiatorTexts)
            {
                if (string.IsNullOrWhiteSpace(timeObjectName) || initiatorTexts.Count == 0)
                {
                    return "";
                }

                string normalizedName = timeObjectName.Trim().ToLowerInvariant();
                foreach (KeyValuePair<string, string> initiatorText in initiatorTexts)
                {
                    if (normalizedName.EndsWith(initiatorText.Key, StringComparison.Ordinal))
                    {
                        return initiatorText.Value;
                    }
                }
                return "";
            }
        }

        private sealed class RuleExpiryInfo : Rule
        {
            public RuleExpiryInfo(Rule rule) : base(rule)
            { }

            public long TimeObjectId { get; set; }
            public DateTime EndTime { get; set; }
            public string ExpiryInitiator { get; set; } = "";
        }
    }
}
