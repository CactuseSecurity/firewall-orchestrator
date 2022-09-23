using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestImplTask: RequestTaskBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("request_task_id"), JsonPropertyName("request_task_id")]
        public long ReqTaskId { get; set; }

        [JsonProperty("implementation_action"), JsonPropertyName("implementation_action")]
        public string ImplAction { get; set; } = FWO.Api.Data.RequestAction.create.ToString();

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public List<RequestImplElement> ImplElements { get; set; } = new List<RequestImplElement>();

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public List<RequestCommentDataHelper> Comments { get; set; } = new List<RequestCommentDataHelper>();


        public List<RequestImplElement> RemovedElements { get; set; } = new List<RequestImplElement>();
        public long TicketId { get; set; }


        public RequestImplTask()
        { }

        public RequestImplTask(RequestReqTask task, bool copyComments = true)
        {
            Id = 0;
            Title = task.Title;
            ReqTaskId = task.Id;
            TaskNumber = 0;
            StateId = 0;
            TaskType = task.TaskType;
            ImplAction = task.RequestAction;
            RuleAction = task.RuleAction;
            Tracking = task.Tracking;
            Start = null;
            Stop = null;
            ServiceGroupId = task.ServiceGroupId;
            NetworkGroupId = task.NetworkGroupId;
            UserGroupId = task.UserGroupId;
            CurrentHandler = task.CurrentHandler;
            RecentHandler = task.RecentHandler;
            AssignedGroup = task.AssignedGroup;
            TargetBeginDate = task.TargetBeginDate;
            TargetEndDate = task.TargetEndDate;
            FreeText = task.FreeText;
            DeviceId = task.DeviceId;
            if (task.Elements != null && task.Elements.Count > 0)
            {
                ImplElements = new List<RequestImplElement>();
                foreach(RequestReqElement element in task.Elements)
                {
                    ImplElements.Add(new RequestImplElement(element));
                }
            }
            if(copyComments)
            {
                foreach(var comm in task.Comments)
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
                        Port = implElem.Port,
                        ProtoId = implElem.ProtoId,
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
