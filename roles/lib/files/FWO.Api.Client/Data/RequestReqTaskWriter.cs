using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestReqTaskWriter : RequestReqTaskBase
    {
        [JsonProperty("elements"), JsonPropertyName("elements")]
        public RequestElementDataHelper Elements { get; set; } = new ();

        [JsonProperty("approvals"), JsonPropertyName("approvals")]
        public RequestApprovalDataHelper Approvals { get; set; } = new ();

        [JsonProperty("reqtask_owners"), JsonPropertyName("reqtask_owners")]
        public RequestOwnerDataHelper Owners { get; set; } = new ();

        public RequestReqTaskWriter(RequestReqTask reqtask) : base(reqtask)
        {
            foreach(var element in reqtask.Elements)
            {
                Elements.RequestElementList.Add(new RequestReqElementWriter(element));
            }
            foreach(var approval in reqtask.Approvals)
            {
                Approvals.RequestApprovalList.Add(new RequestApprovalWriter(approval));
            }
            foreach(var owner in reqtask.Owners)
            {
                Owners.RequestOwnerList.Add(new RequestOwnerWriter(owner.Owner));
            }
        }
    }
    
    public class RequestElementDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<RequestReqElementWriter> RequestElementList { get; set; } = new ();
    }

    public class RequestApprovalDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<RequestApprovalWriter> RequestApprovalList { get; set; } = new ();
    }

    public class RequestOwnerDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<RequestOwnerWriter> RequestOwnerList { get; set; } = new ();
    }
}
