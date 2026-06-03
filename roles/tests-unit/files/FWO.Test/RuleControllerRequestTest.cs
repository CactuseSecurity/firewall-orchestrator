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
        [Test]
        public void RulesByFilterRequest_ShouldDeserializeFieldSourceMapping()
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

            ClassicAssert.IsNotNull(request.Query.FieldSourceMapping);
            ClassicAssert.AreEqual(FieldSource.CustomField, request.Query.FieldSourceMapping!.OwnerInformation);
            ClassicAssert.AreEqual(FieldSource.Database, request.Query.FieldSourceMapping.ChangeId);
        }

        [Test]
        public void RulesByFilterRequest_ShouldRejectInvalidFieldSourceValue()
        {
            const string json = """
            {
              "Query": {
                "IpAddress": "10.1.2.3",
                "Filter": {
                  "MinPrefixLength": 16,
                  "InField": "source",
                  "Action": "any"
                },
                "FieldSourceMapping": {
                  "OwnerInformation": "FromDatabase"
                }
              }
            }
            """;

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RulesByFilterRequest>(json));
        }

        [Test]
        public void RulesByFilterRequest_ShouldRejectNumericFieldSourceValue()
        {
            const string json = """
            {
              "Query": {
                "IpAddress": "10.1.2.3",
                "Filter": {
                  "MinPrefixLength": 16,
                  "InField": "source",
                  "Action": "any"
                },
                "FieldSourceMapping": {
                  "ChangeId": 1
                }
              }
            }
            """;

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RulesByFilterRequest>(json));
        }

        [Test]
        public async Task GetRulesByFilter_ShouldRejectOwnerAndIpAddressTogether()
        {
            RuleController controller = new(new DummyApiConnection());

            ActionResult<RulesByFilterResponse> result = await controller.GetRulesByFilter(
                new RulesByFilterRequest
                {
                    Query = new RulesByFilterQuery
                    {
                        OwnerId = 123,
                        IpAddress = "10.1.2.3",
                        Filter = new RuleFilter
                        {
                            MinPrefixLength = 16,
                            InField = "source",
                            Action = "any"
                        }
                    }
                });

            ClassicAssert.IsInstanceOf<BadRequestObjectResult>(result.Result);
            ClassicAssert.AreEqual("Exactly one of OwnerId or IpAddress must be provided.",
                ((BadRequestObjectResult)result.Result!).Value);
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

            public override void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
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
