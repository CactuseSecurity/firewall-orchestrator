using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestApproval : RequestApprovalBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("task_id"), JsonPropertyName("task_id")]
        public int TaskId { get; set; }


        public RequestApproval()
        { }

        public RequestApproval(RequestApproval approval) : base(approval)
        {
            Id = approval.Id;
            TaskId = approval.TaskId;
        }
    }
}
