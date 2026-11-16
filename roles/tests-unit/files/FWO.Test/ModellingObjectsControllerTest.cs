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
internal class ModellingObjectsControllerTest
{
    private const string kRequiredRoles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Modeller}";
    private static readonly List<string> kModellerRole = new() { Roles.Modeller };
    private static readonly List<string> kAdminAndModellerRoles = new() { Roles.Admin, Roles.Modeller };

    [TestCase(nameof(ModellingObjectsController.GetAvailableInterfacesForApp), "getAvailableInterfacesForApp")]
    [TestCase(nameof(ModellingObjectsController.GetAppRolesPerApp), "getAppRolesPerApp")]
    [TestCase(nameof(ModellingObjectsController.GetNetworkAreas), "getNetworkAreas")]
    public void EndpointsUseModellingRoutePostAndRequiredRoles(string methodName, string template)
    {
        MethodInfo method = typeof(ModellingObjectsController).GetMethod(methodName)!;
        RouteAttribute? route = typeof(ModellingObjectsController).GetCustomAttribute<RouteAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(route?.Template, Is.EqualTo("api/modelling"));
            Assert.That(method.GetCustomAttribute<HttpPostAttribute>()?.Template, Is.EqualTo(template));
            Assert.That(method.GetCustomAttribute<AuthorizeAttribute>()?.Roles, Is.EqualTo(kRequiredRoles));
        });
    }

    [Test]
    public void OnlyNetworkAreasAllowAnEmptyBody()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GetBodyBehavior(nameof(ModellingObjectsController.GetNetworkAreas)), Is.EqualTo(EmptyBodyBehavior.Allow));
            Assert.That(GetBodyBehavior(nameof(ModellingObjectsController.GetAvailableInterfacesForApp)), Is.EqualTo(EmptyBodyBehavior.Default));
            Assert.That(GetBodyBehavior(nameof(ModellingObjectsController.GetAppRolesPerApp)), Is.EqualTo(EmptyBodyBehavior.Default));
        });
    }

    [Test]
    public void InterfaceQueryContainsTheVisibilityRules()
    {
        string query = ModellingQueries.getAvailableInterfacesForApp;
        Assert.Multiple(() =>
        {
            Assert.That(query, Does.Contain("query getAvailableInterfacesForApp($appId: Int!"));
            Assert.That(query, Does.Contain("is_interface: { _eq: true }, is_published: { _eq: true }, is_requested: { _eq: false }, removed: { _eq: false }"));
            Assert.That(query, Does.Contain("{ app_id: { _eq: $appId } }"));
            Assert.That(query, Does.Contain("{ interface_permission: { _is_null: true } }"));
            Assert.That(query, Does.Contain("{ interface_permission: { _ilike: \"public\" } }"));
            Assert.That(query, Does.Contain("{ interface_permission: { _ilike: \"restricted\" }, permitted_owners: { app_id: { _eq: $appId } } }"));
            Assert.That(query, Does.Not.Contain("\"private\""));
            Assert.That(query, Does.Contain("limit: $limit"));
            Assert.That(query, Does.Contain("order_by: [{ name: asc }, { id: asc }]"));
            Assert.That(query, Does.Contain("group_type: { _nin: [20, 23] }, is_deleted: { _eq: false }"));
            Assert.That(query, Does.Contain("owner_network: { is_deleted: { _eq: false } }"));
        });
    }

    [Test]
    public void GroupQueriesExcludeDeletedObjectsAndOtherGroupTypes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModellingQueries.getAppRolesPerApp, Does.Contain("group_type: { _eq: 20 }, is_deleted: { _eq: false }"));
            Assert.That(ModellingQueries.getAppRolesPerApp, Does.Contain("owner_network: { is_deleted: { _eq: false } }"));
            Assert.That(ModellingQueries.getAppRolesPerApp, Does.Contain("order_by: [{ name: asc }, { id: asc }]"));
            Assert.That(ModellingQueries.getNetworkAreas, Does.Contain("group_type: { _eq: 23 }, is_deleted: { _eq: false }"));
            Assert.That(ModellingQueries.getNetworkAreas, Does.Contain("ip_data: nwobject_nwgroups(where: { owner_network: { is_deleted: { _eq: false } } })"));
            Assert.That(ModellingQueries.getNetworkAreas, Does.Contain("order_by: [{ name: asc }, { id: asc }]"));
        });
    }

    [Test]
    public async Task GetAvailableInterfacesReturnsMappedInterfacesForAdmin()
    {
        ModellingObjectsApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "APP-7")),
            Interfaces = new() { CreateInterface(11, "Restricted") }
        };
        ModellingObjectsController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<AvailableInterfacesResponse> result = await controller.GetAvailableInterfacesForApp(
            new GetAvailableInterfacesRequest { AppIdExternal = "app-7" });

        AvailableInterfacesResponse response = GetOkValue(result);
        ModellingInterfaceResponse interf = response.Interfaces.Single();
        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.Queries, Is.EqualTo(new List<string>
            {
                OwnerQueries.getApplicationIdentifiers,
                ModellingQueries.getAvailableInterfacesForApp
            }));
            Assert.That(SerializeVariables(apiConnection.LastOwnerVariables), Does.Contain("\"app_id_external\":{\"_ilike\":\"app-7\"}"));
            Assert.That(SerializeVariables(apiConnection.LastInterfaceVariables), Does.Contain("\"appId\":7"));
            Assert.That(SerializeVariables(apiConnection.LastInterfaceVariables),
                Does.Contain($"\"limit\":{GlobalConst.kDefaultMaxRestResponseObjectLimit + 1}"));
            Assert.That(interf.ConnectionId, Is.EqualTo(11));
            Assert.That(interf.InterfaceName, Is.EqualTo("interface-11"));
            Assert.That(interf.InterfaceDescription, Is.EqualTo("description-11"));
            Assert.That(interf.Permission, Is.EqualTo("restricted"));
            Assert.That(interf.ProvidingAppName, Is.EqualTo("provider"));
            Assert.That(interf.ProvidingAppIdExternal, Is.EqualTo("APP-667"));
            Assert.That(interf.Destination.AppServers.Single().IpAddress, Is.EqualTo("1:2:ed::45"));
            Assert.That(interf.Destination.AppRoles.Single().IpAddresses, Is.EqualTo(new List<string> { "1.4.5.6", "4.56.0.0/22", "1.2.3.8-1.2.3.18" }));
            Assert.That(interf.Destination.NetworkAreas.Single().IpAddresses, Is.EqualTo(new List<string> { "5.6.7.0/24" }));
            Assert.That(interf.Destination.NetworkGroups.Single().IpAddresses, Is.EqualTo(new List<string> { "10.1.1.10" }));
            Assert.That(interf.Source.AppServers, Is.Empty);
            Assert.That(interf.Service.Services.Single().Value, Is.EqualTo("443/tcp"));
            Assert.That(interf.Service.ServiceGroups.Single().Services.Single().Value, Is.EqualTo("1000-2000/udp"));
            Assert.That(controller.Response.Headers[ListPaging.kHasMoreHeader].ToString(), Is.EqualTo("false"));
        });
    }

    [TestCase(1, "true")]
    [TestCase(2, "false")]
    public async Task GetAvailableInterfacesReturnsOnePageAndTellsWhetherMoreFollow(int limit, string expectedHasMore)
    {
        ModellingObjectsApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "APP-7")),
            Interfaces = new() { CreateInterface(11, null), CreateInterface(12, "Public") }
        };
        ModellingObjectsController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));

        ActionResult<AvailableInterfacesResponse> result = await controller.GetAvailableInterfacesForApp(new GetAvailableInterfacesRequest
        {
            AppIdExternal = "APP-7",
            Options = new ModellingListOptions<AvailableInterfacesFilter> { Limit = limit, Offset = 3 }
        });

        AvailableInterfacesResponse response = GetOkValue(result);
        Assert.Multiple(() =>
        {
            Assert.That(response.Interfaces, Has.Count.EqualTo(Math.Min(limit, 2)));
            Assert.That(response.Interfaces[0].Permission, Is.EqualTo("public"));
            Assert.That(SerializeVariables(apiConnection.LastInterfaceVariables), Does.Contain($"\"limit\":{limit + 1},\"offset\":3"));
            Assert.That(controller.Response.Headers[ListPaging.kHasMoreHeader].ToString(), Is.EqualTo(expectedHasMore));
        });
    }

    [Test]
    public async Task GetAvailableInterfacesReturnsNotFoundForUnknownApplication()
    {
        ModellingObjectsApiConnection apiConnection = new();
        ModellingObjectsController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<AvailableInterfacesResponse> result = await controller.GetAvailableInterfacesForApp(
            new GetAvailableInterfacesRequest { AppIdExternal = "APP-404" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
            Assert.That(apiConnection.Queries, Does.Not.Contain(ModellingQueries.getAvailableInterfacesForApp));
        });
    }

    [Test]
    public async Task GetAvailableInterfacesReturnsNotFoundForForeignApplicationOfModeller()
    {
        ModellingObjectsApiConnection apiConnection = new() { Owners = CreateOwners((7, "APP-7")) };
        ModellingObjectsController controller = CreateController(apiConnection,
            PrincipalWithRolesAndClaims(kModellerRole, new Claim(ApplicationAddressQueryBuilder.kEditableOwnersClaim, "{ 8 }")));

        ActionResult<AvailableInterfacesResponse> result = await controller.GetAvailableInterfacesForApp(
            new GetAvailableInterfacesRequest { AppIdExternal = "APP-7" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
            Assert.That(apiConnection.Queries, Does.Not.Contain(ModellingQueries.getAvailableInterfacesForApp));
        });
    }

    [Test]
    public async Task GetAvailableInterfacesServesOwnApplicationOfModeller()
    {
        ModellingObjectsApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "APP-7")),
            Interfaces = new() { CreateInterface(11, "Private") }
        };
        ModellingObjectsController controller = CreateController(apiConnection,
            PrincipalWithRolesAndClaims(kModellerRole, new Claim(ApplicationAddressQueryBuilder.kEditableOwnersClaim, "{ 7 }")));

        ActionResult<AvailableInterfacesResponse> result = await controller.GetAvailableInterfacesForApp(
            new GetAvailableInterfacesRequest { AppIdExternal = "APP-7" });

        Assert.That(GetOkValue(result).Interfaces.Single().Permission, Is.EqualTo("private"));
    }

    [Test]
    public async Task GetVisibleApplicationPrefersTheExactMatchAndRejectsAmbiguousMatches()
    {
        ModellingObjectsApiConnection exactApiConnection = new() { Owners = CreateOwners((7, "app-7"), (8, "APP-7")) };
        ModellingObjectsApiConnection ambiguousApiConnection = new() { Owners = CreateOwners((7, "app-7"), (8, "App-7")) };

        FwoOwner? exactMatch = await CreateController(exactApiConnection, PrincipalWithRoles(Roles.Admin))
            .GetVisibleApplicationAsync(" APP-7 ");
        FwoOwner? ambiguousMatch = await CreateController(ambiguousApiConnection, PrincipalWithRoles(Roles.Admin))
            .GetVisibleApplicationAsync("APP-7");

        Assert.Multiple(() =>
        {
            Assert.That(exactMatch?.Id, Is.EqualTo(8));
            Assert.That(ambiguousMatch, Is.Null);
        });
    }

    [Test]
    public async Task GetVisibleApplicationDoesNotRestrictModellerWhoIsAlsoAdmin()
    {
        ModellingObjectsApiConnection apiConnection = new() { Owners = CreateOwners((7, "APP-7")) };
        ModellingObjectsController controller = CreateController(apiConnection, PrincipalWithRolesAndClaims(kAdminAndModellerRoles));

        FwoOwner? application = await controller.GetVisibleApplicationAsync("APP-7");

        Assert.That(application?.Id, Is.EqualTo(7));
    }

    [Test]
    public async Task GetAvailableInterfacesRejectsInvalidRequestWithAllErrors()
    {
        ModellingObjectsApiConnection apiConnection = new();
        ModellingObjectsController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<AvailableInterfacesResponse> result = await controller.GetAvailableInterfacesForApp(new GetAvailableInterfacesRequest
        {
            AppIdExternal = " ",
            Options = new ModellingListOptions<AvailableInterfacesFilter>
            {
                Limit = 0,
                Offset = -1,
                Filter = new AvailableInterfacesFilter
                {
                    ConnectionId = new() { 0 },
                    Permission = new() { "secret" },
                    InterfaceName = new() { "bad\u0001value" }
                }
            }
        });

        ValidationProblemDetails problem = GetValidationProblem(result.Result);
        Assert.Multiple(() =>
        {
            Assert.That(problem.Errors.Keys, Is.EquivalentTo(new List<string>
            {
                "appIdExternal",
                "options.limit",
                "options.offset",
                "options.filter.connectionId[0]",
                "options.filter.permission[0]",
                "options.filter.interfaceName[0]"
            }));
            Assert.That(apiConnection.Queries, Is.Empty);
        });
    }

    [Test]
    public void ValidateRejectsMissingOptionsTooManyValuesAndTooLongText()
    {
        GetAppRolesPerAppRequest nullOptionsRequest = new() { AppIdExternal = "APP-7", Options = null };
        GetAppRolesPerAppRequest oversizedRequest = new()
        {
            AppIdExternal = new string('a', ModellingObjectRequestLimits.kMaxTextLength + 1),
            Options = new ModellingListOptions<ModellingGroupFilter>
            {
                Limit = GlobalConst.kDefaultMaxRestResponseObjectLimit + 1,
                Filter = new ModellingGroupFilter
                {
                    IdString = CreateValues(ModellingObjectRequestLimits.kMaxFilterValues + 1),
                    Name = new() { null! },
                    Id = new() { -1 }
                }
            }
        };

        Dictionary<string, string[]> nullOptionsErrors = ModellingObjectRequestValidator.Validate(
            nullOptionsRequest, GlobalConst.kDefaultMaxRestResponseObjectLimit);
        Dictionary<string, string[]> oversizedErrors = ModellingObjectRequestValidator.Validate(
            oversizedRequest, GlobalConst.kDefaultMaxRestResponseObjectLimit);

        Assert.Multiple(() =>
        {
            Assert.That(nullOptionsErrors.Keys, Is.EquivalentTo(new List<string> { "options" }));
            Assert.That(oversizedErrors.Keys, Is.EquivalentTo(new List<string>
            {
                "appIdExternal",
                "options.limit",
                "options.filter.idString",
                "options.filter.name[0]",
                "options.filter.id[0]"
            }));
        });
    }

    [Test]
    public void ValidateAcceptsMaximumSizedValuesAndAllPermissions()
    {
        GetAvailableInterfacesRequest request = new()
        {
            AppIdExternal = new string('a', ModellingObjectRequestLimits.kMaxTextLength),
            Options = new ModellingListOptions<AvailableInterfacesFilter>
            {
                Limit = GlobalConst.kDefaultMaxRestResponseObjectLimit,
                Offset = 0,
                Filter = new AvailableInterfacesFilter
                {
                    Permission = new() { " Public", "restricted", "PRIVATE" },
                    ProvidingAppIdExternal = CreateValues(ModellingObjectRequestLimits.kMaxFilterValues)
                }
            }
        };

        Assert.That(ModellingObjectRequestValidator.Validate(request, GlobalConst.kDefaultMaxRestResponseObjectLimit), Is.Empty);
    }

    [Test]
    public async Task GetAppRolesPerAppReturnsAppRolesOfTheApplication()
    {
        ModellingObjectsApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "APP-7")),
            AppRoles = new() { CreateAppRole(1223, "AR4404711-123", ("2.3.4.5/32", "2.3.4.9/32"), ("5.6.7.0/24", "5.6.7.255/24"), ("5.6.7.0", "5.6.7.255")) }
        };
        ModellingObjectsController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<AppRolesPerAppResponse> result = await controller.GetAppRolesPerApp(new GetAppRolesPerAppRequest
        {
            AppIdExternal = "APP-7",
            Options = new ModellingListOptions<ModellingGroupFilter> { Filter = new ModellingGroupFilter { IdString = new() { "AR44*" } } }
        });

        ModellingAppRoleResponse appRole = GetOkValue(result).AppRoles.Single();
        string variables = SerializeVariables(apiConnection.LastAppRoleVariables);
        Assert.Multiple(() =>
        {
            Assert.That(variables, Does.Contain("\"app_id\":{\"_eq\":7}"));
            Assert.That(variables, Does.Contain("\"id_string\":{\"_ilike\":\"AR44%\"}"));
            Assert.That(appRole.Id, Is.EqualTo(1223));
            Assert.That(appRole.IdString, Is.EqualTo("AR4404711-123"));
            Assert.That(appRole.AppServers, Has.Count.EqualTo(3));
            Assert.That(appRole.IpAddresses, Is.EqualTo(new List<string> { "2.3.4.5-2.3.4.9", "5.6.7.0/24" }));
            Assert.That(controller.Response.Headers[ListPaging.kHasMoreHeader].ToString(), Is.EqualTo("false"));
        });
    }

    [Test]
    public async Task GetAppRolesPerAppReturnsNotFoundForUnknownApplication()
    {
        ModellingObjectsApiConnection apiConnection = new();
        ModellingObjectsController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));

        ActionResult<AppRolesPerAppResponse> result = await controller.GetAppRolesPerApp(
            new GetAppRolesPerAppRequest { AppIdExternal = "APP-404" });

        Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task GetAppRolesPerAppRejectsMissingApplicationId()
    {
        ModellingObjectsController controller = CreateController(new ModellingObjectsApiConnection(), PrincipalWithRoles(Roles.Admin));

        ActionResult<AppRolesPerAppResponse> result = await controller.GetAppRolesPerApp(new GetAppRolesPerAppRequest());

        Assert.That(GetValidationProblem(result.Result).Errors.Keys, Is.EquivalentTo(new List<string> { "appIdExternal" }));
    }

    [Test]
    public async Task GetNetworkAreasServesAdminWithoutBody()
    {
        ModellingObjectsApiConnection apiConnection = new() { Areas = new() { CreateArea(47, "NA47") } };
        ModellingObjectsController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<NetworkAreasResponse> result = await controller.GetNetworkAreas(null);

        ModellingNetworkAreaResponse area = GetOkValue(result).NetworkAreas.Single();
        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.Queries, Is.EqualTo(new List<string> { ModellingQueries.getNetworkAreas }));
            Assert.That(area.IdString, Is.EqualTo("NA47"));
            Assert.That(area.Name, Is.EqualTo("area-47"));
            Assert.That(area.IpAddresses, Is.EqualTo(new List<string> { "2.3.4.5-2.3.4.9", "5.6.7.0/24", "1:2:ed::45" }));
        });
    }

    [Test]
    public async Task GetNetworkAreasFiltersIdStringsExactlyUnlessWildcardsAreUsed()
    {
        ModellingObjectsApiConnection apiConnection = new();
        ModellingObjectsController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));

        await controller.GetNetworkAreas(new GetNetworkAreasRequest
        {
            Options = new ModellingListOptions<ModellingGroupFilter>
            {
                Filter = new ModellingGroupFilter { IdString = new() { "NA47", "NA5?" }, Name = new() { "dmz" }, Id = new() { 47 } }
            }
        });

        string variables = SerializeVariables(apiConnection.LastAreaVariables);
        Assert.Multiple(() =>
        {
            Assert.That(variables, Does.Contain("{\"id_string\":{\"_ilike\":\"NA47\"}}"));
            Assert.That(variables, Does.Contain("{\"id_string\":{\"_ilike\":\"NA5_\"}}"));
            Assert.That(variables, Does.Contain("\"name\":{\"_ilike\":\"%dmz%\"}"));
            Assert.That(variables, Does.Contain("\"id\":{\"_in\":[47]}"));
        });
    }

    [Test]
    public async Task GetNetworkAreasForbidsModellerWithoutCommonServiceApplication()
    {
        ModellingObjectsApiConnection apiConnection = new();
        ModellingObjectsController controller = CreateController(apiConnection,
            PrincipalWithRolesAndClaims(kModellerRole, new Claim(ApplicationAddressQueryBuilder.kEditableOwnersClaim, "{ 7, 8 }")));

        ActionResult<NetworkAreasResponse> result = await controller.GetNetworkAreas(new GetNetworkAreasRequest());

        string variables = SerializeVariables(apiConnection.LastOwnerVariables);
        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<ForbidResult>());
            Assert.That(variables, Does.Contain("\"id\":{\"_in\":[7,8]}"));
            Assert.That(variables, Does.Contain("\"common_service_possible\":{\"_eq\":true}"));
            Assert.That(apiConnection.Queries, Does.Not.Contain(ModellingQueries.getNetworkAreas));
        });
    }

    [Test]
    public async Task GetNetworkAreasForbidsModellerWithoutApplicationsWithoutQuery()
    {
        ModellingObjectsApiConnection apiConnection = new();
        ModellingObjectsController controller = CreateController(apiConnection, PrincipalWithRolesAndClaims(kModellerRole));

        ActionResult<NetworkAreasResponse> result = await controller.GetNetworkAreas(new GetNetworkAreasRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<ForbidResult>());
            Assert.That(apiConnection.Queries, Is.Empty);
        });
    }

    [Test]
    public async Task GetNetworkAreasServesModellerWithCommonServiceApplication()
    {
        ModellingObjectsApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "APP-7")),
            Areas = new() { CreateArea(47, "NA47") }
        };
        ModellingObjectsController controller = CreateController(apiConnection,
            PrincipalWithRolesAndClaims(kModellerRole, new Claim(ApplicationAddressQueryBuilder.kEditableOwnersClaim, "{ 7 }")));

        ActionResult<NetworkAreasResponse> result = await controller.GetNetworkAreas(new GetNetworkAreasRequest());

        Assert.That(GetOkValue(result).NetworkAreas, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EndpointsReturnInternalServerErrorWhenTheApiFails()
    {
        ModellingObjectsApiConnection apiConnection = new() { Fail = true };
        ModellingObjectsController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<AvailableInterfacesResponse> interfaces = await controller.GetAvailableInterfacesForApp(
            new GetAvailableInterfacesRequest { AppIdExternal = "APP-7" });
        ActionResult<AppRolesPerAppResponse> appRoles = await controller.GetAppRolesPerApp(
            new GetAppRolesPerAppRequest { AppIdExternal = "APP-7" });
        ActionResult<NetworkAreasResponse> areas = await controller.GetNetworkAreas(null);

        Assert.Multiple(() =>
        {
            Assert.That(((ObjectResult)interfaces.Result!).StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            Assert.That(((ObjectResult)appRoles.Result!).StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            Assert.That(((ObjectResult)areas.Result!).StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        });
    }

    [Test]
    public void RequestsRejectUnknownKeysAndDefaultOptionsToEmptyObject()
    {
        GetAvailableInterfacesRequest? request = JsonSerializer.Deserialize<GetAvailableInterfacesRequest>("{\"appIdExternal\":\"APP-7\"}");

        Assert.Multiple(() =>
        {
            Assert.That(request?.Options, Is.Not.Null);
            Assert.That(request?.Options?.Filter, Is.Null);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetNetworkAreasRequest>("{\"areaIdStrings\":[]}"));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetAppRolesPerAppRequest>(
                "{\"appIdExternal\":\"APP-7\",\"options\":{\"filter\":{\"unknown\":[]}}}"));
        });
    }

    private static EmptyBodyBehavior? GetBodyBehavior(string methodName)
    {
        ParameterInfo parameter = typeof(ModellingObjectsController).GetMethod(methodName)!.GetParameters().Single();
        return parameter.GetCustomAttribute<FromBodyAttribute>()?.EmptyBodyBehavior;
    }

    private static T GetOkValue<T>(ActionResult<T> result)
    {
        return (T)((OkObjectResult)result.Result!).Value!;
    }

    private static ValidationProblemDetails GetValidationProblem(ActionResult? result)
    {
        return (ValidationProblemDetails)((BadRequestObjectResult)result!).Value!;
    }

    private static List<string> CreateValues(int count)
    {
        return Enumerable.Range(1, count).Select(index => $"value-{index}").ToList();
    }

    private static List<FwoOwner> CreateOwners(params (int Id, string ExtAppId)[] owners)
    {
        return owners.Select(owner => new FwoOwner { Id = owner.Id, Name = $"app-{owner.Id}", ExtAppId = owner.ExtAppId }).ToList();
    }

    private static ModellingAppServerWrapper CreateAppServer(long id, string ip, string ipEnd)
    {
        return new ModellingAppServerWrapper { Content = new ModellingAppServer { Id = id, Name = $"server-{id}", Ip = ip, IpEnd = ipEnd } };
    }

    private static ModellingAppRole CreateAppRole(long id, string idString, params (string Ip, string IpEnd)[] addresses)
    {
        return new ModellingAppRole
        {
            Id = id,
            IdString = idString,
            Name = $"role-{id}",
            AppServers = addresses.Select((address, index) => CreateAppServer(index + 1, address.Ip, address.IpEnd)).ToList()
        };
    }

    private static ModellingNetworkArea CreateArea(long id, string idString)
    {
        return new ModellingNetworkArea
        {
            Id = id,
            IdString = idString,
            Name = $"area-{id}",
            IpData = new()
            {
                new NetworkDataWrapper { Content = new NetworkSubnet { Ip = "2.3.4.5/32", IpEnd = "2.3.4.9/32" } },
                new NetworkDataWrapper { Content = new NetworkSubnet { Ip = "5.6.7.0/24", IpEnd = "5.6.7.255/24" } },
                new NetworkDataWrapper { Content = new NetworkSubnet { Ip = "1:2:ed::45/128", IpEnd = "1:2:ed::45/128" } }
            }
        };
    }

    private static ModellingService CreateService(int id, int port, int portEnd, string protocol)
    {
        return new ModellingService { Id = id, Name = $"service-{id}", Port = port, PortEnd = portEnd, Protocol = new NetworkProtocol { Name = protocol } };
    }

    private static ModellingInterfaceQueryResult CreateInterface(int id, string? permission)
    {
        ModellingNetworkArea area = new()
        {
            Id = 47,
            IdString = "NA47",
            Name = "dmz",
            IpData = new() { new NetworkDataWrapper { Content = new NetworkSubnet { Ip = "5.6.7.0/24", IpEnd = "5.6.7.255/24" } } }
        };
        ModellingServiceGroup serviceGroup = new()
        {
            Id = 21,
            Name = "app-ports-02",
            Services = new() { new ModellingServiceWrapper { Content = CreateService(14, 1000, 2000, "UDP") } }
        };
        return new ModellingInterfaceQueryResult
        {
            Id = id,
            Name = $"interface-{id}",
            Reason = $"description-{id}",
            InterfacePermission = permission,
            Owner = new FwoOwner { Id = 667, Name = "provider", ExtAppId = "APP-667" },
            DestinationAppServers = new() { CreateAppServer(504, "1:2:ed::45/128", "1:2:ed::45/128") },
            DestinationAppRoles = new()
            {
                new ModellingAppRoleWrapper
                {
                    Content = CreateAppRole(1223, "AR667-002", ("1.4.5.6/32", "1.4.5.6/32"), ("4.56.0.0/22", "4.56.3.255/22"), ("1.2.3.8", "1.2.3.18"))
                }
            },
            DestinationAreas = new() { new ModellingNetworkAreaWrapper { Content = area } },
            DestinationOtherGroups = new() { new ModellingAppRoleWrapper { Content = CreateAppRole(77, "NG-077", ("10.1.1.10", "")) } },
            Services = new() { new ModellingServiceWrapper { Content = CreateService(11, 443, 443, "TCP") } },
            ServiceGroups = new() { new ModellingServiceGroupWrapper { Content = serviceGroup } }
        };
    }

    private static ModellingObjectsController CreateController(ApiConnection apiConnection, ClaimsPrincipal user)
    {
        return new ModellingObjectsController(apiConnection, new SimulatedGlobalConfig())
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

    private sealed class ModellingObjectsApiConnection : SimulatedApiConnection
    {
        public List<FwoOwner> Owners { get; set; } = [];
        public List<ModellingInterfaceQueryResult> Interfaces { get; set; } = [];
        public List<ModellingAppRole> AppRoles { get; set; } = [];
        public List<ModellingNetworkArea> Areas { get; set; } = [];
        public bool Fail { get; set; }
        public List<string> Queries { get; } = [];
        public object? LastOwnerVariables { get; private set; }
        public object? LastInterfaceVariables { get; private set; }
        public object? LastAppRoleVariables { get; private set; }
        public object? LastAreaVariables { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
            string query,
            object? variables = null,
            string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            if (Fail)
            {
                throw new InvalidOperationException("api failure");
            }

            Queries.Add(query);
            if (query == OwnerQueries.getApplicationIdentifiers)
            {
                LastOwnerVariables = variables;
                return Task.FromResult((QueryResponseType)(object)Owners);
            }
            if (query == ModellingQueries.getAvailableInterfacesForApp)
            {
                LastInterfaceVariables = variables;
                return Task.FromResult((QueryResponseType)(object)Interfaces);
            }
            if (query == ModellingQueries.getAppRolesPerApp)
            {
                LastAppRoleVariables = variables;
                return Task.FromResult((QueryResponseType)(object)AppRoles);
            }
            LastAreaVariables = variables;
            return Task.FromResult((QueryResponseType)(object)Areas);
        }
    }
}
