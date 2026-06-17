using FWO.Api.Client;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ComplianceControllerTest
    {
        [Test]
        public void GetInitialComplianceCheckStatus_ReturnsNotFoundForUnknownJob()
        {
            ComplianceController controller = new(new DummyApiConnection(), new ComplianceCheckStatusTracker());

            var result = controller.GetInitialComplianceCheckStatus("missing");

            Assert.That(result.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.NotFoundResult>());
        }

        [Test]
        public void StartInitialComplianceCheck_ReturnsConflictWhenJobAlreadyActive()
        {
            ComplianceCheckStatusTracker tracker = new();
            tracker.CreateQueuedJob();
            ComplianceController controller = new(new DummyApiConnection(), tracker);

            var result = controller.StartInitialComplianceCheck();

            Assert.That(result.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.ConflictObjectResult>());
        }

        private sealed class DummyApiConnection : ApiConnection
        {
            public override void SetAuthHeader(string jwt) { }
            public override void SetRole(string role) { }
            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SwitchBack() { }
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null) => throw new NotImplementedException();
            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null) => throw new NotImplementedException();
            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null) => throw new NotImplementedException();
            protected override void Dispose(bool disposing) { }
            public override void DisposeSubscriptions<T>() { }
            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct) => throw new NotImplementedException();
        }
    }
}
