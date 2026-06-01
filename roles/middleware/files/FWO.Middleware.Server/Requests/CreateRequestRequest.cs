using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

public sealed class CreateRequestRequest
{
    [JsonPropertyName("requestorName")]
    public string RequestorName { get; set; } = string.Empty;

    [JsonPropertyName("requestorId")]
    public string RequestorId { get; set; } = string.Empty;

    [JsonPropertyName("ruleContactName")]
    public string RuleContactName { get; set; } = string.Empty;

    [JsonPropertyName("ruleContactId")]
    public string RuleContactId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("rules")]
    public List<CreateRequestRuleRequest> Rules { get; set; } = [];

    [JsonPropertyName("addressObjects")]
    public List<CreateAddressObjectRequest> AddressObjects { get; set; } = [];

    [JsonPropertyName("addressGroups")]
    public List<CreateAddressGroupRequest> AddressGroups { get; set; } = [];

    [JsonPropertyName("serviceObjects")]
    public List<CreateServiceObjectRequest> ServiceObjects { get; set; } = [];

    [JsonPropertyName("serviceGroups")]
    public List<CreateServiceGroupRequest> ServiceGroups { get; set; } = [];

    [JsonPropertyName("timeObjects")]
    public List<CreateTimeObjectRequest> TimeObjects { get; set; } = [];

    public sealed class CreateRequestRuleRequest
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("sourceObjects")]
        public List<int> SourceObjects { get; set; } = [];

        [JsonPropertyName("destinationObjects")]
        public List<int> DestinationObjects { get; set; } = [];

        [JsonPropertyName("serviceObjects")]
        public List<int> ServiceObjects { get; set; } = [];

        [JsonPropertyName("timeObjectId")]
        public int TimeObjectId { get; set; }

        [JsonPropertyName("ownerId")]
        public int OwnerId { get; set; }

        [JsonPropertyName("violationJustification")]
        public string ViolationJustification { get; set; } = string.Empty;
    }

    public sealed class CreateAddressObjectRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("ipStart")]
        public string IpStart { get; set; } = string.Empty;

        [JsonPropertyName("ipEnd")]
        public string IpEnd { get; set; } = string.Empty;
    }

    public sealed class CreateAddressGroupRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("memberIds")]
        public List<int> MemberIds { get; set; } = [];
    }

    public sealed class CreateServiceObjectRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("protocol")]
        public string Protocol { get; set; } = string.Empty;

        [JsonPropertyName("portStart")]
        public int PortStart { get; set; }

        [JsonPropertyName("portEnd")]
        public int PortEnd { get; set; }
    }

    public sealed class CreateServiceGroupRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("memberIds")]
        public List<int> MemberIds { get; set; } = [];
    }

    public sealed class CreateTimeObjectRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("startTime")]
        public string StartTime { get; set; } = string.Empty;

        [JsonPropertyName("endTime")]
        public string EndTime { get; set; } = string.Empty;
    }
}
