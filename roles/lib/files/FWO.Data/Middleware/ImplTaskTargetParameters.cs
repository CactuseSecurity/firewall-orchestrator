using System.Text.Json.Serialization;

namespace FWO.Data.Middleware
{
    /// <summary>
    /// Asks the middleware for the target managements of an object task (POST api/workflow/getImplTaskTargets).
    /// </summary>
    public class ImplTaskTargetParameters
    {
        /// <summary>
        /// Database id of the management the request task belongs to.
        /// </summary>
        [JsonPropertyName("managementId")]
        public int ManagementId { get; set; }

        /// <summary>
        /// True for services, false for network objects.
        /// </summary>
        [JsonPropertyName("isServiceObject")]
        public bool IsServiceObject { get; set; }

        /// <summary>
        /// True for time objects; selects timeObjectCreation. Not to be combined with IsServiceObject.
        /// </summary>
        [JsonPropertyName("isTimeObject")]
        public bool IsTimeObject { get; set; }

        /// <summary>
        /// Paging options; the page size is left to the middleware default, which is its maximum.
        /// </summary>
        [JsonPropertyName("options")]
        public ImplTaskTargetPaging Options { get; set; } = new();
    }

    /// <summary>
    /// Paging of the target management lookup.
    /// </summary>
    public class ImplTaskTargetPaging
    {
        /// <summary>
        /// Response header of the middleware telling whether further targets follow the returned page.
        /// </summary>
        public const string kHasMoreHeader = "X-Has-More";

        /// <summary>
        /// Number of targets to skip, i.e. the number of targets already received.
        /// </summary>
        [JsonPropertyName("offset")]
        public int Offset { get; set; }
    }

    /// <summary>
    /// Target managements returned by the middleware for an object task.
    /// </summary>
    public class ImplTaskTargetResult
    {
        /// <summary>
        /// The target managements (id and name).
        /// </summary>
        [JsonPropertyName("targets")]
        public List<Management> Targets { get; set; } = [];
    }
}
