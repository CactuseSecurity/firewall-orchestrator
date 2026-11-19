using System.Text.Json;
using FWO.Api.Client;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleControllerRequestTest
    {
        private static readonly JsonSerializerOptions kWebJsonOptions = new(JsonSerializerDefaults.Web);

        [Test]
        public void RulesByFilterRequest_ShouldDeserializeRuleUidUsingWebJsonNaming()
        {
            const string json = """
            {
              "query": {
                "ruleUid": "uid-4711"
              }
            }
            """;

            RulesByFilterRequest request = JsonSerializer.Deserialize<RulesByFilterRequest>(json, kWebJsonOptions)!;

            Assert.That(request.Query.RuleUid, Is.EqualTo("uid-4711"));
        }

        [Test]
        public void RulesByFilterRequest_ShouldIgnoreUnknownMappingField()
        {
            const string json = """
            {
              "RequestContext": {
                "UserName": "debug",
                "UserID": "42"
              },
              "Query": {
                "IpAddress": "10.1.2.3",
                "Filter": {
                  "MinPrefixLength": 16,
                  "InField": "source",
                  "Action": "any"
                },
                "FieldSourceMapping": {
                  "OwnerInformation": "CustomField",
                  "ChangeId": "Database"
                }
              }
            }
            """;

            RulesByFilterRequest request = JsonSerializer.Deserialize<RulesByFilterRequest>(json)!;

            ClassicAssert.AreEqual("debug", request.RequestContext.UserName);
            ClassicAssert.AreEqual("42", request.RequestContext.UserID);
            ClassicAssert.AreEqual("10.1.2.3", request.Query.IpAddress);
            ClassicAssert.AreEqual("any", request.Query.Filter!.Action);
        }

        [Test]
        public void RulesByFilterRequest_ShouldRequireMinPrefixLength()
        {
            const string json = """
            {
              "Query": {
                "IpAddress": "10.1.2.3",
                "Filter": {
                  "InField": "source",
                  "Action": "any"
                }
              }
            }
            """;

            ClassicAssert.Throws<JsonException>(() => JsonSerializer.Deserialize<RulesByFilterRequest>(json));
        }

        [TestCase(123, "10.1.2.3", null)]
        [TestCase(123, null, "uid-4711")]
        [TestCase(null, "10.1.2.3", "uid-4711")]
        [TestCase(123, "10.1.2.3", "uid-4711")]
        public async Task GetRulesByFilter_ShouldRejectMultipleSelectors(int? ownerId, string? ipAddress, string? ruleUid)
        {
            RuleController controller = new(new DummyApiConnection(), new SimulatedGlobalConfig());

            ActionResult<RulesByFilterResponse> result = await controller.GetRulesByFilter(
                new RulesByFilterRequest
                {
                    Query = new RulesByFilterQuery
                    {
                        OwnerId = ownerId,
                        IpAddress = ipAddress,
                        RuleUid = ruleUid,
                        Filter = new RuleFilter
                        {
                            MinPrefixLength = 16,
                            InField = "source",
                            Action = "any"
                        }
                    }
                });

            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value,
                Is.EqualTo("Exactly one of OwnerId, IpAddress or RuleUid must be provided."));
        }

        [TestCase(null, null)]
        [TestCase("", null)]
        [TestCase(" ", "\t")]
        public async Task GetRulesByFilter_ShouldRejectMissingOrBlankSelectors(string? ipAddress, string? ruleUid)
        {
            RuleController controller = new(new DummyApiConnection(), new SimulatedGlobalConfig());

            ActionResult<RulesByFilterResponse> result = await controller.GetRulesByFilter(
                new RulesByFilterRequest
                {
                    Query = new RulesByFilterQuery
                    {
                        IpAddress = ipAddress,
                        RuleUid = ruleUid
                    }
                });

            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value,
                Is.EqualTo("Either OwnerId, IpAddress or RuleUid must be provided."));
        }

        private sealed class DummyApiConnection : ApiConnection
        {
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

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? options = null)
            {
                throw new NotImplementedException();
            }

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
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

            protected override void Dispose(bool disposing)
            {
            }

            public override void DisposeSubscriptions<T>()
            {
                throw new NotImplementedException();
            }

            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
            {
                throw new NotImplementedException();
            }
        }
    }
}
