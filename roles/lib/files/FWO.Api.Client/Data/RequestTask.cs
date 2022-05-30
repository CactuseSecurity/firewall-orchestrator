using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTask : RequestTaskBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("ticket_id"), JsonPropertyName("ticket_id")]
        public int TicketId { get; set; }

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public List<RequestElement> Elements { get; set; } = new List<RequestElement>();

        [JsonProperty("implementation_tasks"), JsonPropertyName("implementation_tasks")]
        public List<ImplementationTask> ImplementationTasks { get; set; } = new List<ImplementationTask>();

        public RequestTask()
        { }

        public RequestTask(RequestTask task)
        {
            Id = task.Id;
            Title = task.Title;
            TicketId = task.TicketId;
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
            Elements = task.Elements;
            ImplementationTasks = task.ImplementationTasks;
        }
    }
}
