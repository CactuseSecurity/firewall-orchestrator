using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the CreateTicketRequest type.
/// </summary>
public sealed class CreateTicketRequest
{
    private CreateTicketOptions options = new();

    /// <summary>
    /// Gets the RequestorName value.
    /// </summary>
    [JsonPropertyName("requestorName")]
    public string RequestorName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the RequestorId value.
    /// </summary>
    [JsonPropertyName("requestorId")]
    public string RequestorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets the RuleContactName value.
    /// </summary>
    [JsonPropertyName("ruleContactName")]
    public string RuleContactName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the RuleContactId value.
    /// </summary>
    [JsonPropertyName("ruleContactId")]
    public string RuleContactId { get; set; } = string.Empty;

    /// <summary>
    /// Gets the Title value.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets the optional create-ticket behavior settings. Defaults to an empty object.
    /// </summary>
    [JsonPropertyName("options")]
    public CreateTicketOptions Options
    {
        get => options;
        set => options = value ?? new CreateTicketOptions();
    }

    /// <summary>
    /// Gets the Rules value.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<CreateTicketRuleRequest> Rules { get; set; } = [];

    /// <summary>
    /// Gets the AddressObjects value.
    /// </summary>
    [JsonPropertyName("addressObjects")]
    public List<CreateAddressObjectRequest> AddressObjects { get; set; } = [];

    /// <summary>
    /// Gets the AddressGroups value.
    /// </summary>
    [JsonPropertyName("addressGroups")]
    public List<CreateAddressGroupRequest> AddressGroups { get; set; } = [];

    /// <summary>
    /// Gets the ServiceObjects value.
    /// </summary>
    [JsonPropertyName("serviceObjects")]
    public List<CreateServiceObjectRequest> ServiceObjects { get; set; } = [];

    /// <summary>
    /// Gets the ServiceGroups value.
    /// </summary>
    [JsonPropertyName("serviceGroups")]
    public List<CreateServiceGroupRequest> ServiceGroups { get; set; } = [];

    /// <summary>
    /// Gets the TimeObjects value.
    /// </summary>
    [JsonPropertyName("timeObjects")]
    public List<CreateTimeObjectRequest> TimeObjects { get; set; } = [];

    /// <summary>
    /// Represents optional behavior settings for creating a workflow ticket.
    /// </summary>
    public sealed class CreateTicketOptions
    {
        /// <summary>
        /// Gets or sets whether request tasks are sorted before the ticket is saved.
        /// Omitted or null means false, preserving the request's declared build order.
        /// </summary>
        [JsonPropertyName("sortTasks")]
        public bool? SortTasks { get; set; }
    }

    /// <summary>
    /// Represents the CreateTicketRuleRequest type.
    /// </summary>
    public sealed class CreateTicketRuleRequest
    {
        /// <summary>
        /// Gets the Action value.
        /// </summary>
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Gets the Name value.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the SourceObjects value. Negative ids reference address objects declared in this request;
        /// positive ids reference existing Flow address objects.
        /// </summary>
        [JsonPropertyName("sourceObjects")]
        public List<long> SourceObjects { get; set; } = [];

        /// <summary>
        /// Gets the SourceGroups value. Negative ids reference address groups declared in this request;
        /// positive ids reference existing Flow address groups.
        /// </summary>
        [JsonPropertyName("sourceGroups")]
        public List<long> SourceGroups { get; set; } = [];

        /// <summary>
        /// Gets the DestinationObjects value. Negative ids reference address objects declared in this request;
        /// positive ids reference existing Flow address objects.
        /// </summary>
        [JsonPropertyName("destinationObjects")]
        public List<long> DestinationObjects { get; set; } = [];

        /// <summary>
        /// Gets the DestinationGroups value. Negative ids reference address groups declared in this request;
        /// positive ids reference existing Flow address groups.
        /// </summary>
        [JsonPropertyName("destinationGroups")]
        public List<long> DestinationGroups { get; set; } = [];

        /// <summary>
        /// Gets the ServiceObjects value. Negative ids reference service objects declared in this request;
        /// positive ids reference existing Flow service objects.
        /// </summary>
        [JsonPropertyName("serviceObjects")]
        public List<long> ServiceObjects { get; set; } = [];

        /// <summary>
        /// Gets the ServiceGroups value. Negative ids reference service groups declared in this request;
        /// positive ids reference existing Flow service groups.
        /// </summary>
        [JsonPropertyName("serviceGroups")]
        public List<long> ServiceGroups { get; set; } = [];

        /// <summary>
        /// Gets the TimeObjectId value. Negative ids reference a time object declared in this request;
        /// positive ids reference an existing Flow time object.
        /// </summary>
        [JsonPropertyName("timeObjectId")]
        public long TimeObjectId { get; set; }

        /// <summary>
        /// Gets the OwnerId value.
        /// </summary>
        [JsonPropertyName("ownerId")]
        public int OwnerId { get; set; }

        /// <summary>
        /// Gets the ViolationJustification value.
        /// </summary>
        [JsonPropertyName("violationJustification")]
        public string ViolationJustification { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the CreateAddressObjectRequest type.
    /// </summary>
    public sealed class CreateAddressObjectRequest
    {
        /// <summary>
        /// Gets the Id value.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Gets the Name value.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the IpStart value.
        /// </summary>
        [JsonPropertyName("ipStart")]
        public string IpStart { get; set; } = string.Empty;

        /// <summary>
        /// Gets the IpEnd value.
        /// </summary>
        [JsonPropertyName("ipEnd")]
        public string IpEnd { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the CreateAddressGroupRequest type.
    /// </summary>
    public sealed class CreateAddressGroupRequest
    {
        /// <summary>
        /// Gets the Id value.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Gets the Name value.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the MemberIds value.
        /// </summary>
        [JsonPropertyName("memberIds")]
        public List<long> MemberIds { get; set; } = [];
    }

    /// <summary>
    /// Represents the CreateServiceObjectRequest type.
    /// </summary>
    public sealed class CreateServiceObjectRequest
    {
        /// <summary>
        /// Gets the Id value.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Gets the Name value.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the Protocol value.
        /// </summary>
        [JsonPropertyName("protocol")]
        public string Protocol { get; set; } = string.Empty;

        /// <summary>
        /// Gets the PortStart value. Null for a protocol-only service (e.g. ICMP or the
        /// canonical any-IP-protocol service).
        /// </summary>
        [JsonPropertyName("portStart")]
        public int? PortStart { get; set; }

        /// <summary>
        /// Gets the PortEnd value. Null for a protocol-only service (e.g. ICMP or the
        /// canonical any-IP-protocol service).
        /// </summary>
        [JsonPropertyName("portEnd")]
        public int? PortEnd { get; set; }
    }

    /// <summary>
    /// Represents the CreateServiceGroupRequest type.
    /// </summary>
    public sealed class CreateServiceGroupRequest
    {
        /// <summary>
        /// Gets the Id value.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Gets the Name value.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the MemberIds value.
        /// </summary>
        [JsonPropertyName("memberIds")]
        public List<long> MemberIds { get; set; } = [];
    }

    /// <summary>
    /// Represents the CreateTimeObjectRequest type.
    /// </summary>
    public sealed class CreateTimeObjectRequest
    {
        /// <summary>
        /// Gets the Id value.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Gets the Name value.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the start time. Values without a timezone offset are interpreted in the middleware host's local time zone.
        /// </summary>
        [JsonPropertyName("startTime")]
        public string StartTime { get; set; } = string.Empty;

        /// <summary>
        /// Gets the end time. Values without a timezone offset are interpreted in the middleware host's local time zone.
        /// </summary>
        [JsonPropertyName("endTime")]
        public string EndTime { get; set; } = string.Empty;
    }
}
