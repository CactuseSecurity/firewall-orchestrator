using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTaskWriter : RequestTaskBase
    {

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public RequestElementDataHelper Elements { get; set; } = new RequestElementDataHelper();

        [JsonProperty("approvals"), JsonPropertyName("approvals")]
        public RequestApprovalDataHelper Approvals { get; set; } = new RequestApprovalDataHelper();

        public RequestTaskWriter(RequestTask task) : base(task)
        {
            foreach(var element in task.Elements)
            {
                Elements.RequestElementList.Add(new RequestElementWriter(element));
            }
            foreach(var approval in task.Approvals)
            {
                Approvals.RequestApprovalList.Add(new RequestApprovalWriter(approval));
            }
        }
    }
    
    public class RequestElementDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<RequestElementWriter> RequestElementList { get; set; } = new List<RequestElementWriter>();
    }

    public class RequestApprovalDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<RequestApprovalWriter> RequestApprovalList { get; set; } = new List<RequestApprovalWriter>();
    }
}
