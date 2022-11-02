using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestApprovalBase : RequestStatefulObject
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

        [JsonProperty("initial_approval"), JsonPropertyName("initial_approval")]
        public bool InitialApproval { get; set; } = true;


        public RequestApprovalBase()
        { }

        public RequestApprovalBase(RequestApprovalBase approval) : base(approval)
        {
            DateOpened = approval.DateOpened;
            ApprovalDate = approval.ApprovalDate;
            Deadline = approval.Deadline;
            ApproverGroup = approval.ApproverGroup;
            ApproverDn = approval.ApproverDn;
            TenantId = approval.TenantId;
            InitialApproval = approval.InitialApproval;
         }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            ApproverGroup = Sanitizer.SanitizeLdapPathOpt(ApproverGroup, ref shortened);
            ApproverDn = Sanitizer.SanitizeLdapPathOpt(ApproverDn, ref shortened);
            return shortened;
        }
    }
}
