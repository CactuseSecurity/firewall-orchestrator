using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class ApplicationAddressesControllerTest
{
    private static readonly List<string> kControllerRoutes = new() { "api/modelling" };
    private static readonly List<string> kModellerRole = new() { Roles.Modeller };

    [Test]
    public void GetUsesApplicationAddressesRoute()
    {
        RouteAttribute[] controllerRoutes = typeof(ApplicationAddressesController).GetCustomAttributes<RouteAttribute>().ToArray();
        MethodInfo getMethod = typeof(ApplicationAddressesController).GetMethod(nameof(ApplicationAddressesController.Get))!;
        HttpPostAttribute? httpPost = getMethod.GetCustomAttribute<HttpPostAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(controllerRoutes.Select(route => route.Template), Is.EquivalentTo(kControllerRoutes));
            Assert.That(httpPost?.Template, Is.EqualTo("getIpDataForOwners"));
        });
    }

    [Test]
    public void GetAllowsEmptyRequestBodyAndRequiredRoles()
    {
        MethodInfo getMethod = typeof(ApplicationAddressesController).GetMethod(nameof(ApplicationAddressesController.Get))!;
        ParameterInfo requestParameter = getMethod.GetParameters().Single();
        FromBodyAttribute? fromBody = requestParameter.GetCustomAttribute<FromBodyAttribute>();
        AuthorizeAttribute? authorize = getMethod.GetCustomAttribute<AuthorizeAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(fromBody?.EmptyBodyBehavior, Is.EqualTo(EmptyBodyBehavior.Allow));
            Assert.That(authorize?.Roles, Is.EqualTo($"{Roles.Admin}, {Roles.Auditor}, {Roles.Modeller}"));
        });
    }

    [Test]
    public void ApplicationAddressQueryReadsActiveAppServerAddressesDirectly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModellingQueries.getApplicationIpAddresses, Does.Contain("query getApplicationIpAddresses"));
            Assert.That(ModellingQueries.getApplicationIpAddresses, Does.Contain("owner_network"));
            Assert.That(ModellingQueries.getApplicationIpAddresses, Does.Contain("nw_type: { _eq: 10 }"));
            Assert.That(ModellingQueries.getApplicationIpAddresses, Does.Contain("is_deleted: { _eq: false }"));
            Assert.That(ModellingQueries.getApplicationIpAddresses, Does.Contain("app_id: owner_id"));
            Assert.That(ModellingQueries.getApplicationIpAddresses, Does.Not.Contain("modelling_nwgroup"));
        });
    }

    [Test]
    public void ApplicationQueryReadsOnlyTheIdentifyingOwnerFields()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OwnerQueries.getApplicationIdentifiers, Does.Contain("query getApplicationIdentifiers"));
            Assert.That(OwnerQueries.getApplicationIdentifiers, Does.Contain("$where: owner_bool_exp"));
            Assert.That(OwnerQueries.getApplicationIdentifiers, Does.Contain("limit: $limit, offset: $offset"));
            Assert.That(OwnerQueries.getApplicationIdentifiers,
                Does.Contain("order_by: [{ name: asc }, { app_id_external: asc }, { id: asc }]"));
            Assert.That(OwnerQueries.getApplicationIdentifiers, Does.Contain("app_id_external"));
            Assert.That(OwnerQueries.getApplicationIdentifiers, Does.Not.Contain("fragment"));
            Assert.That(OwnerQueries.getApplicationIdentifiers, Does.Not.Contain("owner_responsibles"));
            Assert.That(OwnerQueries.getApplicationIdentifiers, Does.Not.Contain("owner_lifecycle_state {"));
        });
    }

    [Test]
    public async Task GetReturnsAppServerAddressesOfEveryVisibleApplication()
    {
        ApplicationAddressesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"), (8, "Application 08", "APP-8")),
            AppServers = CreateAppServers(
                (7, "10.7.0.1/32", "10.7.0.1/32"),
                (7, "10.7.0.0/24", "10.7.0.255/24"),
                (8, "10.8.0.1", "10.8.0.9"),
                (9, "10.9.0.1", ""))
        };
        ApplicationAddressesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(new GetApplicationAddressesRequest());

        List<ApplicationAddressResponse> response = (List<ApplicationAddressResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.Queries, Is.EqualTo(new List<string>
            {
                OwnerQueries.getApplicationIdentifiers,
                ModellingQueries.getApplicationIpAddresses
            }));
            Assert.That(SerializeVariables(apiConnection.LastApplicationAddressVariables),
                Does.Contain("\"owner_id\":{\"_in\":[7,8]}"));
            Assert.That(response, Has.Count.EqualTo(2));
            Assert.That(response[0].ApplicationId, Is.EqualTo(7));
            Assert.That(response[0].ApplicationName, Is.EqualTo("Application 07"));
            Assert.That(response[0].AppIdExternal, Is.EqualTo("APP-7"));
            Assert.That(response[0].Addresses, Is.EqualTo(new List<string> { "10.7.0.1", "10.7.0.0/24" }));
            Assert.That(response[1].Addresses, Is.EqualTo(new List<string> { "10.8.0.1-10.8.0.9" }));
        });
    }

    [Test]
    public async Task GetReturnsSelectedApplicationsWithNoAddresses()
    {
        ApplicationAddressesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"))
        };
        ApplicationAddressesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(new GetApplicationAddressesRequest());

        List<ApplicationAddressResponse> response = (List<ApplicationAddressResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(response, Has.Count.EqualTo(1));
            Assert.That(response[0].ApplicationId, Is.EqualTo(7));
            Assert.That(response[0].Addresses, Is.Empty);
        });
    }

    [Test]
    public async Task GetReturnsEveryAddressOfAnApplicationOnlyOnce()
    {
        ApplicationAddressesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7")),
            AppServers = CreateAppServers(
                (7, "10.7.0.1/32", "10.7.0.1/32"),
                (7, "10.7.0.1", ""),
                (7, "10.7.0.2", ""))
        };
        ApplicationAddressesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(new GetApplicationAddressesRequest());

        List<ApplicationAddressResponse> response = (List<ApplicationAddressResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response[0].Addresses, Is.EqualTo(new List<string> { "10.7.0.1", "10.7.0.2" }));
    }

    [Test]
    public async Task GetDoesNotReadAddressesWhenNoApplicationsMatch()
    {
        ApplicationAddressesApiConnection apiConnection = new();
        ApplicationAddressesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(new GetApplicationAddressesRequest());

        List<ApplicationAddressResponse> response = (List<ApplicationAddressResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Empty);
            Assert.That(apiConnection.Queries, Is.EqualTo(new List<string> { OwnerQueries.getApplicationIdentifiers }));
        });
    }

    [Test]
    public async Task GetRestrictsModellerAddressesToEditableApplications()
    {
        ApplicationAddressesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7")),
            AppServers = CreateAppServers((7, "10.7.0.1", ""))
        };
        ClaimsPrincipal modeller = PrincipalWithRolesAndClaims(
            kModellerRole, new Claim("x-hasura-editable-owners", "{7}"));
        ApplicationAddressesController controller = CreateController(apiConnection, modeller);

        await controller.Get(new GetApplicationAddressesRequest());

        Assert.That(SerializeVariables(apiConnection.LastApplicationVariables), Does.Contain("\"id\":{\"_in\":[7]}"));
    }

    [Test]
    public async Task GetSelectsNoApplicationForModellerWithoutEditableApplications()
    {
        ApplicationAddressesApiConnection apiConnection = new();
        ApplicationAddressesController controller = CreateController(apiConnection, PrincipalWithRolesAndClaims(kModellerRole));

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(new GetApplicationAddressesRequest());

        List<ApplicationAddressResponse> response = (List<ApplicationAddressResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(SerializeVariables(apiConnection.LastApplicationVariables), Does.Contain("\"id\":{\"_in\":[]}"));
            Assert.That(response, Is.Empty);
        });
    }

    [Test]
    public async Task GetExcludesTheDefaultSuperOwnerFromTheApplicationQuery()
    {
        ApplicationAddressesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"))
        };
        ApplicationAddressesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        await controller.Get(new GetApplicationAddressesRequest());

        Assert.That(SerializeVariables(apiConnection.LastApplicationVariables), Does.Contain("\"is_default\":{\"_eq\":false}"));
    }

    [Test]
    public async Task GetAddsActiveStateFilterByDefault()
    {
        ApplicationAddressesApiConnection apiConnection = new();
        ApplicationAddressesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        await controller.Get(new GetApplicationAddressesRequest());

        string variables = SerializeVariables(apiConnection.LastApplicationVariables);
        Assert.Multiple(() =>
        {
            Assert.That(variables, Does.Contain("\"owner_lifecycle_state\":{\"active_state\":{\"_eq\":true}}"));
            Assert.That(variables, Does.Contain("\"owner_lifecycle_state_id\":{\"_is_null\":true}"));
        });
    }

    [Test]
    public async Task GetOmitsActiveStateFilterWhenInactiveStatesAreRequested()
    {
        ApplicationAddressesApiConnection apiConnection = new();
        ApplicationAddressesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationAddressesRequest request = new()
        {
            Options = new() { ShowOnlyActiveState = false }
        };

        await controller.Get(request);

        Assert.That(SerializeVariables(apiConnection.LastApplicationVariables), Does.Not.Contain("owner_lifecycle_state"));
    }

    [Test]
    public async Task GetPassesApplicationFiltersAndPagingToTheApplicationQuery()
    {
        ApplicationAddressesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"))
        };
        ApplicationAddressesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationAddressesRequest request = new()
        {
            Options = new()
            {
                Filter = new()
                {
                    ApplicationId = new() { 7, 8 },
                    ApplicationName = new() { "application *", "second application" },
                    AppIdExternal = new() { "app-*" }
                },
                Limit = 20,
                Offset = 5
            }
        };

        await controller.Get(request);

        string variables = SerializeVariables(apiConnection.LastApplicationVariables);
        Assert.Multiple(() =>
        {
            Assert.That(variables, Does.Contain("\"id\":{\"_in\":[7,8]}"));
            Assert.That(variables, Does.Contain(
                "\"_or\":[{\"name\":{\"_ilike\":\"application %\"}},{\"name\":{\"_ilike\":\"%second application%\"}}]"));
            Assert.That(variables, Does.Contain("{\"app_id_external\":{\"_ilike\":\"app-%\"}}"));
            Assert.That(variables, Does.Not.Contain("\"_or\":[{\"app_id_external\""));
            Assert.That(variables, Does.Contain("\"limit\":20"));
            Assert.That(variables, Does.Contain("\"offset\":5"));
        });
    }

    [Test]
    public async Task GetAddsEveryCaseInsensitivelyDistinctFilterValueOnlyOnce()
    {
        ApplicationAddressesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"))
        };
        ApplicationAddressesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationAddressesRequest request = new()
        {
            Options = new()
            {
                Filter = new() { AppIdExternal = new() { "APP-7", "app-7", " app-7 " } }
            }
        };

        await controller.Get(request);

        string variables = SerializeVariables(apiConnection.LastApplicationVariables);
        Assert.Multiple(() =>
        {
            Assert.That(variables, Does.Contain("{\"app_id_external\":{\"_ilike\":\"%APP-7%\"}}"));
            Assert.That(variables, Does.Not.Contain("\"_or\":[{\"app_id_external\""));
        });
    }

    [Test]
    public void ApplicationAddressVariablesContainOnlyTheVisibleApplicationIds()
    {
        List<int> applicationIds = new() { 7, 8 };

        string variables = SerializeVariables(ApplicationAddressQueryBuilder.BuildApplicationAddressVariables(applicationIds));

        Assert.Multiple(() =>
        {
            Assert.That(variables, Does.Contain("\"owner_id\":{\"_in\":[7,8]}"));
            Assert.That(variables, Does.Not.Contain("group_type"));
            Assert.That(variables, Does.Not.Contain("id_string"));
        });
    }

    [Test]
    public void RequestDefaultsOptionsToAnEmptyObject()
    {
        GetApplicationAddressesRequest request = new();

        Assert.Multiple(() =>
        {
            Assert.That(request.Options, Is.Not.Null);
            Assert.That(request.Options!.Filter, Is.Null);
            Assert.That(request.Options.Limit, Is.Null);
            Assert.That(request.Options.Offset, Is.Null);
            Assert.That(request.Options.ShowOnlyActiveState, Is.Null);
        });
    }

    [Test]
    public void RequestDeserializesMultiValueFiltersAndRejectsRemovedZoneSpecificProperties()
    {
        const string ValidJson = """{"options":{"filter":{"applicationId":[7,8],"applicationName":["App 7","App 8"],"appIdExternal":["APP-7","APP-8"]}}}""";
        const string InvalidJson = """{"options":{"details-level":"ip-only","filter":{"id":7}}}""";

        GetApplicationAddressesRequest? request = JsonSerializer.Deserialize<GetApplicationAddressesRequest>(ValidJson);

        Assert.Multiple(() =>
        {
            Assert.That(request?.Options?.Filter?.ApplicationId, Is.EqualTo(new List<int> { 7, 8 }));
            Assert.That(request?.Options?.Filter?.ApplicationName, Is.EqualTo(new List<string> { "App 7", "App 8" }));
            Assert.That(request?.Options?.Filter?.AppIdExternal, Is.EqualTo(new List<string> { "APP-7", "APP-8" }));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetApplicationAddressesRequest>(InvalidJson));
        });
    }

    [Test]
    public async Task GetAggregatesAllKnownValidationErrors()
    {
        ApplicationAddressesController controller = CreateController(new ApplicationAddressesApiConnection(), PrincipalWithRoles(Roles.Admin));
        GetApplicationAddressesRequest request = new()
        {
            Options = new()
            {
                Filter = new()
                {
                    ApplicationId = new() { 0 },
                    ApplicationName = new() { "bad\u0001application" },
                    AppIdExternal = new() { new string('a', GetMaxFilterTextLength() + 1) }
                },
                Limit = 0,
                Offset = -1
            }
        };

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(request);
        ValidationProblemDetails validationProblem = (ValidationProblemDetails)((ObjectResult)result.Result!).Value!;

        Assert.Multiple(() =>
        {
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.applicationId[0]"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.applicationName[0]"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.appIdExternal[0]"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.limit"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.offset"));
        });
    }

    [Test]
    public async Task GetRejectsLimitAboveTheAllowedMaximum()
    {
        ApplicationAddressesController controller = CreateController(new ApplicationAddressesApiConnection(), PrincipalWithRoles(Roles.Admin));
        GetApplicationAddressesRequest request = new()
        {
            Options = new() { Limit = GetMaxLimit() + 1 }
        };

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(request);

        ValidationProblemDetails validationProblem = (ValidationProblemDetails)((ObjectResult)result.Result!).Value!;
        Assert.That(validationProblem.Errors.Keys, Does.Contain("options.limit"));
    }

    [Test]
    public async Task GetAcceptsFilterListsAtTheAllowedMaximum()
    {
        int maxFilterValues = GetMaxFilterValues();
        GetApplicationAddressesRequest request = new()
        {
            Options = new()
            {
                Filter = new()
                {
                    ApplicationId = Enumerable.Range(1, maxFilterValues).ToList(),
                    ApplicationName = CreateFilterValues("Application", maxFilterValues),
                    AppIdExternal = CreateFilterValues("APP", maxFilterValues)
                }
            }
        };
        ApplicationAddressesController controller = CreateController(new ApplicationAddressesApiConnection(), PrincipalWithRoles(Roles.Admin));

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(request);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public async Task GetAggregatesErrorsForFilterListsAboveTheAllowedMaximum()
    {
        int maximumFilterValues = GetMaxFilterValues();
        GetApplicationAddressesRequest request = new()
        {
            Options = new()
            {
                Filter = new()
                {
                    ApplicationId = Enumerable.Range(1, maximumFilterValues + 1).ToList(),
                    ApplicationName = CreateFilterValues("Application", maximumFilterValues + 1),
                    AppIdExternal = CreateFilterValues("APP", maximumFilterValues + 1)
                }
            }
        };
        ApplicationAddressesController controller = CreateController(new ApplicationAddressesApiConnection(), PrincipalWithRoles(Roles.Admin));

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(request);

        ValidationProblemDetails validationProblem = (ValidationProblemDetails)((ObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.applicationId"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.applicationName"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.appIdExternal"));
        });
    }

    [Test]
    public async Task GetRejectsNullOptions()
    {
        ApplicationAddressesController controller = CreateController(new ApplicationAddressesApiConnection(), PrincipalWithRoles(Roles.Admin));
        GetApplicationAddressesRequest request = new() { Options = null };

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(request);

        ValidationProblemDetails validationProblem = (ValidationProblemDetails)((ObjectResult)result.Result!).Value!;
        Assert.That(validationProblem.Errors.Keys, Does.Contain("options"));
    }

    [Test]
    public async Task GetRejectsNullTextFilterValues()
    {
        const string RequestJson = """{"options":{"filter":{"applicationName":[null]}}}""";
        ApplicationAddressesController controller = CreateController(new ApplicationAddressesApiConnection(), PrincipalWithRoles(Roles.Admin));
        GetApplicationAddressesRequest request = JsonSerializer.Deserialize<GetApplicationAddressesRequest>(RequestJson)!;

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(request);

        ValidationProblemDetails validationProblem = (ValidationProblemDetails)((ObjectResult)result.Result!).Value!;
        Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.applicationName[0]"));
    }

    [Test]
    public async Task GetRejectsBlankTextFilterValues()
    {
        ApplicationAddressesApiConnection apiConnection = new();
        ApplicationAddressesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationAddressesRequest request = new()
        {
            Options = new()
            {
                Filter = new()
                {
                    ApplicationName = new() { string.Empty },
                    AppIdExternal = new() { " " }
                }
            }
        };

        ActionResult<List<ApplicationAddressResponse>> result = await controller.Get(request);

        ValidationProblemDetails validationProblem = (ValidationProblemDetails)((ObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.applicationName[0]"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.appIdExternal[0]"));
            Assert.That(apiConnection.Queries, Is.Empty);
        });
    }

    private static List<FwoOwner> CreateOwners(params (int Id, string Name, string? AppIdExternal)[] owners)
    {
        return owners.Select(owner => new FwoOwner
        {
            Id = owner.Id,
            Name = owner.Name,
            ExtAppId = owner.AppIdExternal
        }).ToList();
    }

    private static List<ModellingAppServer> CreateAppServers(params (int AppId, string Ip, string IpEnd)[] appServers)
    {
        return appServers.Select((appServer, index) => new ModellingAppServer
        {
            Id = index + 1,
            AppId = appServer.AppId,
            Ip = appServer.Ip,
            IpEnd = appServer.IpEnd
        }).ToList();
    }

    private static ApplicationAddressesController CreateController(ApiConnection apiConnection, ClaimsPrincipal user)
    {
        return new ApplicationAddressesController(apiConnection)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
    {
        return PrincipalWithRolesAndClaims(roles);
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

    private static int GetMaxFilterTextLength()
    {
        return GetControllerConstant("kMaxFilterTextLength");
    }

    private static int GetMaxLimit()
    {
        return GetControllerConstant("kMaxLimit");
    }

    private static int GetMaxFilterValues()
    {
        return GetControllerConstant("kMaxFilterValues");
    }

    private static List<string> CreateFilterValues(string prefix, int count)
    {
        return Enumerable.Range(1, count).Select(index => $"{prefix}-{index}").ToList();
    }

    private static int GetControllerConstant(string fieldName)
    {
        FieldInfo constant = typeof(ApplicationAddressesController).GetField(
            fieldName, BindingFlags.NonPublic | BindingFlags.Static)!;
        return (int)constant.GetRawConstantValue()!;
    }

    private sealed class ApplicationAddressesApiConnection : SimulatedApiConnection
    {
        public List<FwoOwner> Owners { get; set; } = [];
        public List<ModellingAppServer> AppServers { get; set; } = [];
        public List<string> Queries { get; } = [];
        public object? LastApplicationVariables { get; private set; }
        public object? LastApplicationAddressVariables { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
            string query,
            object? variables = null,
            string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (query == OwnerQueries.getApplicationIdentifiers)
            {
                LastApplicationVariables = variables;
                return Task.FromResult((QueryResponseType)(object)Owners);
            }
            if (query == ModellingQueries.getApplicationIpAddresses)
            {
                LastApplicationAddressVariables = variables;
                return Task.FromResult((QueryResponseType)(object)AppServers);
            }

            return Task.FromResult((QueryResponseType)(object)new List<ModellingAppServer>());
        }
    }
}
