using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report;
using FWO.Ui.Display;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Rule expiry check class
    /// </summary>
    public class RuleExpiryCheck
    {
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;

        /// <summary>
        /// Constructor for rule expiry check class
        /// </summary>
        public RuleExpiryCheck(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <summary>
        /// Rule expiry check
        /// </summary>
        public async Task<int> CheckRuleExpiry()
        {
            int emailsSent = 0;
            UserConfig displayUserConfig = new(globalConfig, false);
            RuleDisplayHtml ruleDisplayHtml = new(displayUserConfig);
            Dictionary<string, string> ruleExpiryInitiatorKeys = ParseRuleExpiryInitiatorKeys(globalConfig.RuleExpiryInitiatorKeys);
            List<UserGroup> ownerGroups = await MiddlewareServerServices.GetInternalGroups(apiConnection);
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.RuleTimer, globalConfig, apiConnection, ownerGroups);
            List<RuleOwnerWithRuleTimes> rulesByOwner = await apiConnection.SendQueryAsync<List<RuleOwnerWithRuleTimes>>(
                RuleQueries.getTimeBasedRulesByOwner);

            var ownerBuckets = rulesByOwner
                .Where(item => item.Owner != null && item.Rule != null)
                .GroupBy(item => item.Owner.Id);

            foreach (var ownerBucket in ownerBuckets)
            {
                FwoOwner owner = ownerBucket.First().Owner;
                List<RuleExpiryInfo> timedEntries = ownerBucket
                    .SelectMany(item => item.Rule.GetRuleTimesWithEndDate(ruleDisplayHtml, ruleExpiryInitiatorKeys))
                    .DistinctBy(item => $"{item.RuleId}:{item.TimeObjectId}")
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
                    string body = BuildRuleExpiryBody(owner, dueEntries, globalConfig.RuleExpiryEmailBody, timeIntervalText);
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

        private string BuildRuleExpiryBody(FwoOwner owner, List<RuleExpiryInfo> expiredEntries, string bodyTemplate, string timeIntervalText)
        {
            string introText = bodyTemplate
                    .Replace(Placeholder.APPNAME, owner.Name)
                    .Replace(Placeholder.APPID, owner.ExtAppId ?? "")
                    .Replace(Placeholder.TIME_INTERVAL, timeIntervalText);

            StringBuilder builder = new();
            builder.Append("<p>")
                .Append(WebUtility.HtmlEncode(introText).Replace(Environment.NewLine, "<br>").Replace("\n", "<br>"))
                .Append("</p>")
                .Append("<table border=\"1\" cellspacing=\"0\" cellpadding=\"4\">")
                .Append("<thead><tr>")
                .Append($"<th>{WebUtility.HtmlEncode(globalConfig.GetText("uid"))}</th>")
                .Append($"<th>{WebUtility.HtmlEncode(globalConfig.GetText("name"))}</th>")
                .Append($"<th>{WebUtility.HtmlEncode(globalConfig.GetText("source"))}</th>")
                .Append($"<th>{WebUtility.HtmlEncode(globalConfig.GetText("destination"))}</th>")
                .Append($"<th>{WebUtility.HtmlEncode(globalConfig.GetText("service"))}</th>")
                .Append($"<th>{WebUtility.HtmlEncode(globalConfig.GetText("change_id"))}</th>")
                .Append($"<th>{WebUtility.HtmlEncode(globalConfig.GetText("last_hit"))}</th>")
                .Append($"<th>{WebUtility.HtmlEncode(globalConfig.GetText("deadline"))}</th>")
                .Append($"<th>{WebUtility.HtmlEncode(globalConfig.GetText("ruleExpiryInitiator"))}</th>")
                .Append("</tr></thead><tbody>");

            foreach (RuleExpiryInfo entry in expiredEntries.OrderBy(item => item.EndTime))
            {
                builder.Append("<tr>")
                    .Append(HtmlCell(entry.RuleUid))
                    .Append(HtmlCell(entry.RuleName))
                    .Append(HtmlRawCell(entry.Source))
                    .Append(HtmlRawCell(entry.Destination))
                    .Append(HtmlRawCell(entry.Service))
                    .Append(HtmlCell(entry.ChangeId))
                    .Append(HtmlCell(entry.LastHitDate?.ToString("yyyy-MM-dd") ?? ""))
                    .Append(HtmlCell(entry.EndTime.ToString("yyyy-MM-dd")))
                    .Append(HtmlCell(entry.ExpiryInitiator))
                    .Append("</tr>");
            }

            builder.Append("</tbody></table>");
            return builder.ToString();
        }

        private string BuildTimeIntervalText(FwoNotification notification)
        {
            int intervalValue;
            SchedulerInterval intervalUnit;

            if ((notification.InitialOffsetAfterDeadline ?? 0) > 0)
            {
                intervalValue = notification.InitialOffsetAfterDeadline ?? 0;
                intervalUnit = notification.RepeatIntervalAfterDeadline;
            }
            else
            {
                intervalValue = notification.OffsetBeforeDeadline ?? 0;
                intervalUnit = notification.IntervalBeforeDeadline;
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
            return globalConfig.GetText(intervalTextKey);
        }

        private static string HtmlCell(string? value)
        {
            return $"<td>{WebUtility.HtmlEncode(value ?? "")}</td>";
        }

        private static string HtmlRawCell(string? value)
        {
            return $"<td>{value ?? ""}</td>";
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
            public List<RuleExpiryInfo> GetRuleTimesWithEndDate(RuleDisplayHtml ruleDisplayHtml, IReadOnlyDictionary<string, string> initiatorTexts)
            {
                string changeId = ExtractChangeId(CustomFields);
                string source = ruleDisplayHtml.DisplaySource(this, OutputLocation.export, ReportType.ResolvedRules);
                string destination = ruleDisplayHtml.DisplayDestination(this, OutputLocation.export, ReportType.ResolvedRules);
                string service = ruleDisplayHtml.DisplayServices(this, OutputLocation.export, ReportType.ResolvedRules);

                return RuleTimes
                    .Select(ruleTime => new RuleExpiryInfo
                    {
                        RuleId = Id,
                        TimeObjectId = ruleTime.TimeObjId,
                        RuleUid = Uid ?? "",
                        RuleName = Name ?? "",
                        Source = source,
                        Destination = destination,
                        Service = service,
                        ChangeId = changeId,
                        LastHitDate = Metadata.LastHit,
                        EndTime = ruleTime.TimeObj!.EndTime!.Value,
                        ExpiryInitiator = DetermineExpiryInitiator(ruleTime.TimeObj.Name, initiatorTexts)
                    })
                    .ToList();
            }

            private static string ExtractChangeId(string customFieldsString)
            {
                if (string.IsNullOrWhiteSpace(customFieldsString))
                {
                    return "";
                }

                try
                {
                    Dictionary<string, string>? customFields = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(customFieldsString);
                    if (customFields == null)
                    {
                        return "";
                    }

                    if (customFields.TryGetValue(GlobalConst.kField2, out string? value) && !string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }

                    if (customFields.TryGetValue(GlobalConst.kDatumRegelpr, out string? fallback) && !string.IsNullOrWhiteSpace(fallback))
                    {
                        return fallback;
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    return "";
                }

                return "";
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

        private sealed class RuleExpiryInfo
        {
            public long RuleId { get; set; }
            public long TimeObjectId { get; set; }
            public string RuleUid { get; set; } = "";
            public string RuleName { get; set; } = "";
            public string Source { get; set; } = "";
            public string Destination { get; set; } = "";
            public string Service { get; set; } = "";
            public string ChangeId { get; set; } = "";
            public DateTime? LastHitDate { get; set; }
            public DateTime EndTime { get; set; }
            public string ExpiryInitiator { get; set; } = "";
        }
    }
}
