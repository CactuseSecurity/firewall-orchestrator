using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ImplementationTask : RequestTask
    {
        [JsonProperty("request_task_id"), JsonPropertyName("request_task_id")]
        public int ReqTaskId { get; set; }

        [JsonProperty("deviceId"), JsonPropertyName("deviceId")]
        public int? DeviceId { get; set; }

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public List<ImplementationElement> ImplElements { get; set; } = new List<ImplementationElement>();


        public ImplementationTask()
        { }

        public ImplementationTask(RequestTask task)
        {
            Id = 0;
            ReqTaskId = task.Id;
            Title = "Implementation for " + task.Title;
            TicketId = task.TicketId;
            TaskNumber = 0;
            StateId = 0;
            TaskType = task.TaskType;
            RequestAction = task.RequestAction;
            RuleAction = task.RuleAction;
            Tracking = task.Tracking;
            Start = null;
            Stop = null;
            ServiceGroupId = task.ServiceGroupId;
            NetworkGroupId = task.NetworkGroupId;
            Reason = task.Reason;
            if (task.Elements != null && task.Elements.Count > 0)
            {
                ImplElements = new List<ImplementationElement>();
                foreach(RequestElement element in task.Elements)
                {
                    ImplElements.Add(new ImplementationElement(element));
                }
            }
        }
    }
}
