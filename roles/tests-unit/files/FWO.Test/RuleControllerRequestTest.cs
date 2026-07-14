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
