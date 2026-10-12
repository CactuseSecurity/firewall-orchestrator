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
