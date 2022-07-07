using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ImplementationTask: TaskBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("request_task_id"), JsonPropertyName("request_task_id")]
        public int ReqTaskId { get; set; }

        [JsonProperty("implementation_task_number"), JsonPropertyName("implementation_task_number")]
        public int ImplTaskNumber { get; set; }

        [JsonProperty("device_id"), JsonPropertyName("device_id")]
        public int? DeviceId { get; set; }

        [JsonProperty("implementation_action"), JsonPropertyName("implementation_action")]
        public string ImplAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public List<ImplementationElement> ImplElements { get; set; } = new List<ImplementationElement>();


        public ImplementationTask()
        { }

        public ImplementationTask(RequestTask task)
        {
            Id = 0;
            ReqTaskId = task.Id;
            ImplTaskNumber = 0;
            StateId = 0;
            ImplAction = task.RequestAction;
            RuleAction = task.RuleAction;
            Tracking = task.Tracking;
            Start = null;
            Stop = null;
            ServiceGroupId = task.ServiceGroupId;
            NetworkGroupId = task.NetworkGroupId;
            UserGroupId = task.UserGroupId;
            CurrentHandler = task.CurrentHandler;
            TargetBeginDate = task.TargetBeginDate;
            TargetEndDate = task.TargetEndDate;
            FwAdminComments = task.FwAdminComments;
            if (task.Elements != null && task.Elements.Count > 0)
            {
                ImplElements = new List<ImplementationElement>();
                foreach(RequestElement element in task.Elements)
                {
                    ImplElements.Add(new ImplementationElement(element));
                }
            }
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            FwAdminComments = Sanitizer.SanitizeOpt(FwAdminComments, ref shortened);
            return shortened;
        }

        public RuleElement? getRuleElement(RuleField field)
        {
            RuleElement? element = null;
            ImplementationElement? implElem = ImplElements.FirstOrDefault(x => x.Field == field.ToString());
            if (implElem != null)
            {
                element = new RuleElement()
                {
                    ElemId = implElem.Id,
                    TaskId = implElem.ImplTaskId,
                    Ip = implElem.Ip,
                    Port = implElem.Port,
                    ProtoId = implElem.ProtoId,
                    NetworkId = implElem.NetworkId,
                    ServiceId = implElem.ServiceId
                };
            }
            return element;
        }
    }
}
