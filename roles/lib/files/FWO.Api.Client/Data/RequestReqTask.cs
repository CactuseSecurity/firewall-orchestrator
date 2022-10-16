﻿using System.Text.Json.Serialization; 
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
        public List<RequestReqElement> Elements { get; set; } = new List<RequestReqElement>();

        [JsonProperty("implementation_tasks"), JsonPropertyName("implementation_tasks")]
        public List<RequestImplTask> ImplementationTasks { get; set; } = new List<RequestImplTask>();

        [JsonProperty("request_approvals"), JsonPropertyName("request_approvals")]
        public List<RequestApproval> Approvals { get; set; } = new List<RequestApproval>();

        [JsonProperty("owners"), JsonPropertyName("owners")]
        public List<RequestOwnerDataHelper> Owners { get; set; } = new List<RequestOwnerDataHelper>();

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public List<RequestCommentDataHelper> Comments { get; set; } = new List<RequestCommentDataHelper>();

        public List<RequestReqElement> RemovedElements { get; set; } = new List<RequestReqElement>();


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
        }

        public string OwnerList()
        {
            List<string> ownerNames = new List<string>();
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

        public List<NwObjectElement> getNwObjectElements(AccessField field)
        {
            List<NwObjectElement> elements = new List<NwObjectElement>();
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

        public List<NwServiceElement> getServiceElements()
        {
            List<NwServiceElement> elements = new List<NwServiceElement>();
            foreach(var implElem in Elements)
            {
                if (implElem.Field == AccessField.service.ToString())
                {
                    elements.Add( new NwServiceElement()
                    {
                        ElemId = implElem.Id,
                        TaskId = implElem.TaskId,
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
