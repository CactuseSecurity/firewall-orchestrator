using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Collections;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleExpiryCheckTest
    {
        [Test]
        public async Task CheckRuleExpiry_SendsNotification_WhenExpiredRuleIsDue()
        {
            RuleExpiryCheckTestApiConn apiConnection = new();
            apiConnection.Notifications =
            [
                CreateRuleTimerNotification(1)
            ];
            apiConnection.ExpiredRuleEntries =
            [
                new ExpiredRuleEntryInput
                {
                    OwnerId = 1,
                    OwnerName = "Owner1",
                    OwnerExtAppId = "APP1",
                    RuleId = 1001,
                    RuleUid = "uid-1001",
                    RuleName = "Allow A",
                    RuleNumber = 10,
                    ManagementId = 5,
                    RulebaseName = "RB1",
                    SourceShort = "SrcShort",
                    SourceLong = "SrcLong",
                    DestinationShort = "DstShort",
                    DestinationLong = "DstLong",
                    ServiceShort = "SvcShort",
                    ServiceLong = "SvcLong",
                    CustomFields = "{\"field-2\":\"CHG-1001\"}",
                    LastHit = DateTime.Now.AddDays(-5),
                    RuleTimes = [new() { TimeObjId = 11, TimeObjName = "TO-1", EndTime = DateTime.Now.AddDays(-2) }]
                }
            ];

            RuleExpiryCheck check = new(apiConnection, CreateGlobalConfig());

            int sentEmails = await check.CheckRuleExpiry();

            ClassicAssert.AreEqual(1, sentEmails);
            ClassicAssert.AreEqual(1, apiConnection.LastUpdatedNotificationIdCount);
        }

        [Test]
        public async Task CheckRuleExpiry_DoesNotSend_WhenNotificationIsNotDue()
        {
            RuleExpiryCheckTestApiConn apiConnection = new();
            FwoNotification notification = CreateRuleTimerNotification(1);
            notification.LastSent = DateTime.Now;
            apiConnection.Notifications = [notification];
            apiConnection.ExpiredRuleEntries =
            [
                new ExpiredRuleEntryInput
                {
                    OwnerId = 1,
                    OwnerName = "Owner1",
                    OwnerExtAppId = "APP1",
                    RuleId = 1002,
                    RuleUid = "uid-1002",
                    RuleName = "Allow B",
                    RuleNumber = 20,
                    ManagementId = 5,
                    RulebaseName = "RB1",
                    SourceShort = "SrcShort",
                    SourceLong = "SrcLong",
                    DestinationShort = "DstShort",
                    DestinationLong = "DstLong",
                    ServiceShort = "SvcShort",
                    ServiceLong = "SvcLong",
                    CustomFields = "{\"field-2\":\"CHG-1002\"}",
                    LastHit = DateTime.Now.AddDays(-7),
                    RuleTimes = [new() { TimeObjId = 12, TimeObjName = "TO-2", EndTime = DateTime.Now.AddDays(-1) }]
                }
            ];

            RuleExpiryCheck check = new(apiConnection, CreateGlobalConfig());

            int sentEmails = await check.CheckRuleExpiry();

            ClassicAssert.AreEqual(0, sentEmails);
            ClassicAssert.AreEqual(0, apiConnection.LastUpdatedNotificationIdCount);
        }

        [Test]
        public async Task CheckRuleExpiry_AggregatesByOwner_AndAppliesOwnerScopedNotifications()
        {
            RuleExpiryCheckTestApiConn apiConnection = new();
            apiConnection.Notifications =
            [
                CreateRuleTimerNotification(1),
                CreateRuleTimerNotification(2, ownerId: 2)
            ];
            apiConnection.ExpiredRuleEntries =
            [
                new ExpiredRuleEntryInput
                {
                    OwnerId = 1,
                    OwnerName = "Owner1",
                    OwnerExtAppId = "APP1",
                    RuleId = 2001,
                    RuleUid = "uid-2001",
                    RuleName = "Allow C",
                    RuleNumber = 30,
                    ManagementId = 8,
                    RulebaseName = "RB2",
                    SourceShort = "S1",
                    SourceLong = "SL1",
                    DestinationShort = "D1",
                    DestinationLong = "DL1",
                    ServiceShort = "V1",
                    ServiceLong = "VL1",
                    CustomFields = "{\"field-2\":\"CHG-2001\"}",
                    LastHit = DateTime.Now.AddDays(-4),
                    RuleTimes = [new() { TimeObjId = 21, TimeObjName = "TO-3", EndTime = DateTime.Now.AddDays(-3) }]
                },
                new ExpiredRuleEntryInput
                {
                    OwnerId = 2,
                    OwnerName = "Owner2",
                    OwnerExtAppId = "APP2",
                    RuleId = 2002,
                    RuleUid = "uid-2002",
                    RuleName = "Allow D",
                    RuleNumber = 31,
                    ManagementId = 8,
                    RulebaseName = "RB2",
                    SourceShort = "S2",
                    SourceLong = "SL2",
                    DestinationShort = "D2",
                    DestinationLong = "DL2",
                    ServiceShort = "V2",
                    ServiceLong = "VL2",
                    CustomFields = "{\"field-2\":\"CHG-2002\"}",
                    LastHit = DateTime.Now.AddDays(-4),
                    RuleTimes = [new() { TimeObjId = 22, TimeObjName = "TO-4", EndTime = DateTime.Now.AddDays(-3) }]
                }
            ];

            RuleExpiryCheck check = new(apiConnection, CreateGlobalConfig());

            int sentEmails = await check.CheckRuleExpiry();

            ClassicAssert.AreEqual(3, sentEmails, "Expected one global mail per owner plus one owner-scoped mail for owner 2.");
            ClassicAssert.AreEqual(2, apiConnection.LastUpdatedNotificationIdCount, "Only unique notification ids should be marked as sent.");
        }

        [Test]
        public void ParseRuleExpiryInitiatorKeys_ParsesAndNormalizes_AndHandlesInvalidInput()
        {
            MethodInfo? parseMethod = typeof(RuleExpiryCheck).GetMethod("ParseRuleExpiryInitiatorKeys", BindingFlags.NonPublic | BindingFlags.Static);
            ClassicAssert.IsNotNull(parseMethod, "Expected private method ParseRuleExpiryInitiatorKeys.");

            Dictionary<string, string> parsed = (Dictionary<string, string>)(parseMethod!.Invoke(null, ["{ \" User \": \"Text A\", \"nsb\": \"Text B\" }"]) ?? new Dictionary<string, string>());
            ClassicAssert.AreEqual(2, parsed.Count);
            ClassicAssert.AreEqual("Text A", parsed["user"]);
            ClassicAssert.AreEqual("Text B", parsed["nsb"]);

            Dictionary<string, string> parsedInvalid = (Dictionary<string, string>)(parseMethod.Invoke(null, ["{ invalid json }"]) ?? new Dictionary<string, string>());
            ClassicAssert.AreEqual(0, parsedInvalid.Count);
        }

        [Test]
        public void GetLocalizedIntervalUnit_ReturnsConfiguredTexts()
        {
            SimulatedGlobalConfig config = CreateGlobalConfig();
            config.DummyTranslate["Days"] = "Tage";
            config.DummyTranslate["Weeks"] = "Wochen";
            config.DummyTranslate["Months"] = "Monate";

            RuleExpiryCheck check = new(new RuleExpiryCheckTestApiConn(), config);
            MethodInfo? getLocalizedMethod = typeof(RuleExpiryCheck).GetMethod("GetLocalizedIntervalUnit", BindingFlags.NonPublic | BindingFlags.Instance);
            ClassicAssert.IsNotNull(getLocalizedMethod, "Expected private method GetLocalizedIntervalUnit.");

            string days = (string)(getLocalizedMethod!.Invoke(check, [SchedulerInterval.Days]) ?? "");
            string weeks = (string)(getLocalizedMethod.Invoke(check, [SchedulerInterval.Weeks]) ?? "");
            string months = (string)(getLocalizedMethod.Invoke(check, [SchedulerInterval.Months]) ?? "");

            ClassicAssert.AreEqual("Tage", days);
            ClassicAssert.AreEqual("Wochen", weeks);
            ClassicAssert.AreEqual("Monate", months);
        }

        [Test]
        public void DetermineExpiryInitiator_UsesSuffixMapping()
        {
            Type? ruleWithExpiryType = typeof(RuleExpiryCheck).GetNestedType("RuleWithExpiry", BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(ruleWithExpiryType, "Expected private nested type RuleWithExpiry.");

            MethodInfo? determineMethod = ruleWithExpiryType!.GetMethod("DetermineExpiryInitiator", BindingFlags.NonPublic | BindingFlags.Static);
            ClassicAssert.IsNotNull(determineMethod, "Expected private method DetermineExpiryInitiator.");

            IReadOnlyDictionary<string, string> initiatorTexts = new Dictionary<string, string>
            {
                ["user"] = "by user",
                ["nsb"] = "by nsb"
            };

            string resultUser = (string)(determineMethod!.Invoke(null, ["my-time-user", initiatorTexts]) ?? "");
            string resultNsb = (string)(determineMethod.Invoke(null, ["my-time-nsb", initiatorTexts]) ?? "");
            string resultUnknown = (string)(determineMethod.Invoke(null, ["my-time-other", initiatorTexts]) ?? "");

            ClassicAssert.AreEqual("by user", resultUser);
            ClassicAssert.AreEqual("by nsb", resultNsb);
            ClassicAssert.AreEqual("", resultUnknown);
        }

        [Test]
        public void BuildRuleExpiryBody_ReplacesPlaceholders()
        {
            RuleExpiryCheck check = new(new RuleExpiryCheckTestApiConn(), CreateGlobalConfig());
            MethodInfo? buildBodyMethod = typeof(RuleExpiryCheck).GetMethod("BuildRuleBody", BindingFlags.NonPublic | BindingFlags.Instance);
            ClassicAssert.IsNotNull(buildBodyMethod, "Expected protected method BuildRuleBody.");
            MethodInfo closedBuildBodyMethod = (buildBodyMethod ?? throw new InvalidOperationException("BuildRuleBody method not found."))
                .MakeGenericMethod(typeof(RuleExpiryCheck).GetNestedType("RuleExpiryInfo", BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("RuleExpiryInfo type not found."));

            Type? ruleExpiryInfoType = typeof(RuleExpiryCheck).GetNestedType("RuleExpiryInfo", BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(ruleExpiryInfoType, "Expected private nested type RuleExpiryInfo.");
            Type resolvedRuleExpiryInfoType = ruleExpiryInfoType ?? throw new InvalidOperationException("RuleExpiryInfo type not found.");

            Rule rule = new()
            {
                Uid = "uid-1",
                Name = "rule-1",
                Source = "src",
                Destination = "dst",
                Service = "svc",
                CustomFields = "{\"field2\":\"chg-1\"}",
                Metadata = new RuleMetadata { LastHit = DateTime.Today }
            };
            object ruleEntry = Activator.CreateInstance(resolvedRuleExpiryInfoType, [rule]) ?? throw new InvalidOperationException("Could not create RuleExpiryInfo.");
            resolvedRuleExpiryInfoType.GetProperty("EndTime")?.SetValue(ruleEntry, DateTime.Today);
            resolvedRuleExpiryInfoType.GetProperty("ExpiryInitiator")?.SetValue(ruleEntry, "init");

            Type listType = typeof(List<>).MakeGenericType(resolvedRuleExpiryInfoType);
            IList entries = (IList)(Activator.CreateInstance(listType) ?? throw new InvalidOperationException("Could not create entry list."));
            entries.Add(ruleEntry);

            FwoOwner owner = new() { Name = "OwnerX", ExtAppId = "APPX" };
            string bodyTemplate = "Hello @@APPNAME@@ @@APPID@@ @@TIME_INTERVAL@@";
            string intervalText = "2 Weeks";

            string body = (string)(closedBuildBodyMethod.Invoke(check, [owner, bodyTemplate, intervalText, entries, null, null]) ?? "");

            ClassicAssert.IsTrue(body.Contains("Hello OwnerX APPX 2 Weeks"));
            ClassicAssert.IsFalse(body.Contains("@@APPNAME@@"));
            ClassicAssert.IsFalse(body.Contains("@@APPID@@"));
            ClassicAssert.IsFalse(body.Contains("@@TIME_INTERVAL@@"));
        }

        private static FwoNotification CreateRuleTimerNotification(int id, int? ownerId = null)
        {
            return new FwoNotification
            {
                Id = id,
                OwnerId = ownerId,
                Deadline = NotificationDeadline.RuleExpiry,
                RecipientTo = EmailRecipientOption.OtherAddresses,
                EmailAddressTo = "x@y.de",
                EmailSubject = "rule expiry",
                RepeatIntervalAfterDeadline = SchedulerInterval.Days,
                InitialOffsetAfterDeadline = 0,
                RepeatOffsetAfterDeadline = 1,
                RepetitionsAfterDeadline = 10
            };
        }

        private static SimulatedGlobalConfig CreateGlobalConfig()
        {
            return new SimulatedGlobalConfig
            {
                UseDummyEmailAddress = true,
                DummyEmailAddress = "x@y.de",
                EmailServerAddress = "127.0.0.1",
                EmailPort = 1
            };
        }

        private sealed class RuleExpiryCheckTestApiConn : SimulatedApiConnection
        {
            public List<FwoNotification> Notifications { get; set; } = [];
            public List<ExpiredRuleEntryInput> ExpiredRuleEntries { get; set; } = [];
            public int LastUpdatedNotificationIdCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                Type responseType = typeof(QueryResponseType);

                if (responseType == typeof(List<Ldap>) && query == AuthQueries.getLdapConnections)
                {
                    List<Ldap> internalLdaps =
                    [
                        new Ldap
                        {
                            Address = "127.0.0.1",
                            Port = 1,
                            UserSearchPath = "uid=intuser2,ou=users,ou=tenant2,dc=fworch,dc=internal",
                            GroupSearchPath = "ou=groups,dc=fworch,dc=internal"
                        }
                    ];
                    return Task.FromResult((QueryResponseType)(object)internalLdaps);
                }

                if (responseType == typeof(List<FwoNotification>) && query == NotificationQueries.getNotifications)
                {
                    return Task.FromResult((QueryResponseType)(object)Notifications);
                }

                if (responseType == typeof(ReturnId) && query == NotificationQueries.updateNotificationsLastSent)
                {
                    LastUpdatedNotificationIdCount = CountIds(variables);
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = LastUpdatedNotificationIdCount });
                }

                if (query == RuleQueries.getTimeBasedRulesByOwner && responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    object result = BuildExpiredRuleResponse(responseType);
                    return Task.FromResult((QueryResponseType)result);
                }

                throw new NotImplementedException($"Unhandled query: {query}");
            }

            private static int CountIds(object? variables)
            {
                object? ids = variables?.GetType().GetProperty("ids")?.GetValue(variables);
                return ids is ICollection coll ? coll.Count : 0;
            }

            private object BuildExpiredRuleResponse(Type listType)
            {
                IList resultList = (IList)(Activator.CreateInstance(listType) ?? throw new InvalidOperationException("Could not create response list."));
                Type rowType = listType.GetGenericArguments()[0];
                Type ruleType = rowType.GetProperty("Rule")?.PropertyType ?? throw new InvalidOperationException("Missing Rule type.");

                foreach (ExpiredRuleEntryInput entry in ExpiredRuleEntries)
                {
                    object row = Activator.CreateInstance(rowType) ?? throw new InvalidOperationException("Could not create response row.");
                    rowType.GetProperty("Owner")?.SetValue(row, new FwoOwner { Id = entry.OwnerId, Name = entry.OwnerName, ExtAppId = entry.OwnerExtAppId });

                    object rule = Activator.CreateInstance(ruleType) ?? throw new InvalidOperationException("Could not create rule.");
                    ruleType.GetProperty("RuleId")?.SetValue(rule, entry.RuleId);
                    ruleType.GetProperty("RuleUid")?.SetValue(rule, entry.RuleUid);
                    ruleType.GetProperty("RuleName")?.SetValue(rule, entry.RuleName);
                    ruleType.GetProperty("RuleNumber")?.SetValue(rule, entry.RuleNumber);
                    ruleType.GetProperty("ManagementId")?.SetValue(rule, entry.ManagementId);
                    ruleType.GetProperty("SourceShort")?.SetValue(rule, entry.SourceShort);
                    ruleType.GetProperty("SourceLong")?.SetValue(rule, entry.SourceLong);
                    ruleType.GetProperty("DestinationShort")?.SetValue(rule, entry.DestinationShort);
                    ruleType.GetProperty("DestinationLong")?.SetValue(rule, entry.DestinationLong);
                    ruleType.GetProperty("ServiceShort")?.SetValue(rule, entry.ServiceShort);
                    ruleType.GetProperty("ServiceLong")?.SetValue(rule, entry.ServiceLong);
                    ruleType.GetProperty("CustomFields")?.SetValue(rule, entry.CustomFields);
                    ruleType.GetProperty("Metadata")?.SetValue(rule, new RuleMetadata { LastHit = entry.LastHit });
                    ruleType.GetProperty("Rulebase")?.SetValue(rule, new Rulebase { Name = entry.RulebaseName });

                    PropertyInfo? ruleTimesProp = ruleType.GetProperty("RuleTimes");
                    Type ruleTimesListType = ruleTimesProp?.PropertyType ?? throw new InvalidOperationException("Missing RuleTimes list type.");
                    IList ruleTimes = (IList)(Activator.CreateInstance(ruleTimesListType) ?? throw new InvalidOperationException("Could not create RuleTimes list."));
                    foreach (ExpiredTimeInput time in entry.RuleTimes)
                    {
                        ruleTimes.Add(new RuleTime
                        {
                            TimeObjId = time.TimeObjId,
                            TimeObj = new TimeObject
                            {
                                Id = time.TimeObjId,
                                Name = time.TimeObjName,
                                EndTime = time.EndTime
                            }
                        });
                    }
                    ruleTimesProp.SetValue(rule, ruleTimes);

                    rowType.GetProperty("Rule")?.SetValue(row, rule);
                    resultList.Add(row);
                }
                return resultList;
            }
        }

        private sealed class ExpiredRuleEntryInput
        {
            public int OwnerId { get; set; }
            public string OwnerName { get; set; } = "";
            public string OwnerExtAppId { get; set; } = "";
            public long RuleId { get; set; }
            public string RuleUid { get; set; } = "";
            public string RuleName { get; set; } = "";
            public int RuleNumber { get; set; }
            public int ManagementId { get; set; }
            public string RulebaseName { get; set; } = "";
            public string SourceShort { get; set; } = "";
            public string SourceLong { get; set; } = "";
            public string DestinationShort { get; set; } = "";
            public string DestinationLong { get; set; } = "";
            public string ServiceShort { get; set; } = "";
            public string ServiceLong { get; set; } = "";
            public string CustomFields { get; set; } = "";
            public DateTime? LastHit { get; set; }
            public List<ExpiredTimeInput> RuleTimes { get; set; } = [];
        }

        private sealed class ExpiredTimeInput
        {
            public long TimeObjId { get; set; }
            public string TimeObjName { get; set; } = "";
            public DateTime EndTime { get; set; }
        }
    }
}
