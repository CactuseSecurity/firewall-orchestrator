using FWO.Data.Flow;
using FWO.Middleware.Server.OpenApi;
using FWO.Middleware.Server.Responses;
using NUnit.Framework;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Test;

[TestFixture]
internal class FlowCatalogGroupResponseSerializationTest
{
    private static readonly JsonSerializerOptions kSerializerOptions = ApiDocumentationJsonOptions.CreateSerializerOptions();

    [Test]
    public void CompactAddressMember_OmitsEveryTechnicalField()
    {
        AddressGroupResponse.AddressGroupMemberResponse member = new()
        {
            Id = 1001,
            Name = "app-server-1"
        };

        JsonObject json = Serialize(member);

        Assert.That(json.Select(property => property.Key), Is.EquivalentTo(new[] { "id", "name" }));
        Assert.That(json["id"]!.GetValue<long>(), Is.EqualTo(1001));
        Assert.That(json["name"]!.GetValue<string>(), Is.EqualTo("app-server-1"));
    }

    [Test]
    public void DetailedAddressMember_EmitsStandaloneFields()
    {
        AddressGroupResponse.AddressGroupMemberResponse member = new()
        {
            Id = 1001,
            Name = "app-network",
            Type = "network",
            IpRange = ["192.0.2.0", "192.0.2.255"],
            State = FlowState.Implemented,
            ShowInRequest = true,
            IncludeTechnicalDetails = true
        };

        JsonObject json = Serialize(member);

        Assert.Multiple(() =>
        {
            Assert.That(json.Select(property => property.Key), Is.EquivalentTo(
                new[] { "id", "name", "type", "ipRange", "state", "showInRequest" }));
            Assert.That(json["type"]!.GetValue<string>(), Is.EqualTo("network"));
            Assert.That(json["ipRange"]!.AsArray().Select(value => value!.GetValue<string>()),
                Is.EqualTo(new[] { "192.0.2.0", "192.0.2.255" }));
            Assert.That(json["state"]!.GetValue<string>(), Is.EqualTo(FlowState.Implemented));
            Assert.That(json["showInRequest"]!.GetValue<bool>(), Is.True);
        });
    }

    [Test]
    public void CompactServiceMember_OmitsEveryTechnicalField()
    {
        ServiceGroupResponse.ServiceGroupMemberResponse member = new()
        {
            Id = 3001,
            Name = "icmp"
        };

        JsonObject json = Serialize(member);

        Assert.That(json.Select(property => property.Key), Is.EquivalentTo(new[] { "id", "name" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CompactMember_WithPopulatedTechnicalFields_RemainsCompactInExamples(bool useBaseOptions)
    {
        ServiceGroupResponse.ServiceGroupMemberResponse member = new()
        {
            Id = 3001,
            Name = "icmp",
            Protocol = "ICMP",
            State = FlowState.Implemented,
            ShowInRequest = true,
            IncludeTechnicalDetails = false
        };
        JsonSerializerOptions? baseOptions = useBaseOptions
            ? ApiDocumentationJsonOptions.CreateSerializerOptions()
            : null;
        JsonSerializerOptions options = ApiDocumentationJsonOptions.CreateExampleSerializerOptions(baseOptions);

        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(member, options))!.AsObject();

        Assert.That(json.Select(property => property.Key), Is.EquivalentTo(new[] { "id", "name" }));
    }

    [Test]
    public void DetailedProtocolOnlyServiceMember_EmitsNullPorts()
    {
        ServiceGroupResponse.ServiceGroupMemberResponse member = new()
        {
            Id = 3001,
            Name = "icmp",
            PortStart = null,
            PortEnd = null,
            Protocol = "ICMP",
            State = FlowState.Implemented,
            ShowInRequest = true,
            IncludeTechnicalDetails = true
        };

        JsonObject json = Serialize(member);

        Assert.Multiple(() =>
        {
            Assert.That(json.ContainsKey("portStart"), Is.True);
            Assert.That(json.ContainsKey("portEnd"), Is.True);
            Assert.That(json["portStart"], Is.Null);
            Assert.That(json["portEnd"], Is.Null);
            Assert.That(json["protocol"]!.GetValue<string>(), Is.EqualTo("ICMP"));
            Assert.That(json["state"]!.GetValue<string>(), Is.EqualTo(FlowState.Implemented));
            Assert.That(json["showInRequest"]!.GetValue<bool>(), Is.True);
            Assert.That(json.ContainsKey("includeTechnicalDetails"), Is.False);
        });
    }

    private static JsonObject Serialize<T>(T value)
    {
        return JsonNode.Parse(JsonSerializer.Serialize(value, kSerializerOptions))!.AsObject();
    }
}
