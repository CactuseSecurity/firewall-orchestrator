using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleControllerRuleTypeTest
    {
        private const long kAccessRuleId = 101;
        private const long kNatRuleId = 201;
        private const long kTranslationRowId = 202;
        private const int kOwnerId = 42;
        private const string kMatchingIp = "10.1.2.3";
        private const string kSharedUid = "uid-shared";
        private const string kRuleTypesFilterVariable = "rule_type_filters";
        private static readonly List<string?> kNatOnly = [RuleTypeFilter.kNat];
        private static readonly List<string?> kAccessAndNat = [RuleTypeFilter.kAccess, RuleTypeFilter.kNat];
        private static readonly List<string?> kInvalidRuleTypes = ["xlate"];
        private static readonly List<string?> kEmptyRuleTypes = [];
        private static readonly List<string> kAccessOnlyTypes = [RuleTypeFilter.kAccess];
        private static readonly List<string> kNatOnlyTypes = [RuleTypeFilter.kNat];
        private static readonly List<string> kAccessAndNatTypes = [RuleTypeFilter.kAccess, RuleTypeFilter.kNat];
        private static readonly List<string> kSelectors = ["owner", "ip", "uid"];
        private static readonly List<string> kAccessRuleConditionKeys = ["access_rule"];
        private static readonly Language[] kLanguages = [new Language { Name = "English", CultureInfo = "en-US" }];
        private static readonly System.Text.Json.JsonSerializerOptions kWebJsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

        [TestCaseSource(nameof(kSelectors))]
        public async Task GetRulesByFilter_WithoutRuleTypes_ReturnsNoNatRules(string selector)
        {
            RuleTypeApiConnection apiConnection = new();
            RuleController controller = CreateController(apiConnection);

            RulesByFilterResponse response = ExtractResponse(await controller.GetRulesByFilter(BuildRequest(selector, null)));

            Assert.Multiple(() =>
            {
                Assert.That(response.Result.Rules.Select(rule => rule.RuleType), Is.EqualTo(kAccessOnlyTypes));
                Assert.That(response.Result.Rules.Single().Translation, Is.Null);
                Assert.That(HasMoreHeader(controller), Is.EqualTo("false"));
            });
        }

        [TestCaseSource(nameof(kSelectors))]
        public async Task GetRulesByFilter_WithNatRuleType_ReturnsOnlyNatRulesWithTranslation(string selector)
        {
            RuleTypeApiConnection apiConnection = new();
            RuleController controller = CreateController(apiConnection);

            RulesByFilterResponse response = ExtractResponse(await controller.GetRulesByFilter(BuildRequest(selector, kNatOnly)));

            RuleDetail natRule = response.Result.Rules.Single();
            Assert.Multiple(() =>
            {
                Assert.That(natRule.RuleType, Is.EqualTo(RuleTypeFilter.kNat));
                Assert.That(natRule.Name, Is.EqualTo("NatRule"));
                Assert.That(natRule.Source.Single().Name, Is.EqualTo("OriginalSource"));
                Assert.That(natRule.Translation, Is.Not.Null);
                Assert.That(natRule.Translation!.Source.Single().Name, Is.EqualTo("TranslatedSource"));
                Assert.That(natRule.Translation.Source.Single().Ip, Does.Contain("192.0.2.10"));
                Assert.That(natRule.Translation.Destination.Single().Name, Is.EqualTo("TranslatedDestination"));
                Assert.That(natRule.Translation.Service.Single().Name, Is.EqualTo("translated-https"));
                Assert.That(natRule.Translation.Service.Single().Port, Is.EqualTo(8443));
                Assert.That(natRule.Translation.SourceShort, Does.Contain("TranslatedSource"));
            });
        }

        [TestCaseSource(nameof(kSelectors))]
        public async Task GetRulesByFilter_WithAccessAndNat_ReturnsBothButNeverTheTranslationRow(string selector)
        {
            RuleTypeApiConnection apiConnection = new();
            RuleController controller = CreateController(apiConnection);

            RulesByFilterResponse response = ExtractResponse(await controller.GetRulesByFilter(BuildRequest(selector, kAccessAndNat)));

            Assert.Multiple(() =>
            {
                Assert.That(response.Result.Rules.Select(rule => rule.RuleType), Is.EqualTo(kAccessAndNatTypes));
                Assert.That(response.Result.Count, Is.EqualTo(2));
                Assert.That(response.Result.Rules.Select(rule => rule.Name), Has.None.EqualTo("TranslationRow"));
            });
        }

        [Test]
        public async Task GetRulesByFilter_SendsRuleTypeFilterToTheQuery()
        {
            RuleTypeApiConnection apiConnection = new();
            RuleController controller = CreateController(apiConnection);

            await controller.GetRulesByFilter(BuildRequest("uid", null));

            Dictionary<string, object> expression = apiConnection.LastRuleTypeFilters!.Single();
            Assert.That(expression.Keys, Is.EquivalentTo(kAccessRuleConditionKeys));
        }

        [Test]
        public async Task GetRulesByFilter_AppliesRuleTypeFilterBeforePaging()
        {
            RuleTypeApiConnection apiConnection = new();
            RuleController firstPageController = CreateController(apiConnection);
            RuleController secondPageController = CreateController(apiConnection);

            RulesByFilterResponse firstPage = ExtractResponse(await firstPageController.GetRulesByFilter(
                BuildRequest("owner", kAccessAndNat, limit: 1, offset: 0)));
            RulesByFilterResponse secondPage = ExtractResponse(await secondPageController.GetRulesByFilter(
                BuildRequest("owner", kAccessAndNat, limit: 1, offset: 1)));

            Assert.Multiple(() =>
            {
                Assert.That(firstPage.Result.Rules.Select(rule => rule.RuleType), Is.EqualTo(kAccessOnlyTypes));
                Assert.That(HasMoreHeader(firstPageController), Is.EqualTo("true"));
                Assert.That(secondPage.Result.Rules.Select(rule => rule.RuleType), Is.EqualTo(kNatOnlyTypes));
                Assert.That(HasMoreHeader(secondPageController), Is.EqualTo("false"));
            });
        }

        [TestCaseSource(nameof(kSelectors))]
        public async Task GetRulesByFilter_InvalidRuleType_ReturnsBadRequest(string selector)
        {
            RuleTypeApiConnection apiConnection = new();
            RuleController controller = CreateController(apiConnection);

            ActionResult<RulesByFilterResponse> result = await controller.GetRulesByFilter(BuildRequest(selector, kInvalidRuleTypes));

            BadRequestObjectResult? badRequest = result.Result as BadRequestObjectResult;
            Assert.Multiple(() =>
            {
                Assert.That(badRequest, Is.Not.Null);
                Assert.That(badRequest?.Value?.ToString(), Does.StartWith("Invalid filter: options.filter.ruleTypes"));
                Assert.That(apiConnection.RuleQueryCount, Is.Zero);
            });
        }

        [Test]
        public async Task GetRulesByFilter_EmptyRuleTypes_ReturnsBadRequest()
        {
            RuleController controller = CreateController(new RuleTypeApiConnection());

            ActionResult<RulesByFilterResponse> result = await controller.GetRulesByFilter(BuildRequest("uid", kEmptyRuleTypes));

            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GetRulesByFilter_ReportsSelectorPagingAndRuleTypeErrorsTogether()
        {
            RuleController controller = CreateController(new RuleTypeApiConnection());
            RulesByFilterRequest request = BuildRequest("uid", kInvalidRuleTypes, limit: 0);
            request.Query.OwnerId = kOwnerId;

            ActionResult<RulesByFilterResponse> result = await controller.GetRulesByFilter(request);

            string message = (result.Result as BadRequestObjectResult)?.Value?.ToString() ?? "";
            Assert.Multiple(() =>
            {
                Assert.That(message, Does.StartWith("Exactly one of OwnerId, IpAddress or RuleUid must be provided."));
                Assert.That(message, Does.Contain("Invalid paging:"));
                Assert.That(message, Does.Contain("Invalid filter: options.filter.ruleTypes"));
            });
        }

        [Test]
        public async Task GetRulesByFilter_NullOptionsFilter_ReturnsAccessRulesOnly()
        {
            RuleController controller = CreateController(new RuleTypeApiConnection());
            RulesByFilterRequest request = BuildRequest("ip", null);
            request.Options = new RulesByFilterOptions { Filter = null };

            RulesByFilterResponse response = ExtractResponse(await controller.GetRulesByFilter(request));

            Assert.That(response.Result.Rules.Select(rule => rule.RuleType), Is.EqualTo(kAccessOnlyTypes));
        }

        [Test]
        public void RulesByFilterRequest_DeserializesRuleTypesUsingWebJsonNaming()
        {
            const string json = "{\"query\":{\"ruleUid\":\"x\"},\"options\":{\"filter\":{\"ruleTypes\":[\"access\",\"nat\"]}}}";

            RulesByFilterRequest request = System.Text.Json.JsonSerializer.Deserialize<RulesByFilterRequest>(json, kWebJsonOptions)!;

            Assert.That(request.Options?.Filter?.RuleTypes, Is.EqualTo(kAccessAndNat));
        }

        [Test]
        public void RuleDetail_OmitsTranslationForAccessRulesInJson()
        {
            string accessJson = System.Text.Json.JsonSerializer.Serialize(new RuleDetail { RuleType = RuleTypeFilter.kAccess });
            string natJson = System.Text.Json.JsonSerializer.Serialize(new RuleDetail
            {
                RuleType = RuleTypeFilter.kNat,
                Translation = new RuleTranslation()
            });

            Assert.Multiple(() =>
            {
                Assert.That(accessJson, Does.Contain("\"ruleType\":\"access\"").And.Not.Contain("translation"));
                Assert.That(natJson, Does.Contain("\"ruleType\":\"nat\"").And.Contain("\"translation\":{"));
            });
        }

        [Test]
        public void BuildSiemEntry_LogsRuleTypesBounded()
        {
            string longValue = new('n', 100);
            List<string?> ruleTypes = [RuleTypeFilter.kNat, null, longValue, "extra"];
            RulesByFilterRequest request = BuildRequest("uid", ruleTypes);

            string entry = RuleController.BuildSiemEntry(request, "req", null, "rejected", DateTime.UnixEpoch);

            Assert.Multiple(() =>
            {
                Assert.That(entry, Does.Contain($"RuleTypes: [\"nat\", \"\", \"{new string('n', 32)}\"]"));
                Assert.That(entry, Does.Not.Contain("extra"));
                Assert.That(entry, Does.EndWith("Result: rejected"));
            });
        }

        [Test]
        public void BuildSiemEntry_OmitsRuleTypesWhenNotSupplied()
        {
            string entry = RuleController.BuildSiemEntry(BuildRequest("uid", null), "req", null, "ok", DateTime.UnixEpoch);

            Assert.That(entry, Does.Not.Contain("RuleTypes"));
        }

        private static RulesByFilterRequest BuildRequest(string selector, List<string?>? ruleTypes, int? limit = null, int? offset = null)
        {
            RulesByFilterQuery query = selector switch
            {
                "owner" => new RulesByFilterQuery { OwnerId = kOwnerId },
                "ip" => new RulesByFilterQuery
                {
                    IpAddress = kMatchingIp,
                    Filter = new RuleFilter { Action = RuleActions.Any, MinPrefixLength = 0, InField = "source" }
                },
                _ => new RulesByFilterQuery { RuleUid = kSharedUid }
            };

            return new RulesByFilterRequest
            {
                Query = query,
                Options = new RulesByFilterOptions
                {
                    Limit = limit,
                    Offset = offset,
                    Filter = new RulesByFilterOptionsFilter { RuleTypes = ruleTypes }
                }
            };
        }

        private static RuleController CreateController(ApiConnection apiConnection)
        {
            RuleController controller = new(apiConnection, new SimulatedGlobalConfig())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            return controller;
        }

        private static RulesByFilterResponse ExtractResponse(ActionResult<RulesByFilterResponse> actionResult)
        {
            OkObjectResult okResult = actionResult.Result as OkObjectResult
                ?? throw new AssertionException($"Expected OK response, got {actionResult.Result}.");
            return okResult.Value as RulesByFilterResponse
                ?? throw new AssertionException("Expected rules-by-filter payload.");
        }

        private static string HasMoreHeader(ControllerBase controller)
        {
            return controller.HttpContext.Response.Headers["X-Has-More"].ToString();
        }

        /// <summary>
        /// Simulates the rule queries over one access rule and one NAT rule (original and translated row), all with the
        /// same UID, owner and matching original source, and evaluates the rule type expressions sent by the controller.
        /// </summary>
        private sealed class RuleTypeApiConnection : SimulatedApiConnection
        {
            private readonly List<Rule> rules = [BuildAccessRule(), BuildNatRule(), BuildTranslationRow()];

            public List<Dictionary<string, object>>? LastRuleTypeFilters { get; private set; }
            public int RuleQueryCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                object response = query switch
                {
                    _ when query == ConfigQueries.getConfigItemsByUser => Array.Empty<ConfigItem>(),
                    _ when query == ConfigQueries.getLanguages => kLanguages,
                    _ when query == ConfigQueries.getTextsPerLanguage || query == ConfigQueries.getCustomTextsPerLanguage => new List<UiText>(),
                    _ when query == RuleQueries.getRuleIdsByRuleOwner || query == RuleQueries.getRuleIdsByRuleUid
                        => SelectRules(variables).Select(rule => new RuleOwnerItem { RuleId = rule.Id }).ToList(),
                    _ when query == RuleQueries.getRuleDetailsById => SelectRuleDetails(variables),
                    _ => throw new NotImplementedException($"Unexpected query: {query}")
                };
                return Task.FromResult((QueryResponseType)response);
            }

            private List<Rule> SelectRules(object? variables)
            {
                ++RuleQueryCount;
                LastRuleTypeFilters = GetVariable<List<Dictionary<string, object>>>(variables, kRuleTypesFilterVariable);
                return rules.Where(rule => LastRuleTypeFilters is null || LastRuleTypeFilters.All(expression => Matches(rule, expression))).ToList();
            }

            private List<Rule> SelectRuleDetails(object? variables)
            {
                List<long>? ruleIds = GetVariable<List<long>>(variables, "rule_ids");
                if (ruleIds is null)
                {
                    return SelectRules(variables);
                }
                return rules.Where(rule => ruleIds.Contains(rule.Id)).ToList();
            }

            private static T? GetVariable<T>(object? variables, string name) where T : class
            {
                return variables?.GetType().GetProperty(name)?.GetValue(variables) as T;
            }

            private static bool Matches(Rule rule, Dictionary<string, object> expression)
            {
                return expression.All(condition => condition.Key switch
                {
                    "_or" => ((List<Dictionary<string, object>>)condition.Value).Any(alternative => Matches(rule, alternative)),
                    "access_rule" => rule.AccessRule == GetComparison(condition.Value, "_eq"),
                    "nat_rule" => rule.NatRule == GetComparison(condition.Value, "_eq"),
                    "xlate_rule" => (rule.XlateRule is null) == GetComparison(condition.Value, "_is_null"),
                    _ => throw new NotImplementedException($"Unexpected rule type condition: {condition.Key}")
                });
            }

            private static bool GetComparison(object comparison, string comparisonOperator)
            {
                return (bool)((Dictionary<string, object>)comparison)[comparisonOperator];
            }

            private static Rule BuildAccessRule()
            {
                return new Rule
                {
                    Id = kAccessRuleId,
                    Uid = kSharedUid,
                    Name = "AccessRule",
                    AccessRule = true,
                    Froms = [BuildLocation(1, "AccessSource", kMatchingIp)]
                };
            }

            private static Rule BuildNatRule()
            {
                return new Rule
                {
                    Id = kNatRuleId,
                    Uid = kSharedUid,
                    Name = "NatRule",
                    AccessRule = false,
                    NatRule = true,
                    XlateRule = kTranslationRowId,
                    Froms = [BuildLocation(2, "OriginalSource", kMatchingIp)],
                    TranslatedRule = new Rule
                    {
                        Froms = [BuildLocation(3, "TranslatedSource", "192.0.2.10")],
                        Tos = [BuildLocation(4, "TranslatedDestination", "198.51.100.20")],
                        Services =
                        [
                            new ServiceWrapper
                            {
                                Content = new NetworkService
                                {
                                    Id = 5,
                                    Name = "translated-https",
                                    DestinationPort = 8443,
                                    DestinationPortEnd = 8443,
                                    Protocol = new NetworkProtocol { Name = "tcp" },
                                    Type = new NetworkServiceType { Name = "simple" }
                                }
                            }
                        ]
                    }
                };
            }

            private static Rule BuildTranslationRow()
            {
                return new Rule
                {
                    Id = kTranslationRowId,
                    Uid = kSharedUid,
                    Name = "TranslationRow",
                    AccessRule = false,
                    NatRule = true,
                    XlateRule = null,
                    Froms = [BuildLocation(6, "TranslationRowSource", kMatchingIp)]
                };
            }

            private static NetworkLocation BuildLocation(long id, string name, string ip)
            {
                return new NetworkLocation(new NetworkUser(), new NetworkObject
                {
                    Id = id,
                    Name = name,
                    IP = ip,
                    IpEnd = ip,
                    Type = new NetworkObjectType { Name = ObjectType.Host }
                });
            }
        }
    }
}
