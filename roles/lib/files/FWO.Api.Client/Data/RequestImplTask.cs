using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestImplTask: RequestTaskBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("reqtask_id"), JsonPropertyName("reqtask_id")]
        public long ReqTaskId { get; set; }

        [JsonProperty("device_id"), JsonPropertyName("device_id")]
        public int? DeviceId { get; set; }

        [JsonProperty("implementation_action"), JsonPropertyName("implementation_action")]
        public string ImplAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public List<RequestImplElement> ImplElements { get; set; } = new List<RequestImplElement>();

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public List<RequestCommentDataHelper> Comments { get; set; } = new List<RequestCommentDataHelper>();


        public List<RequestImplElement> RemovedElements { get; set; } = new List<RequestImplElement>();
        public long TicketId { get; set; }


        public RequestImplTask()
        {}

        public RequestImplTask(RequestImplTask implTask): base(implTask)
        {
            Id = implTask.Id;
            ReqTaskId = implTask.ReqTaskId;
            ImplAction = implTask.ImplAction;
            DeviceId = implTask.DeviceId;
            ImplElements = implTask.ImplElements;
            Comments = implTask.Comments;
            TicketId = implTask.TicketId;
       }


        public RequestImplTask(RequestReqTask reqtask, bool copyComments = true)
        {
            Id = 0;
            Title = reqtask.Title;
            ReqTaskId = reqtask.Id;
            TaskNumber = 0;
            StateId = 0;
            TaskType = reqtask.TaskType;
            ImplAction = reqtask.RequestAction;
            RuleAction = reqtask.RuleAction;
            Tracking = reqtask.Tracking;
            Start = null;
            Stop = null;
            ServiceGroupId = reqtask.ServiceGroupId;
            NetworkGroupId = reqtask.NetworkGroupId;
            UserGroupId = reqtask.UserGroupId;
            CurrentHandler = reqtask.CurrentHandler;
            RecentHandler = reqtask.RecentHandler;
            AssignedGroup = reqtask.AssignedGroup;
            TargetBeginDate = reqtask.TargetBeginDate;
            TargetEndDate = reqtask.TargetEndDate;
            FreeText = reqtask.FreeText;
            DeviceId = null;
            TicketId = reqtask.TicketId;
            if (reqtask.Elements != null && reqtask.Elements.Count > 0)
            {
                ImplElements = new List<RequestImplElement>();
                foreach(RequestReqElement element in reqtask.Elements)
                {
                    ImplElements.Add(new RequestImplElement(element));
                }
            }
            if(copyComments)
            {
                foreach(var comm in reqtask.Comments)
                {
                    comm.Comment.Scope = RequestObjectScopes.ImplementationTask.ToString();
                    Comments.Add(comm);
                }
            }
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
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
                        Port = implElem.Port ?? 0,
                        ProtoId = implElem.ProtoId ?? 0,
                        ServiceId = implElem.ServiceId
                    });
                }
            }
            return elements;
        }

        public string getAllComments()
        {
            string allComments = "";
            foreach(var comment in Comments)
            {
                allComments += comment.Comment.CreationDate.ToShortDateString() + " "
                            + comment.Comment.Creator.Name + ": "
                            + comment.Comment.CommentText + "\n";
            }
            return allComments;
        }
    }
}
