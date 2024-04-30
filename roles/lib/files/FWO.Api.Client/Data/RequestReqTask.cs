using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestReqTask : RequestReqTaskBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("ticket_id"), JsonPropertyName("ticket_id")]
        public long TicketId { get; set; }

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public List<RequestReqElement> Elements { get; set; } = new ();

        [JsonProperty("implementation_tasks"), JsonPropertyName("implementation_tasks")]
        public List<RequestImplTask> ImplementationTasks { get; set; } = new ();

        [JsonProperty("request_approvals"), JsonPropertyName("request_approvals")]
        public List<RequestApproval> Approvals { get; set; } = new ();

        [JsonProperty("owners"), JsonPropertyName("owners")]
        public List<FwoOwnerDataHelper> Owners { get; set; } = new ();

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public List<RequestCommentDataHelper> Comments { get; set; } = new ();

        public List<RequestReqElement> RemovedElements { get; set; } = new ();
        public List<FwoOwner> NewOwners { get; set; } = new ();
        public List<FwoOwner> RemovedOwners { get; set; } = new ();

        public RequestReqTask()
        { }

        public RequestReqTask(RequestReqTask reqtask) : base(reqtask)
        {
            Id = reqtask.Id;
            TicketId = reqtask.TicketId;
            Elements = reqtask.Elements;
            ImplementationTasks = reqtask.ImplementationTasks;
            Approvals = reqtask.Approvals;
            Owners = reqtask.Owners;
            Comments = reqtask.Comments;
            RemovedElements = reqtask.RemovedElements;
            NewOwners = reqtask.NewOwners;
            RemovedOwners = reqtask.RemovedOwners;
        }

        public string OwnerList()
        {
            List<string> ownerNames = new ();
            foreach(var owner in Owners)
            {
                ownerNames.Add(owner.Owner.Name);
            }
            return string.Join(", ", ownerNames);
        }

        public int HighestImplTaskNumber()
        {
            int highestNumber = 0;
            foreach(var implTask in ImplementationTasks)
            {
                if (implTask.TaskNumber > highestNumber)
                {
                    highestNumber = implTask.TaskNumber;
                }
            }
            return highestNumber;
        }

        public List<NwObjectElement> GetNwObjectElements(ElemFieldType field)
        {
            List<NwObjectElement> elements = new ();
            foreach(var reqElem in Elements)
            {
                if (reqElem.Field == field.ToString())
                {
                    elements.Add( new NwObjectElement()
                    {
                        ElemId = reqElem.Id,
                        TaskId = reqElem.TaskId,
                        Cidr = new Cidr(reqElem.Cidr != null ? reqElem.Cidr.CidrString : ""),
                        NetworkId = reqElem.NetworkId
                    });
                }
            }
            return elements;
        }

        public List<NwServiceElement> GetServiceElements()
        {
            List<NwServiceElement> elements = new ();
            foreach(var reqElem in Elements)
            {
                if (reqElem.Field == ElemFieldType.service.ToString())
                {
                    elements.Add( new NwServiceElement()
                    {
                        ElemId = reqElem.Id,
                        TaskId = reqElem.TaskId,
                        Port = reqElem.Port ?? 0,
                        ProtoId = reqElem.ProtoId ?? 0,
                        ServiceId = reqElem.ServiceId
                    });
                }
            }
            return elements;
        }

        public List<NwRuleElement> GetRuleElements()
        {
            List<NwRuleElement> elements = new ();
            foreach(var reqElem in Elements)
            {
                if (reqElem.Field == ElemFieldType.rule.ToString())
                {
                    elements.Add( new NwRuleElement()
                    {
                        ElemId = reqElem.Id,
                        TaskId = reqElem.TaskId,
                        RuleUid = reqElem.RuleUid ?? ""
                    });
                }
            }
            return elements;
        }

        public string GetAllComments()
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

        public int GetRuleDeviceId()
        {
            foreach(var reqElem in Elements)
            {
                if (reqElem.Field == ElemFieldType.rule.ToString() && reqElem.DeviceId != null)
                {
                    return (int)reqElem.DeviceId;
                }
            }
            return 0;
        }
    }
}
