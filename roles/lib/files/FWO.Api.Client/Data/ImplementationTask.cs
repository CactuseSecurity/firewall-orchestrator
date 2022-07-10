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

        public List<ImplementationElement> RemovedElements { get; set; } = new List<ImplementationElement>();

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

        public List<NwObjectElement> getNwObjectElements(AccessField field)
        {
            List<NwObjectElement> elements = new List<NwObjectElement>();
            foreach(var implElem in ImplElements)
            {
                if (implElem.Field == field.ToString())
                {
                    elements.Add( new NwObjectElement()
                    {
                        ElemId = implElem.Id,
                        TaskId = implElem.ImplTaskId,
                        Cidr = new Cidr(implElem.Cidr != null ? implElem.Cidr.CidrString : ""),
                        NetworkId = implElem.NetworkId
                    });
                }
            }
            return elements;
        }

        public List<NwServiceElement> getServiceElements()
        {
            List<NwServiceElement> elements = new List<NwServiceElement>();
            foreach(var implElem in ImplElements)
            {
                if (implElem.Field == AccessField.service.ToString())
                {
                    elements.Add( new NwServiceElement()
                    {
                        ElemId = implElem.Id,
                        TaskId = implElem.ImplTaskId,
                        Port = implElem.Port,
                        ProtoId = implElem.ProtoId,
                        ServiceId = implElem.ServiceId
                    });
                }
            }
            return elements;
        }
    }
}
