using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class WfReqTaskWriter : WfReqTaskBase
    {
        [JsonProperty("elements"), JsonPropertyName("elements")]
        public WfElementDataHelper Elements { get; set; } = new ();

        [JsonProperty("approvals"), JsonPropertyName("approvals")]
        public WfApprovalDataHelper Approvals { get; set; } = new ();

        [JsonProperty("reqtask_owners"), JsonPropertyName("reqtask_owners")]
        public WfOwnerDataHelper Owners { get; set; } = new ();

        public WfReqTaskWriter(WfReqTask reqtask) : base(reqtask)
        {
            foreach(var element in reqtask.Elements)
            {
                Elements.WfElementList.Add(new WfReqElementWriter(element));
            }
            foreach(var approval in reqtask.Approvals)
            {
                Approvals.WfApprovalList.Add(new WfApprovalWriter(approval));
            }
            foreach(var owner in reqtask.Owners)
            {
                Owners.WfOwnerList.Add(new WfOwnerWriter(owner.Owner));
            }
        }
    }
    
    public class WfElementDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<WfReqElementWriter> WfElementList { get; set; } = new ();
    }

    public class WfApprovalDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<WfApprovalWriter> WfApprovalList { get; set; } = new ();
    }

    public class WfOwnerDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<WfOwnerWriter> WfOwnerList { get; set; } = new ();
    }
}
