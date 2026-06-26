using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class OwnersControllerTest
    {
        private static readonly string[] kOwnerControllerRoutes = ["api/owners"];
        private static readonly string[] kModellerRole = [Roles.Modeller];
        private static readonly string[] kAdminAndModellerRoles = [Roles.Admin, Roles.Modeller];
        private static readonly string[] kOwnerResponseTypes = ["standard", "infrastructure", "infrastructure"];

        [Test]
        public void GetUsesApiOwnersRoute()
        {
            RouteAttribute[] controllerRoutes = typeof(OwnersController).GetCustomAttributes<RouteAttribute>().ToArray();
            MethodInfo getMethod = typeof(OwnersController).GetMethod(nameof(OwnersController.Get))!;
            HttpPostAttribute? httpPost = getMethod.GetCustomAttribute<HttpPostAttribute>();

            Assert.That(controllerRoutes.Select(route => route.Template), Is.EquivalentTo(kOwnerControllerRoutes));
            Assert.That(httpPost?.Template, Is.EqualTo("get"));
        }

        [Test]
        public void GetAllowsAuditorAdminAndModeller()
        {
            MethodInfo getMethod = typeof(OwnersController).GetMethod(nameof(OwnersController.Get))!;
            AuthorizeAttribute? authorize = getMethod.GetCustomAttribute<AuthorizeAttribute>();

            Assert.That(authorize, Is.Not.Null);
            Assert.That(authorize!.Roles, Is.EqualTo($"{Roles.Auditor}, {Roles.Admin}, {Roles.Modeller}"));
        }

        [Test]
        public async Task GetBuildsAndCombinedFilterPredicates()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            await controller.Get(new GetOwnersRequest
            {
                OwnerId = 42,
                OwnerLifeCycleStateId = 3,
                Active = true,
                Name = "Accounting",
                AppIdExternal = "APP-42"
            });

            Assert.That(apiConnection.Query, Is.EqualTo(OwnerQueries.getOwnersFiltered));
            string variables = SerializeVariables(apiConnection.Variables);
            Assert.That(variables, Does.Contain("\"id\":{\"_eq\":42}"));
            Assert.That(variables, Does.Contain("\"owner_lifecycle_state_id\":{\"_eq\":3}"));
            Assert.That(variables, Does.Contain("\"active\":{\"_eq\":true}"));
            Assert.That(variables, Does.Contain("\"name\":{\"_ilike\":\"%Accounting%\"}"));
            Assert.That(variables, Does.Contain("\"app_id_external\":{\"_ilike\":\"%APP-42%\"}"));
            Assert.That(variables, Does.Contain("\"_and\""));
        }

        [Test]
        public async Task GetExcludesInactiveLifecycleStateByDefault()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            await controller.Get(new GetOwnersRequest());

            string variables = SerializeVariables(apiConnection.Variables);
            Assert.That(variables, Does.Contain("\"owner_lifecycle_state\":{\"active_state\":{\"_eq\":true}}"));
            Assert.That(variables, Does.Contain("\"owner_lifecycle_state_id\":{\"_is_null\":true}"));
            Assert.That(variables, Does.Contain("\"_or\""));
        }

        [Test]
        public async Task GetIncludesInactiveLifecycleStateWhenDisabled()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            await controller.Get(new GetOwnersRequest { ShowOnlyActiveState = false });

            string variables = SerializeVariables(apiConnection.Variables);
            Assert.That(variables, Does.Not.Contain("active_state"));
        }

        [Test]
        public async Task GetConvertsWildcardFiltersToLikePatterns()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            await controller.Get(new GetOwnersRequest
            {
                Name = "Finance*",
                AppIdExternal = "APP-?"
            });

            string variables = SerializeVariables(apiConnection.Variables);
            Assert.That(variables, Does.Contain("\"name\":{\"_ilike\":\"Finance%\"}"));
            Assert.That(variables, Does.Contain("\"app_id_external\":{\"_ilike\":\"APP-_\"}"));
        }

        [Test]
        public async Task GetEscapesLiteralSqlWildcardsInFilters()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            await controller.Get(new GetOwnersRequest
            {
                Name = "APP_1",
                AppIdExternal = "50%"
            });

            string variables = SerializeVariables(apiConnection.Variables);
            Assert.Multiple(() =>
            {
                Assert.That(variables, Does.Contain($"\"name\":{{\"_ilike\":{JsonSerializer.Serialize("%APP\\_1%")}}}"));
                Assert.That(variables, Does.Contain($"\"app_id_external\":{{\"_ilike\":{JsonSerializer.Serialize("%50\\%%")}}}"));
            });
        }

        [Test]
        public async Task GetReturnsBadRequestForNonPositiveOwnerId()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            ActionResult<List<GetOwnerResponse>> result = await controller.Get(new GetOwnersRequest { OwnerId = 0 });

            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(apiConnection.Query, Is.Empty);
        }

        [Test]
        public async Task GetReturnsBadRequestForNonPositiveLifecycleStateId()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            ActionResult<List<GetOwnerResponse>> result = await controller.Get(new GetOwnersRequest { OwnerLifeCycleStateId = 0 });

            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(apiConnection.Query, Is.Empty);
        }

        [Test]
        public async Task GetReturnsBadRequestForControlCharacterInName()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            ActionResult<List<GetOwnerResponse>> result = await controller.Get(new GetOwnersRequest { Name = "badname" });

            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(apiConnection.Query, Is.Empty);
        }

        [Test]
        public async Task GetReturnsBadRequestForOverlongFilter()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            ActionResult<List<GetOwnerResponse>> result = await controller.Get(new GetOwnersRequest { AppIdExternal = new string('a', 257) });

            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(apiConnection.Query, Is.Empty);
        }

        [Test]
        public void GetOwnersRequestRejectsUnknownJsonProperties()
        {
            string json = """{"ownerId":1,"unsupported":true}""";

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetOwnersRequest>(json));
        }

        [Test]
        public async Task GetRestrictsModellerToEditableOwnerIds()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(
                apiConnection,
                PrincipalWithRolesAndClaims(kModellerRole, new Claim("x-hasura-editable-owners", "{7,8}")));

            await controller.Get(new GetOwnersRequest());

            string variables = SerializeVariables(apiConnection.Variables);
            Assert.That(variables, Does.Contain("\"id\":{\"_in\":[7,8]}"));
        }

        [Test]
        public async Task GetDoesNotRestrictAdminWhoAlsoHasModellerRole()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(
                apiConnection,
                PrincipalWithRolesAndClaims(kAdminAndModellerRoles, new Claim("x-hasura-editable-owners", "{7,8}")));

            await controller.Get(new GetOwnersRequest());

            string variables = SerializeVariables(apiConnection.Variables);
            Assert.That(variables, Does.Not.Contain("\"_in\""));
        }

        [Test]
        public async Task GetMapsOwnerResponseType()
        {
            OwnersApiConnection apiConnection = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Application", ExtAppId = "APP-0001" },
                    new FwoOwner { Id = 2, Name = "Network", ExtAppId = "COM-0002" },
                    new FwoOwner { Id = 3, Name = "Empty" }
                ]
            };
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));

            ActionResult<List<GetOwnerResponse>> result = await controller.Get(new GetOwnersRequest());

            OkObjectResult okResult = (OkObjectResult)result.Result!;
            List<GetOwnerResponse> owners = (List<GetOwnerResponse>)okResult.Value!;
            Assert.That(owners.Select(owner => owner.Type), Is.EqualTo(kOwnerResponseTypes));
        }

        [Test]
        public async Task GetMapsOwnerLifecycleState()
        {
            OwnersApiConnection apiConnection = new()
            {
                Owners =
                [
                    new FwoOwner
                    {
                        Id = 1,
                        Name = "Application",
                        ExtAppId = "APP-0001",
                        OwnerLifeCycleState = new OwnerLifeCycleState { Id = 5, Name = "Active", ActiveState = true }
                    },
                    new FwoOwner { Id = 2, Name = "Network", ExtAppId = "COM-0002" }
                ]
            };
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));

            ActionResult<List<GetOwnerResponse>> result = await controller.Get(new GetOwnersRequest());

            OkObjectResult okResult = (OkObjectResult)result.Result!;
            List<GetOwnerResponse> owners = (List<GetOwnerResponse>)okResult.Value!;
            Assert.Multiple(() =>
            {
                Assert.That(owners[0].OwnerLifecycleState, Is.Not.Null);
                Assert.That(owners[0].OwnerLifecycleState!.Id, Is.EqualTo(5));
                Assert.That(owners[0].OwnerLifecycleState!.Name, Is.EqualTo("Active"));
                Assert.That(owners[1].OwnerLifecycleState, Is.Null);
            });
        }

        [Test]
        public async Task GetOmitsDetailFieldsByDefault()
        {
            OwnersApiConnection apiConnection = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Application", ExtAppId = "APP-0001", TenantId = 7, Criticality = "high", Active = false }
                ]
            };
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));

            ActionResult<List<GetOwnerResponse>> result = await controller.Get(new GetOwnersRequest());

            OkObjectResult okResult = (OkObjectResult)result.Result!;
            List<GetOwnerResponse> owners = (List<GetOwnerResponse>)okResult.Value!;
            Assert.Multiple(() =>
            {
                Assert.That(owners[0].OwnerResponsibles, Is.Null);
                Assert.That(owners[0].TenantId, Is.Null);
                Assert.That(owners[0].Criticality, Is.Null);
                Assert.That(owners[0].Active, Is.Null);
            });
        }

        [Test]
        public async Task GetReturnsDetailFieldsWhenRequested()
        {
            OwnersApiConnection apiConnection = new()
            {
                Owners =
                [
                    new FwoOwner
                    {
                        Id = 1,
                        Name = "Application",
                        ExtAppId = "APP-0001",
                        IsDefault = true,
                        TenantId = 7,
                        RecertInterval = 365,
                        Criticality = "high",
                        OwnerLifeCycleStateId = 5,
                        Active = false,
                        ImportSource = "import",
                        CommSvcPossible = true,
                        LastRecertifierId = 11,
                        LastRecertifierDn = "cn=user",
                        RecertActive = true,
                        AdditionalInfo = new Dictionary<string, string> { ["key"] = "value" },
                        OwnerResponsibles =
                        [
                            new OwnerResponsible { Dn = "cn=main", ResponsibleTypeId = GlobalConst.kOwnerResponsibleTypeMain },
                            new OwnerResponsible { Dn = "cn=support", ResponsibleTypeId = GlobalConst.kOwnerResponsibleTypeSupporting }
                        ]
                    }
                ]
            };
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));

            ActionResult<List<GetOwnerResponse>> result = await controller.Get(new GetOwnersRequest { ShowDetails = true });

            OkObjectResult okResult = (OkObjectResult)result.Result!;
            GetOwnerResponse owner = ((List<GetOwnerResponse>)okResult.Value!)[0];
            Assert.Multiple(() =>
            {
                Assert.That(owner.OwnerResponsibles, Has.Count.EqualTo(2));
                Assert.That(owner.OwnerResponsibles![0].Dn, Is.EqualTo("cn=main"));
                Assert.That(owner.OwnerResponsibles[0].ResponsibleType, Is.EqualTo(GlobalConst.kOwnerResponsibleTypeMain));
                Assert.That(owner.OwnerResponsibles[1].Dn, Is.EqualTo("cn=support"));
                Assert.That(owner.OwnerResponsibles[1].ResponsibleType, Is.EqualTo(GlobalConst.kOwnerResponsibleTypeSupporting));
                Assert.That(owner.IsDefault, Is.True);
                Assert.That(owner.TenantId, Is.EqualTo(7));
                Assert.That(owner.RecertInterval, Is.EqualTo(365));
                Assert.That(owner.Criticality, Is.EqualTo("high"));
                Assert.That(owner.OwnerLifecycleStateId, Is.EqualTo(5));
                Assert.That(owner.Active, Is.False);
                Assert.That(owner.ImportSource, Is.EqualTo("import"));
                Assert.That(owner.CommonServicePossible, Is.True);
                Assert.That(owner.LastRecertifier, Is.EqualTo(11));
                Assert.That(owner.LastRecertifierDn, Is.EqualTo("cn=user"));
                Assert.That(owner.RecertActive, Is.True);
                Assert.That(owner.AdditionalInfo, Is.Not.Null);
                Assert.That(owner.AdditionalInfo!["key"], Is.EqualTo("value"));
            });
            string serializedOwner = JsonSerializer.Serialize(owner);
            Assert.Multiple(() =>
            {
                Assert.That(serializedOwner, Does.Contain("\"ownerResponsibles\""));
                Assert.That(serializedOwner, Does.Contain("\"responsibleType\""));
            });
        }

        [Test]
        public async Task GetTreatsNullRequestAsEmptyRequest()
        {
            OwnersApiConnection apiConnection = new();
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            ActionResult<List<GetOwnerResponse>> result = await controller.Get(null);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
                Assert.That(SerializeVariables(apiConnection.Variables), Does.Contain("active_state"));
            });
        }

        [Test]
        public async Task GetReturnsEmptyListWhenApiReturnsNull()
        {
            OwnersApiConnection apiConnection = new() { ReturnNullOwners = true };
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));

            ActionResult<List<GetOwnerResponse>> result = await controller.Get(new GetOwnersRequest());

            OkObjectResult okResult = (OkObjectResult)result.Result!;
            List<GetOwnerResponse> owners = (List<GetOwnerResponse>)okResult.Value!;
            Assert.That(owners, Is.Empty);
        }

        [Test]
        public async Task GetReturnsInternalServerErrorWhenApiThrows()
        {
            OwnersApiConnection apiConnection = new() { ThrowOnQuery = true };
            OwnersController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            ActionResult<List<GetOwnerResponse>> result = await controller.Get(new GetOwnersRequest());

            ObjectResult objectResult = (ObjectResult)result.Result!;
            Assert.Multiple(() =>
            {
                Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
                Assert.That(objectResult.Value, Is.EqualTo("Internal server error"));
            });
        }

        private static OwnersController CreateController(ApiConnection apiConnection, ClaimsPrincipal user)
        {
            return new OwnersController(apiConnection)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            };
        }

        private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
        {
            return PrincipalWithRolesAndClaims(roles, []);
        }

        private static ClaimsPrincipal PrincipalWithRolesAndClaims(IEnumerable<string> roles, params Claim[] claims)
        {
            IEnumerable<Claim> roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
            ClaimsIdentity identity = new(roleClaims.Concat(claims), "test", ClaimTypes.Name, ClaimTypes.Role);
            return new ClaimsPrincipal(identity);
        }

        private static string SerializeVariables(object? variables)
        {
            return JsonSerializer.Serialize(variables);
        }

        private sealed class OwnersApiConnection : SimulatedApiConnection
        {
            public string Query { get; private set; } = string.Empty;
            public object? Variables { get; private set; }
            public List<FwoOwner> Owners { get; set; } = [];
            public bool ReturnNullOwners { get; set; }
            public bool ThrowOnQuery { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
                string query,
                object? variables = null,
                string? operationName = null,
                QueryChunkingOptions? chunkingOptions = null)
            {
                if (ThrowOnQuery)
                {
                    throw new InvalidOperationException("owner lookup failed");
                }

                Query = query;
                Variables = variables;
                if (ReturnNullOwners)
                {
                    return Task.FromResult(default(QueryResponseType)!);
                }
                return Task.FromResult((QueryResponseType)(object)Owners);
            }
        }
    }
}
