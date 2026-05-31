using FWO.Api.Client;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using GraphQL.Client.Http;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
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

        private static GraphQLHttpClient GetGraphQlClient(GraphQlApiConnection connection)
        {
            FieldInfo? field = typeof(GraphQlApiConnection).GetField("graphQlClient", BindingFlags.NonPublic | BindingFlags.Instance);
            return (GraphQLHttpClient)(field?.GetValue(connection) ?? throw new InvalidOperationException("graphQlClient field not found."));
        }
    }
}
