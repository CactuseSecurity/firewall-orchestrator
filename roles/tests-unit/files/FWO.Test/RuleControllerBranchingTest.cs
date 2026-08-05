using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Networking;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleControllerBranchingTest
    {
        private static readonly string[] kExpectedSourceRuleNames = ["Source", "MatchingGroup"];
        private static readonly string[] kExpectedBothRuleNames = ["Source", "MatchingGroup", "Destination"];

        [Test]
        public async Task GetRulesByFilter_ShouldWorkWithOwnerId()
        {
            RuleController controller = CreateController(new BranchingApiConnection(), "req-owner");

            ActionResult<RulesByFilterResponse> actionResult = await controller.GetRulesByFilter(
                new RulesByFilterRequest
                {
                    RequestContext = new RequestContext { UserName = "debug", UserID = "42" },
                    Query = new RulesByFilterQuery
                    {
                        OwnerId = 42
                    }
                }, "req-owner");

            RulesByFilterResponse response = ExtractResponse(actionResult);
            ClassicAssert.AreEqual("req-owner", response.RequestId);
            ClassicAssert.AreEqual(1, response.Result.Count);
            ClassicAssert.AreEqual(1, response.Result.Rules[0].OwnerInformation.OwnerIds.Count);
            ClassicAssert.AreEqual(42, response.Result.Rules[0].OwnerInformation.OwnerIds[0]);
            ClassicAssert.AreEqual("owner-from-custom", response.Result.Rules[0].OwnerInformation.ExtAppId);
            ClassicAssert.AreEqual("chg-4711", response.Result.Rules[0].AdditionalInformation.ChangeId);

            string json = JsonSerializer.Serialize(response);
            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement rule = document.RootElement.GetProperty("result").GetProperty("rules")[0];
            JsonElement ownerInformation = rule.GetProperty("ownerInformation");
            ClassicAssert.AreEqual("owner-from-custom", ownerInformation.GetProperty("extAppId").GetString());
            ClassicAssert.AreEqual(1, ownerInformation.GetProperty("ownerIds").GetArrayLength());
            ClassicAssert.AreEqual(42, ownerInformation.GetProperty("ownerIds")[0].GetInt32());
            ClassicAssert.AreEqual("chg-4711", rule.GetProperty("additionalInformation").GetProperty("changeId").GetString());
        }

        [Test]
        public async Task GetRulesByFilter_ShouldWorkWithIpAddress()
        {
            RuleController controller = CreateController(new BranchingApiConnection(includeGroupRule: false), "req-ip");

            ActionResult<RulesByFilterResponse> actionResult = await controller.GetRulesByFilter(
                new RulesByFilterRequest
                {
                    RequestContext = new RequestContext { UserName = "debug", UserID = "42" },
                    Query = new RulesByFilterQuery
                    {
                        IpAddress = "10.1.2.3",
                        Filter = new RuleFilter
                        {
                            Action = "any",
                            MinPrefixLength = 16,
                            InField = "source"
                        }
                    }
                }, "req-ip");

            RulesByFilterResponse response = ExtractResponse(actionResult);
            ClassicAssert.AreEqual("req-ip", response.RequestId);
            ClassicAssert.AreEqual(1, response.Result.Count);
            ClassicAssert.AreEqual(1, response.Result.Rules[0].OwnerInformation.OwnerIds.Count);
            ClassicAssert.AreEqual(42, response.Result.Rules[0].OwnerInformation.OwnerIds[0]);
            ClassicAssert.AreEqual("owner-from-custom", response.Result.Rules[0].OwnerInformation.ExtAppId);
            ClassicAssert.AreEqual("chg-4711", response.Result.Rules[0].AdditionalInformation.ChangeId);
        }

        [Test]
        public async Task GetRulesByFilter_ShouldIncludeRuleWhenAnyFlattenedSourceObjectMatches()
        {
            RuleController controller = CreateController(new BranchingApiConnection(), "req-group");

            ActionResult<RulesByFilterResponse> actionResult = await controller.GetRulesByFilter(
                new RulesByFilterRequest
                {
                    RequestContext = new RequestContext { UserName = "debug", UserID = "42" },
                    Query = new RulesByFilterQuery
                    {
                        IpAddress = "10.1.2.3",
                        Filter = new RuleFilter
                        {
                            Action = "any",
                            MinPrefixLength = 24,
                            InField = "source"
                        }
                    }
                }, "req-group");

            RulesByFilterResponse response = ExtractResponse(actionResult);
            ClassicAssert.AreEqual("req-group", response.RequestId);
            ClassicAssert.AreEqual(2, response.Result.Count);
            CollectionAssert.AreEquivalent(kExpectedSourceRuleNames, response.Result.Rules.Select(rule => rule.Name));
        }

        [Test]
        public async Task GetRulesByFilter_ShouldIncludeRuleWhenDestinationObjectMatches()
        {
            RuleController controller = CreateController(new BranchingApiConnection(includeDestinationRule: true), "req-destination");

            ActionResult<RulesByFilterResponse> actionResult = await controller.GetRulesByFilter(
                new RulesByFilterRequest
                {
                    RequestContext = new RequestContext { UserName = "debug", UserID = "42" },
                    Query = new RulesByFilterQuery
                    {
                        IpAddress = "10.1.2.3",
                        Filter = new RuleFilter
                        {
                            Action = "any",
                            MinPrefixLength = 24,
                            InField = "destination"
                        }
                    }
                }, "req-destination");

            RulesByFilterResponse response = ExtractResponse(actionResult);
            ClassicAssert.AreEqual("req-destination", response.RequestId);
            ClassicAssert.AreEqual(1, response.Result.Count);
            ClassicAssert.AreEqual("Destination", response.Result.Rules[0].Name);
        }

        [Test]
        public async Task GetRulesByFilter_ShouldIncludeRuleWhenOnlyDestinationMatchesAndInFieldIsBoth()
        {
            RuleController controller = CreateController(new BranchingApiConnection(includeDestinationRule: true), "req-both");

            ActionResult<RulesByFilterResponse> actionResult = await controller.GetRulesByFilter(
                new RulesByFilterRequest
                {
                    RequestContext = new RequestContext { UserName = "debug", UserID = "42" },
                    Query = new RulesByFilterQuery
                    {
                        IpAddress = "10.1.2.3",
                        Filter = new RuleFilter
                        {
                            Action = "any",
                            MinPrefixLength = 24,
                            InField = "both"
                        }
                    }
                }, "req-both");

            RulesByFilterResponse response = ExtractResponse(actionResult);
            ClassicAssert.AreEqual("req-both", response.RequestId);
            ClassicAssert.AreEqual(3, response.Result.Count);
            CollectionAssert.AreEquivalent(kExpectedBothRuleNames, response.Result.Rules.Select(rule => rule.Name));
        }

        [Test]
        public void GetRulesByFilter_OwnerLookupQuery_ShouldFilterRemovedRuleOwners()
        {
            StringAssert.Contains("removed: { _is_null: true }", RuleQueries.getRuleIdsByRuleOwner);
        }

        private static RuleController CreateController(ApiConnection apiConnection, string requestId)
        {
            RuleController controller = new(apiConnection);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.ControllerContext.HttpContext.Request.Headers["X-Request-Id"] = requestId;
            return controller;
        }

        private static RulesByFilterResponse ExtractResponse(ActionResult<RulesByFilterResponse> actionResult)
        {
            OkObjectResult okResult = actionResult.Result as OkObjectResult
                ?? throw new AssertionException("Expected OK response.");
            return okResult.Value as RulesByFilterResponse
                ?? throw new AssertionException("Expected rules-by-filter payload.");
        }

        private sealed class BranchingApiConnection : ApiConnection
        {
            private readonly bool _includeDestinationRule;
            private readonly bool _includeGroupRule;

            public BranchingApiConnection(bool includeGroupRule = true, bool includeDestinationRule = false)
            {
                _includeGroupRule = includeGroupRule;
                _includeDestinationRule = includeDestinationRule;
            }

            public override void SetAuthHeader(string jwt)
            {
                throw new NotImplementedException();
            }

            public override void SetRole(string role)
            {
                throw new NotImplementedException();
            }

            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
            {
                throw new NotImplementedException();
            }

            public override void SwitchBack()
            {
                throw new NotImplementedException();
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
                string query,
                object? variables = null,
                string? operationName = null,
                QueryChunkingOptions? options = null)
            {
                if (typeof(QueryResponseType) == typeof(ConfigItem[]) && query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((QueryResponseType)(object)new[]
                    {
                        new ConfigItem { Key = "CustomFieldOwnerKey", Value = @"[""owner_key""]", User = 0 },
                        new ConfigItem { Key = "CustomFieldChangeIdKey", Value = @"[""change_key""]", User = 0 }
                    });
                }

                if (typeof(QueryResponseType) == typeof(Language[]) && query == ConfigQueries.getLanguages)
                {
                    return Task.FromResult((QueryResponseType)(object)new[]
                    {
                        new Language { Name = "English", CultureInfo = "en-US" }
                    });
                }

                if (typeof(QueryResponseType) == typeof(List<UiText>) &&
                    (query == ConfigQueries.getTextsPerLanguage || query == ConfigQueries.getCustomTextsPerLanguage))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<UiText>());
                }

                if (typeof(QueryResponseType) == typeof(List<RuleOwnerItem>) && query == RuleQueries.getRuleIdsByRuleOwner)
                {
                    int ownerId = GetIntVariable(variables, "ownerId");
                    return Task.FromResult((QueryResponseType)(object)(ownerId == 42
                        ? new List<RuleOwnerItem> { new() { RuleId = 101 } }
                        : new List<RuleOwnerItem>()));
                }

                if (typeof(QueryResponseType) == typeof(List<Rule>) && query == RuleQueries.getRuleDetailsById)
                {
                    if (HasVariable(variables, "rule_ids"))
                    {
                        return Task.FromResult((QueryResponseType)(object)new List<Rule>
                        {
                            BuildMatchingRule(101)
                        });
                    }

                    List<Rule> rules =
                    [
                        BuildMatchingRule(101),
                        BuildNonMatchingRule(202)
                    ];

                    if (_includeGroupRule)
                    {
                        rules.Add(BuildGroupRule(303));
                    }

                    if (_includeDestinationRule)
                    {
                        rules.Add(BuildDestinationRule(404));
                    }

                    return Task.FromResult((QueryResponseType)(object)rules);
                }

                throw new NotImplementedException($"Unexpected query: {query}");
            }

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(
                string query,
                object? variables = null,
                string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(
                Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler,
                string subscription,
                object? variables = null,
                string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override void DisposeSubscriptions<T>()
            {
                throw new NotImplementedException();
            }

            protected override void Dispose(bool disposing)
            {
            }

            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
            {
                throw new NotImplementedException();
            }

            private static bool HasVariable(object? variables, string name)
            {
                return variables?.GetType().GetProperty(name) is not null;
            }

            private static int GetIntVariable(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
                return value is int intValue ? intValue : Convert.ToInt32(value);
            }

            private static Rule BuildMatchingRule(long ruleId)
            {
                return new Rule
                {
                    Id = ruleId,
                    Name = "Source",
                    RuleOwner = [new RuleOwner { OwnerId = 42, OwnerMappingSourceId = (int)OwnerMappingSourceStm.CustomField }],
                    Froms =
                    [
                        new NetworkLocation(
                            new NetworkUser { Name = "source" },
                            new NetworkObject
                            {
                                Id = 1,
                                Name = "Source",
                                IP = "10.1.2.3",
                                IpEnd = "10.1.2.3",
                                Type = new NetworkObjectType { Name = ObjectType.Network }
                            })
                    ],
                    Tos =
                    [
                        new NetworkLocation(
                            new NetworkUser { Name = "destination" },
                            new NetworkObject
                            {
                                Id = 2,
                                Name = "Destination",
                                IP = "10.9.9.9",
                                IpEnd = "10.9.9.9",
                                Type = new NetworkObjectType { Name = ObjectType.Network }
                            })
                    ],
                    CustomFields = "{'owner_key':'owner-from-custom','change_key':'chg-4711'}"
                };
            }

            private static Rule BuildNonMatchingRule(long ruleId)
            {
                return new Rule
                {
                    Id = ruleId,
                    RuleOwner = [new RuleOwner { OwnerId = 999, OwnerMappingSourceId = (int)OwnerMappingSourceStm.CustomField }],
                    Froms =
                    [
                        new NetworkLocation(
                            new NetworkUser { Name = "source" },
                            new NetworkObject
                            {
                                Id = 3,
                                Name = "Source",
                                IP = "192.168.1.1",
                                IpEnd = "192.168.1.1",
                                Type = new NetworkObjectType { Name = ObjectType.Network }
                            })
                    ],
                    Tos = [],
                    CustomFields = "{'owner_key':'owner-from-custom','change_key':'chg-4711'}"
                };
            }

            private static Rule BuildGroupRule(long ruleId)
            {
                NetworkObject matchingMember = new()
                {
                    Id = 31,
                    Name = "MatchingMember",
                    IP = "10.1.2.0",
                    IpEnd = "10.1.2.255",
                    Type = new NetworkObjectType { Name = ObjectType.Network }
                };

                NetworkObject broadMember = new()
                {
                    Id = 32,
                    Name = "BroadMember",
                    IP = "10.0.0.0",
                    IpEnd = "10.255.255.255",
                    Type = new NetworkObjectType { Name = ObjectType.Network }
                };

                NetworkObject unsupportedMember = new()
                {
                    Id = 33,
                    Name = "Ipv6Member",
                    IP = "2001:db8::",
                    IpEnd = "2001:db8::ffff",
                    Type = new NetworkObjectType { Name = ObjectType.Network }
                };

                NetworkObject group = new()
                {
                    Id = 30,
                    Name = "MatchingGroup",
                    Type = new NetworkObjectType { Name = ObjectType.Group },
                    ObjectGroupFlats =
                    [
                        new GroupFlat<NetworkObject> { Object = matchingMember },
                        new GroupFlat<NetworkObject> { Object = broadMember },
                        new GroupFlat<NetworkObject> { Object = unsupportedMember }
                    ]
                };

                return new Rule
                {
                    Id = ruleId,
                    Name = "MatchingGroup",
                    RuleOwner = [new RuleOwner { OwnerId = 42, OwnerMappingSourceId = (int)OwnerMappingSourceStm.CustomField }],
                    Froms =
                    [
                        new NetworkLocation(
                            new NetworkUser { Name = "source" },
                            group)
                    ],
                    Tos = [],
                    CustomFields = "{'owner_key':'owner-from-custom','change_key':'chg-4711'}"
                };
            }

            private static Rule BuildDestinationRule(long ruleId)
            {
                return new Rule
                {
                    Id = ruleId,
                    Name = "Destination",
                    RuleOwner = [new RuleOwner { OwnerId = 42, OwnerMappingSourceId = (int)OwnerMappingSourceStm.CustomField }],
                    Froms =
                    [
                        new NetworkLocation(
                            new NetworkUser { Name = "source" },
                            new NetworkObject
                            {
                                Id = 40,
                                Name = "OtherSource",
                                IP = "192.168.1.1",
                                IpEnd = "192.168.1.1",
                                Type = new NetworkObjectType { Name = ObjectType.Network }
                            })
                    ],
                    Tos =
                    [
                        new NetworkLocation(
                            new NetworkUser { Name = "destination" },
                            new NetworkObject
                            {
                                Id = 41,
                                Name = "MatchingDestination",
                                IP = "10.1.2.0",
                                IpEnd = "10.1.2.255",
                                Type = new NetworkObjectType { Name = ObjectType.Network }
                            })
                    ],
                    CustomFields = "{'owner_key':'owner-from-custom','change_key':'chg-4711'}"
                };
            }
        }
    }
}
