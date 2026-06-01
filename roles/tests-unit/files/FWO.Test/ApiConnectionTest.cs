using FWO.Api.Client;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class ApiConnectionTest
    {
        [Test]
        public async Task RunWithRoleSwitchesBackAfterSuccessfulAction()
        {
            TrackingApiConnection connection = new();

            await connection.RunWithRole("modeller", async () =>
            {
                Assert.That(connection.ActiveRole, Is.EqualTo("modeller"));
                await Task.CompletedTask;
            });

            Assert.That(connection.ActiveRole, Is.Empty);
            Assert.That(connection.SwitchBackCount, Is.EqualTo(1));
        }

        [Test]
        public void RunWithRoleSwitchesBackAfterException()
        {
            TrackingApiConnection connection = new();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.RunWithRole("modeller", async () =>
            {
                Assert.That(connection.ActiveRole, Is.EqualTo("modeller"));
                await Task.CompletedTask;
                throw new InvalidOperationException("test");
            }));

            Assert.That(connection.ActiveRole, Is.Empty);
            Assert.That(connection.SwitchBackCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RunWithBestRoleReturnsResultAndSwitchesBack()
        {
            TrackingApiConnection connection = new();
            ClaimsPrincipal user = CreateUser("modeller");

            string result = await connection.RunWithBestRole(user, ["modeller"], async () =>
            {
                Assert.That(connection.ActiveRole, Is.EqualTo("modeller"));
                await Task.CompletedTask;
                return "done";
            });

            Assert.That(result, Is.EqualTo("done"));
            Assert.That(connection.ActiveRole, Is.Empty);
            Assert.That(connection.BestRoleCalls, Is.EqualTo(1));
            Assert.That(connection.SwitchBackCount, Is.EqualTo(1));
        }

        [Test]
        public void RunWithBestRoleResultSwitchesBackAfterException()
        {
            TrackingApiConnection connection = new();
            ClaimsPrincipal user = CreateUser("modeller");

            Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.RunWithBestRole(user, ["modeller"], async () =>
            {
                Assert.That(connection.ActiveRole, Is.EqualTo("modeller"));
                await Task.CompletedTask;
                throw new InvalidOperationException("test");
            }));

            Assert.That(connection.ActiveRole, Is.Empty);
            Assert.That(connection.BestRoleCalls, Is.EqualTo(1));
            Assert.That(connection.SwitchBackCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RunWithProperRoleReturnsResultAndSwitchesBack()
        {
            TrackingApiConnection connection = new();
            ClaimsPrincipal user = CreateUser("requester");

            int result = await connection.RunWithProperRole(user, ["requester"], async () =>
            {
                Assert.That(connection.ActiveRole, Is.EqualTo("requester"));
                await Task.CompletedTask;
                return 42;
            });

            Assert.That(result, Is.EqualTo(42));
            Assert.That(connection.ActiveRole, Is.Empty);
            Assert.That(connection.ProperRoleCalls, Is.EqualTo(1));
            Assert.That(connection.SwitchBackCount, Is.EqualTo(1));
        }

        [Test]
        public void RunWithProperRoleSwitchesBackAfterException()
        {
            TrackingApiConnection connection = new();
            ClaimsPrincipal user = CreateUser("requester");

            Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.RunWithProperRole(user, ["requester"], async () =>
            {
                Assert.That(connection.ActiveRole, Is.EqualTo("requester"));
                await Task.CompletedTask;
                throw new InvalidOperationException("test");
            }));

            Assert.That(connection.ActiveRole, Is.Empty);
            Assert.That(connection.ProperRoleCalls, Is.EqualTo(1));
            Assert.That(connection.SwitchBackCount, Is.EqualTo(1));
        }

        [Test]
        public void RunWithProperRoleResultSwitchesBackAfterException()
        {
            TrackingApiConnection connection = new();
            ClaimsPrincipal user = CreateUser("requester");

            Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.RunWithProperRole<int>(user, ["requester"], async () =>
            {
                Assert.That(connection.ActiveRole, Is.EqualTo("requester"));
                await Task.CompletedTask;
                throw new InvalidOperationException("test");
            }));

            Assert.That(connection.ActiveRole, Is.Empty);
            Assert.That(connection.ProperRoleCalls, Is.EqualTo(1));
            Assert.That(connection.SwitchBackCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SendQuerySafeWrapsSendQueryResult()
        {
            TrackingApiConnection connection = new() { QueryResult = "query-result" };

            ApiResponse<string> response = await connection.SendQuerySafeAsync<string>("query");

            Assert.That(response.Result, Is.EqualTo("query-result"));
        }

        [Test]
        public void SetAuthHeaderRaisesEvent()
        {
            TrackingApiConnection connection = new();
            string? receivedHeader = null;
            object? receivedSender = null;
            connection.OnAuthHeaderChanged += (sender, header) =>
            {
                receivedSender = sender;
                receivedHeader = header;
            };

            connection.SetAuthHeader("jwt");

            Assert.That(receivedSender, Is.SameAs(connection));
            Assert.That(receivedHeader, Is.EqualTo("jwt"));
        }

        [Test]
        public void DisposeOnlyDisposesOnce()
        {
            TrackingApiConnection connection = new();

            connection.Dispose();
            connection.Dispose();

            Assert.That(connection.DisposeCount, Is.EqualTo(1));
        }

        private static ClaimsPrincipal CreateUser(string role)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, role)
            ], "test", ClaimTypes.Name, ClaimTypes.Role));
        }

        private sealed class TrackingApiConnection : ApiConnection
        {
            private readonly Stack<string> previousRoles = new();

            public string ActiveRole { get; private set; } = "";
            public int BestRoleCalls { get; private set; }
            public int ProperRoleCalls { get; private set; }
            public int SwitchBackCount { get; private set; }
            public int DisposeCount { get; private set; }
            public object? QueryResult { get; set; }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(
                Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler,
                string subscription, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                return Task.FromResult((QueryResponseType)QueryResult!);
            }

            public override async Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                QueryResponseType result = await SendQueryAsync<QueryResponseType>(query, variables, operationName);
                return new ApiResponse<QueryResponseType>(result);
            }

            public override void SetAuthHeader(string jwt)
            {
                InvokeOnAuthHeaderChanged(this, jwt);
            }

            public override void SetRole(string role)
            {
                previousRoles.Push(ActiveRole);
                ActiveRole = role;
            }

            public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                BestRoleCalls++;
                SetRole(targetRoleList.First());
            }

            public override void SetProperRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                ProperRoleCalls++;
                SetRole(targetRoleList.First());
            }

            public override void SwitchBack()
            {
                SwitchBackCount++;
                ActiveRole = previousRoles.TryPop(out string? previousRole) ? previousRole : "";
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCount++;
            }

            public override void DisposeSubscriptions<T>()
            { }
        }
    }
}
