using System.Text.Json;
using FWO.Middleware.Server.Controllers;
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
    }
}
