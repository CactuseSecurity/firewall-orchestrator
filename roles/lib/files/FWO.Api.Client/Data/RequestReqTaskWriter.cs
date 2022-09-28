using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestReqTaskWriter : RequestReqTaskBase
    {
        [JsonProperty("elements"), JsonPropertyName("elements")]
        public RequestElementDataHelper Elements { get; set; } = new RequestElementDataHelper();

        [JsonProperty("approvals"), JsonPropertyName("approvals")]
        public RequestApprovalDataHelper Approvals { get; set; } = new RequestApprovalDataHelper();

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
        }
    }
    
    public class RequestElementDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<RequestReqElementWriter> RequestElementList { get; set; } = new List<RequestReqElementWriter>();
    }

    public class RequestApprovalDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<RequestApprovalWriter> RequestApprovalList { get; set; } = new List<RequestApprovalWriter>();
    }
}
