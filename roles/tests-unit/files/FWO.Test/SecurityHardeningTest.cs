using FWO.Api.Client;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using GraphQL;
using GraphQL.Client.Http;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class SecurityHardeningTest
    {
        [Test]
        public void EscapeFilterValue_EscapesRfc4515SpecialCharacters()
        {
            string escaped = Ldap.EscapeFilterValue(@"a*b(c)d\e" + '\0');

            Assert.That(escaped, Is.EqualTo(@"a\2ab\28c\29d\5ce\00"));
        }

        [Test]
        public void EscapeSearchPattern_PreservesWildcardsButEscapesSegments()
        {
            // '*' stays a wildcard; the literal '(' and ')' within segments are escaped
            string escaped = Ldap.EscapeSearchPattern("*smith*(test)");

            Assert.That(escaped, Is.EqualTo(@"*smith*\28test\29"));
        }

        [Test]
        public void EscapeSearchPattern_NeutralizesFilterInjectionWhileKeepingWildcards()
        {
            // An injection attempt that abuses parentheses must be escaped,
            // while the user-supplied leading/trailing wildcards keep working
            string escaped = Ldap.EscapeSearchPattern("*)(uid=admin)(*");

            Assert.That(escaped, Is.EqualTo(@"*\29\28uid=admin\29\28*"));
            Assert.That(escaped, Does.Not.Contain(")("));
        }

        [Test]
        public void EscapeSearchPattern_ReturnsEmptyForNullOrEmpty()
        {
            Assert.That(Ldap.EscapeSearchPattern(null), Is.EqualTo(""));
            Assert.That(Ldap.EscapeSearchPattern(""), Is.EqualTo(""));
        }

        [Test]
        public void DisplayAllComments_AsMarkupHtmlEncodesUserControlledText()
        {
            List<WfCommentDataHelper> comments =
            [
                new(new WfComment
                {
                    CreationDate = new DateTime(2026, 5, 31),
                    Creator = new UiUser { Name = "<img src=x>" },
                    CommentText = "<script>alert(1)</script>"
                })
            ];

            string rendered = WfStatefulObject.DisplayAllComments(comments, asMarkup: true);

            Assert.That(rendered, Does.Contain("&lt;img src=x&gt;"));
            Assert.That(rendered, Does.Contain("&lt;script&gt;alert(1)&lt;/script&gt;"));
            Assert.That(rendered, Does.Not.Contain("<script>"));
        }

        [Test]
        public async Task GraphQlApiConnection_RoleStateIsIsolatedPerAsyncFlow()
        {
            using GraphQlApiConnection connection = new("http://localhost");

            Task<string> modellerTask = Task.Run(async () =>
            {
                connection.SetRole("modeller");
                await Task.Delay(25);
                string role = connection.GetActRole();
                connection.SwitchBack();
                return role;
            });

            Task<string> auditorTask = Task.Run(async () =>
            {
                connection.SetRole("auditor");
                await Task.Delay(25);
                string role = connection.GetActRole();
                connection.SwitchBack();
                return role;
            });

            string[] roles = await Task.WhenAll(modellerTask, auditorTask);

            Assert.That(roles, Is.EquivalentTo(new[] { "modeller", "auditor" }));
            Assert.That(connection.GetActRole(), Is.Empty);
        }

        [Test]
        public void GraphQlApiConnection_SetBestRoleDoesNotMutateSharedDefaultHeaders()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "admin")
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetBestRole(user, ["admin"]);
            GraphQLHttpClient client = GetGraphQlClient(connection);

            Assert.That(connection.GetActRole(), Is.EqualTo("admin"));
            Assert.That(client.HttpClient.DefaultRequestHeaders.Contains("x-hasura-role"), Is.False);
        }

        [Test]
        public void GraphQlApiConnection_SubscriptionRequestCarriesActiveRole()
        {
            using GraphQlApiConnection connection = new("http://localhost");

            connection.SetRole("modeller");
            GraphQLRequest request = CreateSubscriptionRequest(connection, "subscription Test { test }", null, "Test");

            Assert.That(request.Extensions, Is.TypeOf<Dictionary<string, object?>>());
            Dictionary<string, object?> extensions = (Dictionary<string, object?>)request.Extensions!;
            Dictionary<string, object?> headers = extensions["headers"] as Dictionary<string, object?>
                ?? throw new InvalidOperationException("Subscription extensions headers have unexpected type.");
            Assert.That(headers["x-hasura-role"], Is.EqualTo("modeller"));
        }

        [Test]
        public void GraphQlApiConnection_WebSocketInitPayloadCarriesActiveRole()
        {
            string jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims:
            [
                new Claim("x-hasura-default-role", "middleware-server")
            ]));
            using GraphQlApiConnection connection = new("http://localhost", jwt);

            connection.SetRole("auditor");
            GraphQLHttpClient client = GetGraphQlClient(connection);
            object? payload = client.Options.ConfigureWebSocketConnectionInitPayload!(client.Options);

            Dictionary<string, object?> payloadDictionary = payload as Dictionary<string, object?>
                ?? throw new InvalidOperationException("Websocket init payload has unexpected type.");
            Dictionary<string, object?> headers = payloadDictionary["headers"] as Dictionary<string, object?>
                ?? throw new InvalidOperationException("Websocket init payload headers have unexpected type.");
            Assert.That(headers["authorization"], Is.EqualTo($"Bearer {jwt}"));
            Assert.That(headers["x-hasura-role"], Is.EqualTo("auditor"));
        }

        [Test]
        public void GraphQlApiConnection_UsesJwtDefaultRoleAsBaselineRole()
        {
            string jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims:
            [
                new Claim("x-hasura-default-role", "middleware-server")
            ]));

            using GraphQlApiConnection connection = new("http://localhost", jwt);

            Assert.That(connection.GetActRole(), Is.EqualTo("middleware-server"));

            connection.SetRole("admin");
            Assert.That(connection.GetActRole(), Is.EqualTo("admin"));

            connection.SwitchBack();
            Assert.That(connection.GetActRole(), Is.EqualTo("middleware-server"));
        }

        [Test]
        public void GraphQlApiConnection_MultiRoleUserUsesJwtDefaultRoleWithoutAmbientRole()
        {
            string jwt = CreateJwt("reporter", ["reporter", "modeller"]);
            using GraphQlApiConnection connection = new("http://localhost", jwt);

            GraphQLRequest request = CreateSubscriptionRequest(connection, "subscription Test { test }", null, "Test");

            Dictionary<string, object?> extensions = (Dictionary<string, object?>)request.Extensions!;
            Dictionary<string, object?> headers = extensions["headers"] as Dictionary<string, object?>
                ?? throw new InvalidOperationException("Subscription extensions headers have unexpected type.");
            Assert.That(headers["x-hasura-role"], Is.EqualTo("reporter"));
        }

        [Test]
        public void GraphQlApiConnection_NormalModeDoesNotUseElevatedJwtDefaultRole()
        {
            string jwt = CreateJwt(Roles.Auditor, [Roles.Auditor, Roles.Modeller]);
            ClaimsPrincipal user = CreateUser([Roles.Auditor, Roles.Modeller]);
            using GraphQlApiConnection connection = new("http://localhost", jwt);
            connection.SetExecutionMode(user, GlobalConst.kUserRolesSelection);

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                CreateSubscriptionRequest(connection, "subscription Test { test }", null, "Test"))!;

            Assert.That(exception.InnerException, Is.TypeOf<AuthenticationException>());
        }

        [Test]
        public void GraphQlApiConnection_MultiRoleUserAllowsExplicitRequestRole()
        {
            string jwt = CreateJwt("reporter", ["reporter", "modeller"]);
            using GraphQlApiConnection connection = new("http://localhost", jwt);
            connection.SetRole("modeller");

            GraphQLRequest request = CreateSubscriptionRequest(connection, "subscription Test { test }", null, "Test");

            Dictionary<string, object?> extensions = (Dictionary<string, object?>)request.Extensions!;
            Dictionary<string, object?> headers = extensions["headers"] as Dictionary<string, object?>
                ?? throw new InvalidOperationException("Subscription extensions headers have unexpected type.");
            Assert.That(headers["x-hasura-role"], Is.EqualTo("modeller"));
        }

        [Test]
        public void GraphQlApiConnection_MultiRoleUserAllowsAmbientRequestRole()
        {
            string jwt = CreateJwt("reporter", ["reporter", "modeller"]);
            ClaimsPrincipal user = CreateUser(["reporter", "modeller"]);
            using GraphQlApiConnection connection = new("http://localhost", jwt);
            connection.SetAmbientRole(user, ["modeller"]);

            Assert.That(connection.GetActRole(), Is.EqualTo("modeller"));
            GraphQLRequest request = CreateSubscriptionRequest(connection, "subscription Test { test }", null, "Test");

            Dictionary<string, object?> extensions = (Dictionary<string, object?>)request.Extensions!;
            Dictionary<string, object?> headers = extensions["headers"] as Dictionary<string, object?>
                ?? throw new InvalidOperationException("Subscription extensions headers have unexpected type.");
            Assert.That(headers["x-hasura-role"], Is.EqualTo("modeller"));
        }

        private static GraphQLHttpClient GetGraphQlClient(GraphQlApiConnection connection)
        {
            FieldInfo? field = typeof(GraphQlApiConnection).GetField("graphQlClient", BindingFlags.NonPublic | BindingFlags.Instance);
            return (GraphQLHttpClient)(field?.GetValue(connection) ?? throw new InvalidOperationException("graphQlClient field not found."));
        }

        private static GraphQLRequest CreateSubscriptionRequest(GraphQlApiConnection connection, string query, object? variables, string? operationName)
        {
            MethodInfo? method = typeof(GraphQlApiConnection).GetMethod("CreateSubscriptionRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            return (GraphQLRequest)(method?.Invoke(connection, [query, variables, operationName]) ?? throw new InvalidOperationException("CreateSubscriptionRequest method not found."));
        }

        private static string CreateJwt(string defaultRole, string[] allowedRoles)
        {
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims:
            [
                new Claim("x-hasura-default-role", defaultRole),
                new Claim("x-hasura-allowed-roles", System.Text.Json.JsonSerializer.Serialize(allowedRoles), JsonClaimValueTypes.JsonArray)
            ]));
        }

        private static ClaimsPrincipal CreateUser(string[] roles)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                "test",
                ClaimTypes.Name,
                ClaimTypes.Role));
        }
    }
}
