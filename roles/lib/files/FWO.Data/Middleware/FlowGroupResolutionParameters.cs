
namespace FWO.Data.Middleware
{
    public sealed class FlowGroupResolutionParameters
    {
        /// <summary>Maximum number of group selectors accepted by the resolver endpoint.</summary>
        public const int MaxSelectors = 100;

        /// <summary>Gets or sets requested network group IDs.</summary>
        public List<long> NetworkGroupIds { get; set; } = [];
        /// <summary>Gets or sets requested network group names.</summary>
        public List<string> NetworkGroupNames { get; set; } = [];
        /// <summary>Gets or sets requested service group IDs.</summary>
        public List<long> ServiceGroupIds { get; set; } = [];
        /// <summary>Gets or sets requested service group names.</summary>
        public List<string> ServiceGroupNames { get; set; } = [];

    }

    public sealed class FlowGroupResolutionResult
    {
        /// <summary>Gets or sets resolved network groups.</summary>
        public List<FlowNetworkGroupResolution> NetworkGroups { get; set; } = [];
        /// <summary>Gets or sets resolved service groups.</summary>
        public List<FlowServiceGroupResolution> ServiceGroups { get; set; } = [];
    }

    /// <summary>Represents a resolved network group for workflow policy checks.</summary>
    public sealed class FlowNetworkGroupResolution
    {
        /// <summary>Gets or sets the Flow group ID.</summary>
        public long Id { get; set; }
        /// <summary>Gets or sets the Flow group name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the technical members.</summary>
        public List<FlowNetworkMemberResolution> Members { get; set; } = [];
    }

    /// <summary>Represents a resolved network object used by a Flow group.</summary>
    public sealed class FlowNetworkMemberResolution
    {
        /// <summary>Gets or sets the Flow object ID.</summary>
        public long Id { get; set; }
        /// <summary>Gets or sets the object name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the start IP address.</summary>
        public string IpStart { get; set; } = string.Empty;
        /// <summary>Gets or sets the end IP address.</summary>
        public string IpEnd { get; set; } = string.Empty;
    }

    /// <summary>Represents a resolved service group for workflow policy checks.</summary>
    public sealed class FlowServiceGroupResolution
    {
        /// <summary>Gets or sets the Flow group ID.</summary>
        public long Id { get; set; }
        /// <summary>Gets or sets the Flow group name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the technical members.</summary>
        public List<FlowServiceMemberResolution> Members { get; set; } = [];
    }

    /// <summary>Represents a resolved service object used by a Flow group.</summary>
    public sealed class FlowServiceMemberResolution
    {
        /// <summary>Gets or sets the Flow object ID.</summary>
        public long Id { get; set; }
        /// <summary>Gets or sets the object name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the start port.</summary>
        public int? PortStart { get; set; }
        /// <summary>Gets or sets the end port.</summary>
        public int? PortEnd { get; set; }
        /// <summary>Gets or sets the IP protocol ID.</summary>
        public int ProtoId { get; set; }
    }
}
