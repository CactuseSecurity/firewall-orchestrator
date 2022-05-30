using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTaskWriter : RequestTaskBase
    {

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public RequestElementDataHelper Elements { get; set; } = new RequestElementDataHelper();

        public RequestTaskWriter(RequestTask task)
        {
            Title = task.Title;
            TaskNumber = task.TaskNumber;
            StateId = task.StateId;
            TaskType = task.TaskType;
            RequestAction = task.RequestAction;
            RuleAction = task.RuleAction;
            Tracking = task.Tracking;
            Start = task.Start;
            Stop = task.Stop;
            ServiceGroupId = task.ServiceGroupId;
            NetworkGroupId = task.NetworkGroupId;
            Reason = task.Reason;
            foreach(var element in task.Elements)
            {
                Elements.RequestElementList.Add(element);
            }
        }
    }
    
    public class RequestElementDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<RequestElementBase> RequestElementList { get; set; } = new List<RequestElementBase>();
    }

}
