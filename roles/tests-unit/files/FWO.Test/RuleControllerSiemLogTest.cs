using System.Security.Claims;
using FWO.Middleware.Server.Controllers;
using FWO.Test.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RuleControllerSiemLogTest
    {
        private static readonly DateTime kTimestamp = new(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc);
        private const string kTimestampText = "2026-10-08T14:00:00.0000000Z";
        private static readonly List<Claim> kNameOnlyClaims = [new Claim(ClaimTypes.Name, "fallback-user")];

        [Test]
        public void BuildSiemEntry_ShouldLogJwtCallerAndQuotedRequestUserForOwnerQuery()
        {
            RulesByFilterRequest request = CreateRequest("alice", "42");
            request.Query.OwnerId = 7;

            string entry = RuleController.BuildSiemEntry(request, "req-1", CreateCaller("portal-svc", "17"), "ok", kTimestamp);

            ClassicAssert.AreEqual(
                $"DateTime: {kTimestampText}, RequestId: \"req-1\", CallerName: \"portal-svc\", CallerId: 17, "
                + "UserID: \"42\", UserName: \"alice\", OwnerId: 7, Result: ok",
                entry);
        }

        [Test]
        public void BuildSiemEntry_ShouldLogIpAddressAndFilter()
        {
            RulesByFilterRequest request = CreateRequest("alice", "42");
            request.Query.IpAddress = "10.1.2.3";
            request.Query.Filter = new RuleFilter { MinPrefixLength = 24, InField = "source", Action = "accept" };

            string entry = RuleController.BuildSiemEntry(request, "req-2", CreateCaller("portal-svc", "17"), "ok", kTimestamp);

            StringAssert.EndsWith(
                "IpAddress: \"10.1.2.3\", Filter: {MinPrefixLength: 24, InField: \"source\", Action: \"accept\"}, Result: ok",
                entry);
        }

        [Test]
        public void BuildSiemEntry_ShouldLogIpAddressWhenFilterIsMissing()
        {
            RulesByFilterRequest request = CreateRequest("alice", "42");
            request.Query.IpAddress = "10.1.2.3";

            string entry = RuleController.BuildSiemEntry(request, "req-3", CreateCaller("portal-svc", "17"), "rejected", kTimestamp);

            StringAssert.EndsWith("UserName: \"alice\", IpAddress: \"10.1.2.3\", Result: rejected", entry);
            StringAssert.DoesNotContain("Filter:", entry);
        }

        [Test]
        public void BuildSiemEntry_ShouldQuoteInjectedSeparatorsAndQuotes()
        {
            RulesByFilterRequest request = CreateRequest("alice\", OwnerId: 7", "42");
            request.Query.OwnerId = 1;

            string entry = RuleController.BuildSiemEntry(request, "req-4, Result: ok", CreateCaller("portal-svc", "17"), "rejected", kTimestamp);

            StringAssert.Contains("RequestId: \"req-4, Result: ok\"", entry);
            StringAssert.Contains("UserName: \"alice\\\", OwnerId: 7\"", entry);
            StringAssert.EndsWith("OwnerId: 1, Result: rejected", entry);
        }

        [Test]
        public void BuildSiemEntry_ShouldEscapeControlCharactersAndKeepNonAsciiReadable()
        {
            RulesByFilterRequest request = CreateRequest("Müller\nUserName: eve", "42");
            request.Query.OwnerId = 1;

            string entry = RuleController.BuildSiemEntry(request, "req-5", CreateCaller("portal-svc", "17"), "ok", kTimestamp);

            StringAssert.Contains("UserName: \"Müller\\nUserName: eve\"", entry);
            StringAssert.DoesNotContain("\n", entry);
        }

        [Test]
        public void BuildSiemEntry_ShouldFallBackToIdentityNameAndZeroCallerId()
        {
            RulesByFilterRequest request = CreateRequest("alice", "42");
            request.Query.OwnerId = 1;
            ClaimsPrincipal caller = new(new ClaimsIdentity(kNameOnlyClaims, "test"));

            string entry = RuleController.BuildSiemEntry(request, "req-6", caller, "ok", kTimestamp);

            StringAssert.Contains("CallerName: \"fallback-user\", CallerId: 0,", entry);
        }

        [Test]
        public void BuildSiemEntry_ShouldLogEmptyCallerForAnonymousPrincipal()
        {
            RulesByFilterRequest request = CreateRequest("alice", "42");
            request.Query.OwnerId = 1;

            string entry = RuleController.BuildSiemEntry(request, "req-7", new ClaimsPrincipal(), "error", kTimestamp);

            StringAssert.Contains("CallerName: \"\", CallerId: 0,", entry);
        }

        [Test]
        public void BuildSiemEntry_ShouldLogEmptyCallerWithoutPrincipal()
        {
            RulesByFilterRequest request = CreateRequest("alice", "42");
            request.Query.OwnerId = 1;

            string entry = RuleController.BuildSiemEntry(request, "req-8", null, "error", kTimestamp);

            StringAssert.Contains("CallerName: \"\", CallerId: 0,", entry);
        }

        [Test]
        public async Task GetRulesByFilter_ShouldLogRejectedResultWhenFilterSelectionIsInvalid()
        {
            RuleController controller = new(new SimulatedApiConnection())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = CreateCaller("portal-svc", "17") } }
            };
            RulesByFilterRequest request = CreateRequest("alice", "42");
            ActionResult<RulesByFilterResponse>? actionResult = null;

            string output = await ConsoleOutput.CaptureAsync(async () =>
            {
                actionResult = await controller.GetRulesByFilter(request, "req-siem-rejected");
            });

            ClassicAssert.IsInstanceOf<BadRequestObjectResult>(actionResult?.Result);
            StringAssert.Contains("Log type: portal-application", output);
            StringAssert.Contains("RequestId: \"req-siem-rejected\", CallerName: \"portal-svc\", CallerId: 17,", output);
            StringAssert.Contains("UserName: \"alice\", Result: rejected", output);
        }

        private static RulesByFilterRequest CreateRequest(string userName, string userId)
        {
            return new RulesByFilterRequest
            {
                RequestContext = new RequestContext { UserName = userName, UserID = userId },
                Query = new RulesByFilterQuery()
            };
        }

        private static ClaimsPrincipal CreateCaller(string name, string userId)
        {
            List<Claim> claims =
            [
                new Claim("unique_name", name),
                new Claim("x-hasura-user-id", userId)
            ];
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }
    }
}
