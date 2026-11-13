using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    /// <summary>
    /// Last state change of a workflow object whose state actions the middleware has executed. As every state
    /// change executed in the middleware is recorded, the record tells since when the object is in its state.
    /// </summary>
    public class WfStateChangeExecution
    {
        /// <summary>
        /// Scope of the object (ticket, request task, implementation task, approval).
        /// </summary>
        [JsonProperty("object_scope"), JsonPropertyName("object_scope")]
        public string ObjectScope { get; set; } = "";

        /// <summary>
        /// Id of the object.
        /// </summary>
        [JsonProperty("object_id"), JsonPropertyName("object_id")]
        public long ObjectId { get; set; }

        /// <summary>
        /// State the object was moved from.
        /// </summary>
        [JsonProperty("from_state_id"), JsonPropertyName("from_state_id")]
        public int FromStateId { get; set; }

        /// <summary>
        /// State the object was moved into.
        /// </summary>
        [JsonProperty("to_state_id"), JsonPropertyName("to_state_id")]
        public int ToStateId { get; set; }

        /// <summary>
        /// Time the state change was executed.
        /// </summary>
        [JsonProperty("executed_at"), JsonPropertyName("executed_at")]
        public DateTime ExecutedAt { get; set; }
    }
}
