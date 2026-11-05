using System.Text.Json;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class ModellingObjectMappingTest
{
    private const int kMaxLimit = 100;

    [TestCase(443, 443, "TCP", "443/tcp")]
    [TestCase(1000, 2000, "UDP", "1000-2000/udp")]
    [TestCase(53, null, "udp", "53/udp")]
    public void ToServiceValueBuildsPortAndProtocolNotation(int port, int? portEnd, string protocol, string expected)
    {
        ModellingService service = new() { Port = port, PortEnd = portEnd, Protocol = new NetworkProtocol { Name = protocol } };

        Assert.That(ModellingObjectResponseMapper.ToServiceValue(service), Is.EqualTo(expected));
    }

    [Test]
    public void ToServiceValueHandlesServicesWithoutPortsOrProtocolName()
    {
        ModellingService portless = new() { Protocol = new NetworkProtocol { Name = "ICMP" } };
        ModellingService protocolIdOnly = new() { Port = 80, ProtoId = 6, Protocol = null };
        ModellingService portOnly = new() { Port = 80, Protocol = null };

        Assert.Multiple(() =>
        {
            Assert.That(ModellingObjectResponseMapper.ToServiceValue(portless), Is.EqualTo("icmp"));
            Assert.That(ModellingObjectResponseMapper.ToServiceValue(protocolIdOnly), Is.EqualTo("80/6"));
            Assert.That(ModellingObjectResponseMapper.ToServiceValue(portOnly), Is.EqualTo("80"));
        });
    }

    [TestCase(null, "public")]
    [TestCase(" ", "public")]
    [TestCase("Public", "public")]
    [TestCase("Restricted", "restricted")]
    [TestCase("Private", "private")]
    public void ToPermissionValueReturnsLowerCasePermission(string? stored, string expected)
    {
        Assert.That(ModellingObjectResponseMapper.ToPermissionValue(stored), Is.EqualTo(expected));
    }

    [TestCase("1.2.3.4/32", "1.2.3.4/32", "1.2.3.4")]
    [TestCase("1.2.3.4", "", "1.2.3.4")]
    [TestCase("10.0.0.0/32", "10.0.0.255/32", "10.0.0.0/24")]
    [TestCase("10.0.0.1", "10.0.0.9", "10.0.0.1-10.0.0.9")]
    [TestCase("1:2:ed::45/128", "1:2:ed::45/128", "1:2:ed::45")]
    [TestCase("", "", "")]
    public void ToAppServerResponseUsesCompactNotation(string ip, string ipEnd, string expected)
    {
        ModellingAppServer appServer = new() { Id = 3, Name = "server", Ip = ip, IpEnd = ipEnd };

        ModellingAppServerResponse response = ModellingObjectResponseMapper.ToAppServerResponse(appServer);

        Assert.Multiple(() =>
        {
            Assert.That(response.Id, Is.EqualTo(3));
            Assert.That(response.Name, Is.EqualTo("server"));
            Assert.That(response.IpAddress, Is.EqualTo(expected));
        });
    }

    [Test]
    public void GroupResponsesListDistinctNonEmptyAddresses()
    {
        ModellingAppRole group = new()
        {
            Id = 5,
            IdString = "NG-5",
            Name = "group",
            AppServers = new()
            {
                new ModellingAppServerWrapper { Content = new ModellingAppServer { Ip = "1.1.1.1", IpEnd = "1.1.1.1" } },
                new ModellingAppServerWrapper { Content = new ModellingAppServer { Ip = "1.1.1.1/32", IpEnd = "" } },
                new ModellingAppServerWrapper { Content = new ModellingAppServer { Ip = "", IpEnd = "" } }
            }
        };
        ModellingNetworkArea area = new()
        {
            Id = 6,
            Name = "area",
            IpData = new()
            {
                new NetworkDataWrapper { Content = new NetworkSubnet { Ip = null, IpEnd = null } },
                new NetworkDataWrapper { Content = new NetworkSubnet { Ip = "10.0.0.0/24", IpEnd = null } }
            }
        };

        ModellingNetworkGroupResponse groupResponse = ModellingObjectResponseMapper.ToNetworkGroupResponse(group);
        ModellingAppRoleResponse appRoleResponse = ModellingObjectResponseMapper.ToAppRoleResponse(group);
        ModellingNetworkAreaResponse areaResponse = ModellingObjectResponseMapper.ToNetworkAreaResponse(area);

        Assert.Multiple(() =>
        {
            Assert.That(groupResponse.IdString, Is.EqualTo("NG-5"));
            Assert.That(groupResponse.IpAddresses, Is.EqualTo(new List<string> { "1.1.1.1" }));
            Assert.That(appRoleResponse.AppServers, Has.Count.EqualTo(3));
            Assert.That(appRoleResponse.IpAddresses, Is.EqualTo(new List<string> { "1.1.1.1" }));
            Assert.That(areaResponse.IdString, Is.Empty);
            Assert.That(areaResponse.IpAddresses, Is.EqualTo(new List<string> { "10.0.0.0/24" }));
        });
    }

    [Test]
    public void ToInterfaceResponseKeepsEveryObjectTypeKeyEvenWhenEmpty()
    {
        ModellingInterfaceResponse response = ModellingObjectResponseMapper.ToInterfaceResponse(new ModellingInterfaceQueryResult { Id = 4 });

        string json = JsonSerializer.Serialize(response);
        Assert.Multiple(() =>
        {
            Assert.That(response.InterfaceName, Is.Empty);
            Assert.That(response.ProvidingAppName, Is.Empty);
            Assert.That(response.ProvidingAppIdExternal, Is.Null);
            Assert.That(response.Permission, Is.EqualTo("public"));
            Assert.That(json, Does.Contain("\"source\":{\"appServers\":[],\"appRoles\":[],\"networkAreas\":[],\"networkGroups\":[]}"));
            Assert.That(json, Does.Contain("\"service\":{\"services\":[],\"serviceGroups\":[]}"));
            Assert.That(json, Does.Contain("\"connectionId\":4"));
            Assert.That(json, Does.Contain("\"interfaceDescription\":\"\""));
        });
    }

    [Test]
    public void InterfaceQueryResultReadsTheQueryAliases()
    {
        const string kJson = "{\"id\":9,\"name\":\"i\",\"reason\":\"r\",\"interface_permission\":\"Restricted\"," +
            "\"owner\":{\"id\":1,\"name\":\"o\",\"app_id_external\":\"APP-1\"}," +
            "\"source_other_groups\":[{\"nwgroup\":{\"id\":2,\"id_string\":\"G\",\"name\":\"g\"," +
            "\"nwobjects\":[{\"owner_network\":{\"id\":3,\"name\":\"s\",\"ip\":\"1.1.1.1/32\",\"ip_end\":\"1.1.1.1/32\"}}]}}]," +
            "\"destination_areas\":[{\"nwgroup\":{\"id\":4,\"id_string\":\"NA4\",\"name\":\"a\"," +
            "\"ip_data\":[{\"owner_network\":{\"id\":5,\"name\":\"n\",\"ip\":\"10.0.0.0/24\",\"ip_end\":\"10.0.0.255/24\"}}]}}]}";

        ModellingInterfaceQueryResult result = JsonSerializer.Deserialize<ModellingInterfaceQueryResult>(kJson)!;
        ModellingInterfaceResponse response = ModellingObjectResponseMapper.ToInterfaceResponse(result);

        Assert.Multiple(() =>
        {
            Assert.That(response.Permission, Is.EqualTo("restricted"));
            Assert.That(response.ProvidingAppIdExternal, Is.EqualTo("APP-1"));
            Assert.That(response.Source.NetworkGroups.Single().IpAddresses, Is.EqualTo(new List<string> { "1.1.1.1" }));
            Assert.That(response.Destination.NetworkAreas.Single().IpAddresses, Is.EqualTo(new List<string> { "10.0.0.0/24" }));
        });
    }

    [Test]
    public void InterfaceVariablesCombineEveryFilter()
    {
        ModellingListOptions<AvailableInterfacesFilter> options = new()
        {
            Limit = 5,
            Filter = new AvailableInterfacesFilter
            {
                ConnectionId = new() { 3, 3 },
                InterfaceName = new() { "web" },
                InterfaceDescription = new() { "demo*" },
                Permission = new() { "public", "Restricted" },
                ProvidingAppName = new() { "demo app" },
                ProvidingAppIdExternal = new() { "APP-667" }
            }
        };

        string variables = JsonSerializer.Serialize(ModellingObjectQueryBuilder.BuildInterfaceVariables(7, options, kMaxLimit));

        Assert.Multiple(() =>
        {
            Assert.That(variables, Does.Contain("\"appId\":7"));
            Assert.That(variables, Does.Contain("{\"id\":{\"_in\":[3]}}"));
            Assert.That(variables, Does.Contain("{\"name\":{\"_ilike\":\"%web%\"}}"));
            Assert.That(variables, Does.Contain("{\"reason\":{\"_ilike\":\"demo%\"}}"));
            Assert.That(variables, Does.Contain("{\"interface_permission\":{\"_ilike\":\"public\"}},{\"interface_permission\":{\"_is_null\":true}}"));
            Assert.That(variables, Does.Contain("{\"interface_permission\":{\"_ilike\":\"Restricted\"}}"));
            Assert.That(variables, Does.Contain("{\"owner\":{\"name\":{\"_ilike\":\"%demo app%\"}}}"));
            Assert.That(variables, Does.Contain("{\"owner\":{\"app_id_external\":{\"_ilike\":\"APP-667\"}}}"));
            Assert.That(variables, Does.Contain("\"limit\":6,\"offset\":0"));
        });
    }

    [Test]
    public void InterfaceVariablesWithoutFilterUseEmptyWhere()
    {
        string variables = JsonSerializer.Serialize(ModellingObjectQueryBuilder.BuildInterfaceVariables(
            7, new ModellingListOptions<AvailableInterfacesFilter>(), kMaxLimit));

        Assert.That(variables, Is.EqualTo($"{{\"appId\":7,\"where\":{{}},\"limit\":{kMaxLimit + 1},\"offset\":0}}"));
    }

    [Test]
    public void ApplicationLookupMatchesTheWholeIdIgnoringCase()
    {
        string variables = JsonSerializer.Serialize(ModellingObjectQueryBuilder.BuildApplicationLookupVariables(" app_7%* "));

        Assert.That(variables, Is.EqualTo(
            $"{{\"where\":{{\"app_id_external\":{{\"_ilike\":\"app\\\\_7\\\\%*\"}}}},\"limit\":{ModellingObjectQueryBuilder.kMaxApplicationCandidates},\"offset\":0}}"));
    }

    [TestCase("abc", false, "%abc%")]
    [TestCase("abc", true, "abc")]
    [TestCase("a*c", true, "a%c")]
    [TestCase("a_c?", true, "a\\_c_")]
    public void BuildLikePatternMatchesPlainTextExactlyOnRequest(string value, bool matchPlainTextExactly, string expected)
    {
        Assert.That(GraphQlFilterBuilder.BuildLikePattern(value, matchPlainTextExactly), Is.EqualTo(expected));
    }

    [Test]
    public void BuildCaseInsensitiveEqualsPatternEscapesLikeWildcards()
    {
        Assert.That(GraphQlFilterBuilder.BuildCaseInsensitiveEqualsPattern(" a\\b%c_d*e? "), Is.EqualTo("a\\\\b\\%c\\_d*e?"));
    }
}
