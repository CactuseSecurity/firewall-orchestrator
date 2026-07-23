using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
internal class ApplicationZonesControllerTest
{
    private static readonly List<string> kControllerRoutes = new() { "api/modelling" };
    private static readonly List<string> kModellerRole = new() { Roles.Modeller };

    [Test]
    public void GetUsesApplicationZonesRoute()
    {
        RouteAttribute[] controllerRoutes = typeof(ApplicationZonesController).GetCustomAttributes<RouteAttribute>().ToArray();
        MethodInfo getMethod = typeof(ApplicationZonesController).GetMethod(nameof(ApplicationZonesController.Get))!;
        HttpPostAttribute? httpPost = getMethod.GetCustomAttribute<HttpPostAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(controllerRoutes.Select(route => route.Template), Is.EquivalentTo(kControllerRoutes));
            Assert.That(httpPost?.Template, Is.EqualTo("getApplicationZones"));
        });
    }

    [Test]
    public void GetAllowsEmptyRequestBody()
    {
        MethodInfo getMethod = typeof(ApplicationZonesController).GetMethod(nameof(ApplicationZonesController.Get))!;
        ParameterInfo requestParameter = getMethod.GetParameters().Single();
        FromBodyAttribute? fromBody = requestParameter.GetCustomAttribute<FromBodyAttribute>();

        Assert.That(fromBody?.EmptyBodyBehavior, Is.EqualTo(EmptyBodyBehavior.Allow));
    }

    [Test]
    public void ApplicationZoneQueryLimitsResultsToActiveApplicationZones()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModellingQueries.getAppZones, Does.Contain("query getAppZones"));
            Assert.That(ModellingQueries.getAppZones, Does.Contain("$where: modelling_nwgroup_bool_exp"));
            Assert.That(ModellingQueries.getAppZones, Does.Contain("group_type: { _eq: 21 }"));
            Assert.That(ModellingQueries.getAppZones, Does.Contain("is_deleted: { _eq: false }"));
            Assert.That(ModellingQueries.getAppZones,
                Does.Contain("owner_network: { is_deleted: { _eq: false } }"));
        });
    }

    [Test]
    public void IpOnlyQueriesRequestOnlyIdentifiersAndAddressRanges()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OwnerQueries.getApplicationIdsAndExternalIds, Does.Contain("app_id_external"));
            Assert.That(OwnerQueries.getApplicationIdsAndExternalIds, Does.Not.Contain("owner_responsibles"));
            Assert.That(ModellingQueries.getAppZoneIps, Does.Contain("app_id"));
            Assert.That(ModellingQueries.getAppZoneIps, Does.Contain("ip_end"));
            Assert.That(ModellingQueries.getAppZoneIps, Does.Not.Contain("import_source"));
            Assert.That(ModellingQueries.getAppZoneIps, Does.Not.Contain("custom_type"));
        });
    }

    [Test]
    public void ApplicationQuerySupportsPaging()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OwnerQueries.getOwnersFiltered, Does.Contain("$limit: Int"));
            Assert.That(OwnerQueries.getOwnersFiltered, Does.Contain("$offset: Int"));
            Assert.That(OwnerQueries.getOwnersFiltered, Does.Contain("limit: $limit, offset: $offset"));
        });
    }

    [Test]
    public void GetAllowsAuditorAdminAndModeller()
    {
        MethodInfo getMethod = typeof(ApplicationZonesController).GetMethod(nameof(ApplicationZonesController.Get))!;
        AuthorizeAttribute? authorize = getMethod.GetCustomAttribute<AuthorizeAttribute>();

        Assert.That(authorize?.Roles, Is.EqualTo($"{Roles.Admin}, {Roles.Auditor}, {Roles.Modeller}"));
    }

    [Test]
    public async Task GetReturnsAllVisibleApplicationsAndMapsCompleteAddresses()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"), (8, "Application 08", "APP-8")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(7, 70, "AZ-7", "az-7", "10.7.0.1", "10.7.0.9"),
                CreateApplicationZone(8, 80, "AZ-8", "az-8", "10.8.0.1", string.Empty)
            }
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new();

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        OkObjectResult okResult = (OkObjectResult)result.Result!;
        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)okResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.Queries, Is.EqualTo(new List<string>
            {
                OwnerQueries.getOwnersFiltered,
                ModellingQueries.getAppZones
            }));
            Assert.That(apiConnection.SetBestRoleCount, Is.Zero);
            Assert.That(SerializeVariables(apiConnection.LastApplicationZoneVariables),
                Does.Contain("""{"app_id":{"_in":[7,8]}}"""));
            Assert.That(response, Has.Count.EqualTo(2));
            Assert.That(response[0].ApplicationId, Is.EqualTo(7));
            Assert.That(response[0].ApplicationName, Is.EqualTo("Application 07"));
            Assert.That(response[0].AppIdExternal, Is.EqualTo("APP-7"));
            Assert.That(response[0].Addresses[0].Ip, Is.EqualTo("10.7.0.1-10.7.0.9"));
            Assert.That(response[0].Addresses[0].IpStart, Is.EqualTo("10.7.0.1"));
            Assert.That(response[0].Addresses[0].IpEnd, Is.EqualTo("10.7.0.9"));
            Assert.That(response[0].Addresses[0].ImportSource, Is.EqualTo("manual"));
            Assert.That(response[0].Addresses[0].CustomType, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task GetReturnsCompactIpNotation()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((1, "Application 01", "APP-1"), (2, "Application 02", "APP-2"),
                (3, "Application 03", "APP-3"), (4, "Application 04", "APP-4")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(1, 10, "AZ-1", "az-1", "10.1.0.1/32", "10.1.0.1/32"),
                CreateApplicationZone(2, 20, "AZ-2", "az-2", "10.2.0.0/24", "10.2.0.255/24"),
                CreateApplicationZone(3, 30, "AZ-3", "az-3", "10.3.0.1/32", "10.3.0.9/32"),
                CreateApplicationZone(4, 40, "AZ-4", "az-4", "10.4.0.1/32", string.Empty)
            }
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(new GetApplicationZonesRequest());

        OkObjectResult okResult = (OkObjectResult)result.Result!;
        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)okResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(response[0].Addresses[0].Ip, Is.EqualTo("10.1.0.1"));
            Assert.That(response[0].Addresses[0].IpStart, Is.EqualTo("10.1.0.1/32"));
            Assert.That(response[1].Addresses[0].Ip, Is.EqualTo("10.2.0.0/24"));
            Assert.That(response[2].Addresses[0].Ip, Is.EqualTo("10.3.0.1-10.3.0.9"));
            Assert.That(response[3].Addresses[0].Ip, Is.EqualTo("10.4.0.1"));
            Assert.That(response[3].Addresses[0].IpEnd, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public async Task GetReturnsOnlyExternalIdAndCompactAddressesForIpOnlyDetailsLevel()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"), (8, "Application 08", "APP-8")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(7, 70, "AZ-7A", "az-7a", "10.7.0.1/32", "10.7.0.1/32"),
                CreateApplicationZone(7, 71, "AZ-7B", "az-7b", "10.7.0.2", "10.7.0.9"),
                CreateApplicationZone(8, 80, "AZ-8", "az-8", "10.8.0.0/24", "10.8.0.255/24")
            }
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new() { Options = new() { DetailsLevel = "ip-only" } };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        List<ApplicationZoneIpOnlyResponse> response = (List<ApplicationZoneIpOnlyResponse>)((OkObjectResult)result.Result!).Value!;
        string json = JsonSerializer.Serialize(response);
        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.Queries, Is.EqualTo(new List<string>
            {
                OwnerQueries.getApplicationIdsAndExternalIds,
                ModellingQueries.getAppZoneIps
            }));
            Assert.That(response[0].AppIdExternal, Is.EqualTo("APP-7"));
            Assert.That(response[0].Addresses, Is.EqualTo(new List<string> { "10.7.0.1", "10.7.0.2-10.7.0.9" }));
            Assert.That(response[1].AppIdExternal, Is.EqualTo("APP-8"));
            Assert.That(response[1].Addresses, Is.EqualTo(new List<string> { "10.8.0.0/24" }));
            Assert.That(json, Does.Not.Contain("applicationId"));
            Assert.That(json, Does.Not.Contain("applicationName"));
            Assert.That(json, Does.Not.Contain("ipStart"));
        });
    }

    [Test]
    public async Task GetIpOnlyExcludesApplicationsWithoutMatchingZone()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(7, 70, "AZ-7", "az-7", "10.7.0.1", string.Empty)
            }
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new()
            {
                DetailsLevel = "ip-only",
                Filter = new() { Name = "AZ-no-match" }
            }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        List<ApplicationZoneIpOnlyResponse> response = (List<ApplicationZoneIpOnlyResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response, Is.Empty);
    }

    [Test]
    public async Task GetIpOnlyExcludesApplicationsForDeletedZoneFilter()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"))
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new()
            {
                DetailsLevel = "ip-only",
                Filter = new() { IsDeleted = true }
            }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        List<ApplicationZoneIpOnlyResponse> response = (List<ApplicationZoneIpOnlyResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response, Is.Empty);
    }

    [Test]
    public async Task GetRestrictsModellerToEditableApplications()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"), (9, "Application 09", "APP-9")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(7, 70, "AZ-7", "az-7", "10.7.0.1", string.Empty),
                CreateApplicationZone(9, 90, "AZ-9", "az-9", "10.9.0.1", string.Empty)
            }
        };
        ClaimsPrincipal modeller = PrincipalWithRolesAndClaims(
            kModellerRole, new Claim("x-hasura-editable-owners", "{7,8}"));
        ApplicationZonesController controller = CreateController(apiConnection, modeller);
        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(new GetApplicationZonesRequest());

        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Select(applicationZone => applicationZone.ApplicationId), Is.EqualTo(new List<int> { 7 }));
    }

    [Test]
    public async Task GetIpOnlyRestrictsModellerToEditableApplications()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"), (9, "Application 09", "APP-9"))
        };
        ClaimsPrincipal modeller = PrincipalWithRolesAndClaims(
            kModellerRole, new Claim("x-hasura-editable-owners", "{7}"));
        ApplicationZonesController controller = CreateController(apiConnection, modeller);

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(new GetApplicationZonesRequest
        {
            Options = new() { DetailsLevel = "ip-only" }
        });

        List<ApplicationZoneIpOnlyResponse> response = (List<ApplicationZoneIpOnlyResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Select(application => application.AppIdExternal), Is.EqualTo(new List<string?> { "APP-7" }));
    }

    [Test]
    public async Task GetDoesNotRestrictAdminWithModellerRole()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"), (9, "Application 09", "APP-9")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(7, 70, "AZ-7", "az-7", "10.7.0.1", string.Empty),
                CreateApplicationZone(9, 90, "AZ-9", "az-9", "10.9.0.1", string.Empty)
            }
        };
        ClaimsPrincipal adminAndModeller = PrincipalWithRolesAndClaims(
            new List<string> { Roles.Admin, Roles.Modeller }, new Claim("x-hasura-editable-owners", "{7}"));
        ApplicationZonesController controller = CreateController(apiConnection, adminAndModeller);
        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(new GetApplicationZonesRequest());

        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Select(applicationZone => applicationZone.ApplicationId), Is.EqualTo(new List<int> { 7, 9 }));
    }

    [Test]
    public async Task GetAppliesEveryNullableResponseFilter()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(7, 70, "AZ-Match", "az-match", "10.7.0.1", string.Empty),
                CreateApplicationZone(7, 71, "AZ-Other", "az-other", "10.7.0.2", string.Empty)
            }
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));
        GetApplicationZonesRequest request = new()
        {
            Options = new()
            {
                Filter = new()
                {
                    ApplicationId = 7,
                    ApplicationName = "application *",
                    AppIdExternal = "app-*",
                    Id = 70,
                    Name = "az-*",
                    IdString = "AZ-*",
                    IsDeleted = false
                }
            }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        OkObjectResult okResult = (OkObjectResult)result.Result!;
        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)okResult.Value!;
        Assert.That(response.Select(applicationZone => applicationZone.Id), Is.EqualTo(new List<long> { 70 }));
    }

    [Test]
    public void RequestDefaultsOptionsToAnEmptyObject()
    {
        GetApplicationZonesRequest request = new();

        Assert.Multiple(() =>
        {
            Assert.That(request.Options, Is.Not.Null);
            Assert.That(request.Options!.Filter, Is.Null);
            Assert.That(request.Options.Limit, Is.Null);
            Assert.That(request.Options.Offset, Is.Null);
            Assert.That(request.Options.ShowOnlyActiveState, Is.Null);
            Assert.That(request.Options.DetailsLevel, Is.EqualTo("full"));
        });
    }

    [Test]
    public void RequestDeserializesIpOnlyDetailsLevel()
    {
        const string Json = """{"options":{"details-level":"ip-only"}}""";

        GetApplicationZonesRequest? request = JsonSerializer.Deserialize<GetApplicationZonesRequest>(Json);

        Assert.That(request?.Options?.DetailsLevel, Is.EqualTo("ip-only"));
    }

    [Test]
    public async Task GetTreatsNullDetailsLevelAsFull()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"))
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new() { Options = new() { DetailsLevel = null } };

        await controller.Get(request);

        Assert.That(apiConnection.Queries, Is.EqualTo(new List<string>
        {
            OwnerQueries.getOwnersFiltered,
            ModellingQueries.getAppZones
        }));
    }

    [Test]
    public void RequestRejectsUnknownProperties()
    {
        const string Json = """{"applicationIds":[7]}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetApplicationZonesRequest>(Json));
    }

    [Test]
    public async Task GetAggregatesAllKnownValidationErrors()
    {
        ApplicationZonesController controller = CreateController(new ApplicationZonesApiConnection(), PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new()
            {
                Filter = new()
                {
                    ApplicationId = 0,
                    ApplicationName = "badapplication",
                    AppIdExternal = new string('a', GetMaxFilterTextLength() + 1),
                    Id = 0,
                    Name = "badname",
                    IdString = new string('a', GetMaxFilterTextLength() + 1)
                },
                DetailsLevel = "summary",
                Limit = 0,
                Offset = -1
            }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);
        ValidationProblemDetails validationProblem = (ValidationProblemDetails)((ObjectResult)result.Result!).Value!;

        Assert.Multiple(() =>
        {
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.applicationId"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.applicationName"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.appIdExternal"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.id"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.name"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.filter.idString"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.details-level"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.limit"));
            Assert.That(validationProblem.Errors.Keys, Does.Contain("options.offset"));
        });
    }

    [Test]
    public async Task GetRejectsLimitAboveTheAllowedMaximum()
    {
        ApplicationZonesController controller = CreateController(new ApplicationZonesApiConnection(), PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new() { Limit = GetMaxLimit() + 1 }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        ValidationProblemDetails validationProblem = (ValidationProblemDetails)((ObjectResult)result.Result!).Value!;
        Assert.That(validationProblem.Errors.Keys, Does.Contain("options.limit"));
    }

    [Test]
    public async Task GetPassesLimitAndOffsetToTheApplicationQuery()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners(
                (7, "Application 07", "APP-7"),
                (8, "Application 08", "APP-8"),
                (9, "Application 09", "APP-9"))
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new() { Limit = 1, Offset = 1 }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)((OkObjectResult)result.Result!).Value!;
        string variables = SerializeVariables(apiConnection.LastApplicationVariables);
        Assert.Multiple(() =>
        {
            Assert.That(variables, Does.Contain("\"limit\":1"));
            Assert.That(variables, Does.Contain("\"offset\":1"));
            Assert.That(response.Select(applicationZone => applicationZone.ApplicationId), Is.EqualTo(new List<int> { 8 }));
        });
    }

    [Test]
    public async Task GetIpOnlyPassesLimitAndOffsetToTheApplicationQuery()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners(
                (7, "Application 07", "APP-7"),
                (8, "Application 08", "APP-8"),
                (9, "Application 09", "APP-9"))
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new() { DetailsLevel = "ip-only", Limit = 1, Offset = 1 }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        List<ApplicationZoneIpOnlyResponse> response = (List<ApplicationZoneIpOnlyResponse>)((OkObjectResult)result.Result!).Value!;
        string variables = SerializeVariables(apiConnection.LastApplicationVariables);
        Assert.Multiple(() =>
        {
            Assert.That(variables, Does.Contain("\"limit\":1"));
            Assert.That(variables, Does.Contain("\"offset\":1"));
            Assert.That(response.Select(application => application.AppIdExternal), Is.EqualTo(new List<string?> { "APP-8" }));
        });
    }

    [Test]
    public async Task GetOmitsPagingVariablesWhenNoPageIsRequested()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"))
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        await controller.Get(new GetApplicationZonesRequest());

        string variables = SerializeVariables(apiConnection.LastApplicationVariables);
        Assert.Multiple(() =>
        {
            Assert.That(variables, Does.Not.Contain("\"limit\""));
            Assert.That(variables, Does.Not.Contain("\"offset\""));
        });
    }

    [Test]
    public async Task GetExcludesApplicationsWithInactiveLifecycleStateByDefault()
    {
        ApplicationZonesApiConnection apiConnection = new() { Owners = CreateOwnersWithLifecycleStates() };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(new GetApplicationZonesRequest());

        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Select(applicationZone => applicationZone.ApplicationId), Is.EqualTo(new List<int> { 1, 3 }));
    }

    [Test]
    public async Task GetIncludesApplicationsWithInactiveLifecycleStateOnRequest()
    {
        ApplicationZonesApiConnection apiConnection = new() { Owners = CreateOwnersWithLifecycleStates() };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new() { ShowOnlyActiveState = false }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Select(applicationZone => applicationZone.ApplicationId), Is.EqualTo(new List<int> { 1, 2, 3 }));
    }

    [Test]
    public async Task GetReturnsEveryVisibleApplicationForEmptyRequestBody()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners(
                (7, "Application 07", "APP-7"),
                (8, "Application 08", "APP-8"),
                (9, "Application 09", "APP-9")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(7, 70, "AZ-7", "az-7", "10.7.0.1", string.Empty),
                CreateApplicationZone(8, 80, "AZ-8", "az-8", "10.8.0.1", string.Empty)
            }
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(null);

        OkObjectResult okResult = (OkObjectResult)result.Result!;
        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)okResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.Queries, Is.EqualTo(new List<string>
            {
                OwnerQueries.getOwnersFiltered,
                ModellingQueries.getAppZones
            }));
            Assert.That(response.Select(applicationZone => applicationZone.ApplicationId), Is.EqualTo(new List<int> { 7, 8, 9 }));
            Assert.That(response[2].Id, Is.Null);
        });
    }

    [Test]
    public async Task GetReturnsOnlyEditableApplicationZonesForEmptyObjectRequest()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(7, 70, "AZ-7", "az-7", "10.7.0.1", string.Empty)
            }
        };
        ClaimsPrincipal modeller = PrincipalWithRolesAndClaims(
            kModellerRole, new Claim("x-hasura-editable-owners", "{7}"));
        ApplicationZonesController controller = CreateController(apiConnection, modeller);

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(new GetApplicationZonesRequest());

        OkObjectResult okResult = (OkObjectResult)result.Result!;
        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)okResult.Value!;
        Assert.That(response.Select(applicationZone => applicationZone.Id), Is.EqualTo(new List<long> { 70 }));
    }

    [Test]
    public async Task GetReturnsNothingForModellerWithoutEditableApplications()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7"))
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRolesAndClaims(kModellerRole));

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(new GetApplicationZonesRequest());

        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response, Is.Empty);
    }

    [Test]
    public async Task GetReturnsPlaceholderForExistingApplicationWithoutApplicationZone()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((2, "Application 02", "APP-2"))
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new()
            {
                Filter = new() { ApplicationId = 2 }
            }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        OkObjectResult okResult = (OkObjectResult)result.Result!;
        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)okResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(response, Has.Count.EqualTo(1));
            Assert.That(response[0].ApplicationId, Is.EqualTo(2));
            Assert.That(response[0].ApplicationName, Is.EqualTo("Application 02"));
            Assert.That(response[0].AppIdExternal, Is.EqualTo("APP-2"));
            Assert.That(response[0].Id, Is.Null);
            Assert.That(response[0].Name, Is.Null);
            Assert.That(response[0].IdString, Is.Null);
            Assert.That(response[0].IsDeleted, Is.Null);
            Assert.That(response[0].Addresses, Is.Empty);
        });
    }

    [Test]
    public async Task GetExcludesDefaultSuperOwnerFromApplicationPlaceholders()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = new List<FwoOwner>
            {
                new() { Id = 4, Name = "super-owner", ExtAppId = "NONE", IsDefault = true },
                new() { Id = 2, Name = "Application 02", ExtAppId = "APP-2" }
            }
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(new GetApplicationZonesRequest());

        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Select(applicationZone => applicationZone.ApplicationId), Is.EqualTo(new List<int> { 2 }));
    }

    [Test]
    public async Task GetZoneFilterExcludesApplicationPlaceholders()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((2, "Application 02", "APP-2"), (3, "Application 03", "APP-3")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(2, 20, "AZ-2", "az-2", "10.2.0.1", string.Empty)
            }
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new() { Filter = new() { IsDeleted = false } }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Select(applicationZone => applicationZone.ApplicationId), Is.EqualTo(new List<int> { 2 }));
    }

    [Test]
    public async Task GetReturnsNothingForDeletedApplicationZones()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((2, "Application 02", "APP-2")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(2, 20, "AZ-2", "az-2", "10.2.0.1", string.Empty)
            }
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new() { Filter = new() { IsDeleted = true } }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Empty);
            Assert.That(apiConnection.Queries, Does.Not.Contain(ModellingQueries.getAppZones));
        });
    }

    [Test]
    public async Task GetUsesApplicationNameFilterToSelectApplicationWithoutApplicationZone()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((1, "ownerF_demo", "123"))
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new()
            {
                Filter = new() { ApplicationName = "ownerF_demo" }
            }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        OkObjectResult okResult = (OkObjectResult)result.Result!;
        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)okResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(response, Has.Count.EqualTo(1));
            Assert.That(response[0].ApplicationId, Is.EqualTo(1));
            Assert.That(response[0].ApplicationName, Is.EqualTo("ownerF_demo"));
            Assert.That(response[0].AppIdExternal, Is.EqualTo("123"));
            Assert.That(response[0].Id, Is.Null);
            Assert.That(response[0].Addresses, Is.Empty);
        });
    }

    [Test]
    public async Task GetUsesApplicationIdAndExternalIdFiltersToSelectApplicationWithoutApplicationZone()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((1, "ownerF_demo", "123"))
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));
        GetApplicationZonesRequest request = new()
        {
            Options = new()
            {
                Filter = new()
                {
                    ApplicationId = 1,
                    AppIdExternal = "123"
                }
            }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        OkObjectResult okResult = (OkObjectResult)result.Result!;
        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)okResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(response, Has.Count.EqualTo(1));
            Assert.That(response[0].ApplicationId, Is.EqualTo(1));
            Assert.That(response[0].Id, Is.Null);
        });
    }

    [Test]
    public async Task GetSupportsSingleCharacterWildcardsForEveryStringFilter()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(7, 70, "AZ-Match", "az-match", "10.7.0.1", string.Empty)
            }
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));
        GetApplicationZonesRequest request = new()
        {
            Options = new()
            {
                Filter = new()
                {
                    ApplicationName = "Application 0?",
                    AppIdExternal = "APP-?",
                    Name = "AZ-Matc?",
                    IdString = "az-matc?"
                }
            }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        OkObjectResult okResult = (OkObjectResult)result.Result!;
        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)okResult.Value!;
        Assert.That(response.Select(applicationZone => applicationZone.Id), Is.EqualTo(new List<long> { 70 }));
    }

    [Test]
    public async Task GetMatchesPlainTextFiltersAsContainsSearch()
    {
        ApplicationZonesApiConnection apiConnection = new()
        {
            Owners = CreateOwners((7, "Application 07", "APP-7")),
            AllApplicationZones = new List<ModellingAppZone>
            {
                CreateApplicationZone(7, 70, "AZ-Match", "az-match", "10.7.0.1", string.Empty)
            }
        };
        ApplicationZonesController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));
        GetApplicationZonesRequest request = new()
        {
            Options = new()
            {
                Filter = new()
                {
                    ApplicationName = "APPLICATION 07",
                    Name = "match"
                }
            }
        };

        ActionResult<List<ApplicationZoneResponse>> result = await controller.Get(request);

        List<ApplicationZoneResponse> response = (List<ApplicationZoneResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.Select(applicationZone => applicationZone.Id), Is.EqualTo(new List<long> { 70 }));
    }

    [Test]
    public void ApplicationZoneVariablesPushEveryZoneFilterToTheQuery()
    {
        ApplicationZoneFilter filter = new()
        {
            Id = 70,
            Name = "AZ-*",
            IdString = "az-match"
        };

        string variables = SerializeVariables(
            ApplicationZoneQueryBuilder.BuildApplicationZoneVariables(new List<int> { 7 }, filter));

        Assert.Multiple(() =>
        {
            Assert.That(variables, Does.Contain("""{"app_id":{"_in":[7]}}"""));
            Assert.That(variables, Does.Contain("""{"id":{"_eq":70}}"""));
            Assert.That(variables, Does.Contain("""{"name":{"_ilike":"AZ-%"}}"""));
            Assert.That(variables, Does.Contain("""{"id_string":{"_ilike":"%az-match%"}}"""));
        });
    }

    [Test]
    public void ApplicationVariablesEscapeLiteralSqlWildcards()
    {
        GetApplicationZonesOptions options = new()
        {
            Filter = new() { ApplicationName = "100%_owner" }
        };

        string variables = SerializeVariables(
            ApplicationZoneQueryBuilder.BuildApplicationVariables(options, PrincipalWithRoles(Roles.Admin)));

        Assert.That(variables, Does.Contain("""%100\\%\\_owner%"""));
    }

    [Test]
    public void HasZoneFilterDetectsEveryZoneLevelFilterField()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ApplicationZoneQueryBuilder.HasZoneFilter(null), Is.False);
            Assert.That(ApplicationZoneQueryBuilder.HasZoneFilter(new ApplicationZoneFilter()), Is.False);
            Assert.That(ApplicationZoneQueryBuilder.HasZoneFilter(new ApplicationZoneFilter { ApplicationId = 7 }), Is.False);
            Assert.That(ApplicationZoneQueryBuilder.HasZoneFilter(new ApplicationZoneFilter { Id = 70 }), Is.True);
            Assert.That(ApplicationZoneQueryBuilder.HasZoneFilter(new ApplicationZoneFilter { Name = "AZ" }), Is.True);
            Assert.That(ApplicationZoneQueryBuilder.HasZoneFilter(new ApplicationZoneFilter { IdString = "az" }), Is.True);
            Assert.That(ApplicationZoneQueryBuilder.HasZoneFilter(new ApplicationZoneFilter { IsDeleted = false }), Is.True);
        });
    }

    private static ModellingAppZone CreateApplicationZone(
        int applicationId, long id, string name, string idString, string ip, string ipEnd)
    {
        return new ModellingAppZone
        {
            AppId = applicationId,
            Id = id,
            Name = name,
            IdString = idString,
            AppServers = new List<ModellingAppServerWrapper>
            {
                new ModellingAppServerWrapper
                {
                    Content = new ModellingAppServer
                    {
                        Id = id + 1,
                        AppId = applicationId,
                        Name = "host-" + applicationId,
                        Ip = ip,
                        IpEnd = ipEnd,
                        ImportSource = "manual",
                        CustomType = 4
                    }
                }
            }
        };
    }

    private static ApplicationZonesController CreateController(ApiConnection apiConnection, ClaimsPrincipal user)
    {
        return new ApplicationZonesController(apiConnection)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
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

    private static List<FwoOwner> CreateOwnersWithLifecycleStates()
    {
        return new List<FwoOwner>
        {
            new() { Id = 1, Name = "Application 01", ExtAppId = "APP-1" },
            new()
            {
                Id = 2,
                Name = "Application 02",
                ExtAppId = "APP-2",
                OwnerLifeCycleStateId = 5,
                OwnerLifeCycleState = new OwnerLifeCycleState { Id = 5, Name = "Decommissioned", ActiveState = false }
            },
            new()
            {
                Id = 3,
                Name = "Application 03",
                ExtAppId = "APP-3",
                OwnerLifeCycleStateId = 1,
                OwnerLifeCycleState = new OwnerLifeCycleState { Id = 1, Name = "Active", ActiveState = true }
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

    private static int GetControllerConstant(string fieldName)
    {
        FieldInfo constant = typeof(ApplicationZonesController).GetField(
            fieldName, BindingFlags.NonPublic | BindingFlags.Static)!;
        return (int)constant.GetRawConstantValue()!;
    }

    private sealed class ApplicationZonesApiConnection : SimulatedApiConnection
    {
        public List<ModellingAppZone> AllApplicationZones { get; set; } = new();
        public List<FwoOwner> Owners { get; set; } = new();
        public List<string> Queries { get; } = new();
        public object? LastApplicationVariables { get; private set; }
        public object? LastApplicationZoneVariables { get; private set; }
        public int SetBestRoleCount { get; private set; }

        public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
        {
            SetBestRoleCount++;
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
            string query,
            object? variables = null,
            string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (query == OwnerQueries.getOwnersFiltered)
            {
                LastApplicationVariables = variables;
                return Task.FromResult((QueryResponseType)(object)GetMatchingOwners(variables));
            }
            if (query == OwnerQueries.getApplicationIdsAndExternalIds)
            {
                LastApplicationVariables = variables;
                return Task.FromResult((QueryResponseType)(object)GetMatchingOwners(variables));
            }
            if (query == ModellingQueries.getAppZones)
            {
                LastApplicationZoneVariables = variables;
                return Task.FromResult((QueryResponseType)(object)GetMatchingApplicationZones(variables));
            }
            if (query == ModellingQueries.getAppZoneIps)
            {
                LastApplicationZoneVariables = variables;
                return Task.FromResult((QueryResponseType)(object)GetMatchingApplicationZones(variables));
            }

            return Task.FromResult((QueryResponseType)(object)new List<ModellingAppZone>());
        }

        private List<FwoOwner> GetMatchingOwners(object? variables)
        {
            Dictionary<string, object> where = GetWhere(variables);
            List<FwoOwner> owners = Owners
                .Where(owner => SimulatedGraphQlFilter.Matches(where, OwnerField(owner)))
                .OrderBy(owner => owner.Name, StringComparer.Ordinal)
                .ToList();
            if (GetPagingValue(variables, "offset") is int offset)
            {
                owners = owners.Skip(offset).ToList();
            }
            if (GetPagingValue(variables, "limit") is int limit)
            {
                owners = owners.Take(limit).ToList();
            }
            return owners;
        }

        private List<ModellingAppZone> GetMatchingApplicationZones(object? variables)
        {
            Dictionary<string, object> where = GetWhere(variables);
            return AllApplicationZones
                .Where(applicationZone => SimulatedGraphQlFilter.Matches(where, ApplicationZoneField(applicationZone)))
                .ToList();
        }

        private static Func<string, object?> OwnerField(FwoOwner owner)
        {
            return fieldName => fieldName switch
            {
                "id" => owner.Id,
                "name" => owner.Name,
                "app_id_external" => owner.ExtAppId,
                "is_default" => owner.IsDefault,
                "owner_lifecycle_state_id" => owner.OwnerLifeCycleStateId,
                "owner_lifecycle_state" => OwnerLifecycleStateField(owner),
                _ => null
            };
        }

        private static object? OwnerLifecycleStateField(FwoOwner owner)
        {
            if (owner.OwnerLifeCycleState is null)
            {
                return null;
            }
            return (Func<string, object?>)(fieldName =>
                fieldName == "active_state" ? owner.OwnerLifeCycleState.ActiveState : null);
        }

        private static Func<string, object?> ApplicationZoneField(ModellingAppZone applicationZone)
        {
            return fieldName => fieldName switch
            {
                "id" => applicationZone.Id,
                "app_id" => applicationZone.AppId,
                "name" => applicationZone.Name,
                "id_string" => applicationZone.IdString,
                _ => null
            };
        }

        private static Dictionary<string, object> GetWhere(object? variables)
        {
            if (variables is Dictionary<string, object> variableValues &&
                variableValues.TryGetValue("where", out object? where) &&
                where is Dictionary<string, object> whereExpression)
            {
                return whereExpression;
            }
            return new Dictionary<string, object>();
        }

        private static int? GetPagingValue(object? variables, string fieldName)
        {
            if (variables is Dictionary<string, object> variableValues &&
                variableValues.TryGetValue(fieldName, out object? value))
            {
                return (int)value;
            }
            return null;
        }
    }

    /// <summary>
    /// Evaluates the subset of Hasura boolean expressions that the controllers build, so that the tests verify
    /// the pushed-down filters instead of re-implementing them.
    /// </summary>
    private static class SimulatedGraphQlFilter
    {
        public static bool Matches(Dictionary<string, object> where, Func<string, object?> getValue)
        {
            return where.All(expression => MatchesExpression(expression, getValue));
        }

        private static bool MatchesExpression(KeyValuePair<string, object> expression, Func<string, object?> getValue)
        {
            if (expression.Key is "_and" or "_or")
            {
                List<Dictionary<string, object>> operands = (List<Dictionary<string, object>>)expression.Value;
                return expression.Key == "_and"
                    ? operands.All(operand => Matches(operand, getValue))
                    : operands.Any(operand => Matches(operand, getValue));
            }
            return MatchesField(expression.Key, (Dictionary<string, object>)expression.Value, getValue);
        }

        private static bool MatchesField(
            string fieldName, Dictionary<string, object> comparison, Func<string, object?> getValue)
        {
            object? value = getValue(fieldName);
            if (value is Func<string, object?> nestedGetValue)
            {
                return Matches(comparison, nestedGetValue);
            }
            if (value is null && !comparison.ContainsKey("_is_null"))
            {
                return false;
            }
            return comparison.All(operation => MatchesOperation(operation, value));
        }

        private static bool MatchesOperation(KeyValuePair<string, object> operation, object? value)
        {
            return operation.Key switch
            {
                "_eq" => Equals(value, operation.Value),
                "_in" => value is int intValue && ((List<int>)operation.Value).Contains(intValue),
                "_is_null" => Equals(value is null, operation.Value),
                "_ilike" => MatchesLikePattern(value as string, (string)operation.Value),
                _ => throw new NotSupportedException($"Unsupported operator '{operation.Key}'.")
            };
        }

        private static bool MatchesLikePattern(string? value, string pattern)
        {
            if (value is null)
            {
                return false;
            }

            StringBuilder regexPattern = new("^");
            for (int index = 0; index < pattern.Length; index++)
            {
                char patternCharacter = pattern[index];
                if (patternCharacter == '\\' && index + 1 < pattern.Length)
                {
                    regexPattern.Append(Regex.Escape(pattern[++index].ToString()));
                }
                else if (patternCharacter == '%')
                {
                    regexPattern.Append(".*");
                }
                else if (patternCharacter == '_')
                {
                    regexPattern.Append('.');
                }
                else
                {
                    regexPattern.Append(Regex.Escape(patternCharacter.ToString()));
                }
            }
            regexPattern.Append('$');
            return Regex.IsMatch(value, regexPattern.ToString(), RegexOptions.IgnoreCase);
        }
    }
}
