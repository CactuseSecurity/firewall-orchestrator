using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestApprovalBase
    {
        [JsonProperty("date_opened"), JsonPropertyName("date_opened")]
        public DateTime DateOpened { get; set; } = DateTime.Now;

        [JsonProperty("approval_date"), JsonPropertyName("approval_date")]
        public DateTime? ApprovalDate { get; set; }

        [JsonProperty("approval_deadline"), JsonPropertyName("approval_deadline")]
        public DateTime? Deadline { get; set; }

        [JsonProperty("approver_group"), JsonPropertyName("approver_group")]
        public string? ApproverGroup { get; set; }

//        [JsonProperty("approver"), JsonPropertyName("approver")]
//        public UiUser? Approver { get; set; }

        [JsonProperty("approver"), JsonPropertyName("approver")]
        public string? ApproverDn { get; set; } = "";

        [JsonProperty("tenant_id"), JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonProperty("initial_approval"), JsonPropertyName("initial_approval")]
        public bool InitialApproval { get; set; } = true;

        [JsonProperty("state_id"), JsonPropertyName("state_id")]
        public int StateId { get; set; }


        public RequestApprovalBase()
        { }

        public RequestApprovalBase(RequestApprovalBase approval)
        {
            DateOpened = approval.DateOpened;
            ApprovalDate = approval.ApprovalDate;
            Deadline = approval.Deadline;
            ApproverGroup = approval.ApproverGroup;
            ApproverDn = approval.ApproverDn;
            TenantId = approval.TenantId;
            Comment = approval.Comment;
            InitialApproval = approval.InitialApproval;
            StateId = approval.StateId;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            ApproverGroup = Sanitizer.SanitizeLdapPathOpt(ApproverGroup, ref shortened);
            ApproverDn = Sanitizer.SanitizeLdapPathOpt(ApproverDn, ref shortened);
            Comment = Sanitizer.SanitizeOpt(Comment, ref shortened);
            return shortened;
        }
    }
}
