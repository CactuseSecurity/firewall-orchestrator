using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class WfApproval : WfApprovalBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("task_id"), JsonPropertyName("task_id")]
        public long TaskId { get; set; }

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public List<WfCommentDataHelper> Comments { get; set; } = [];


        public WfApproval()
        { }

        public WfApproval(WfApproval approval) : base(approval)
        {
            Id = approval.Id;
            TaskId = approval.TaskId;
            Comments = approval.Comments;
        }
    }

    public class ApprovalParams
    {
        [JsonProperty("state_id"), JsonPropertyName("state_id")]
        public int StateId { get; set; }

        [JsonProperty("approver_group"), JsonPropertyName("approver_group")]
        public string ApproverGroup { get; set; } = "";

        [JsonProperty("deadline"), JsonPropertyName("deadline")]
        public int Deadline { get; set; }
    }
}
