﻿using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    public class WfReqTask : WfReqTaskBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("ticket_id"), JsonPropertyName("ticket_id")]
        public long TicketId { get; set; }

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public List<WfReqElement> Elements { get; set; } = [];

        [JsonProperty("implementation_tasks"), JsonPropertyName("implementation_tasks")]
        public List<WfImplTask> ImplementationTasks { get; set; } = [];

        [JsonProperty("request_approvals"), JsonPropertyName("request_approvals")]
        public List<WfApproval> Approvals { get; set; } = [];

        [JsonProperty("owners"), JsonPropertyName("owners")]
        public List<FwoOwnerDataHelper> Owners { get; set; } = [];

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public List<WfCommentDataHelper> Comments { get; set; } = [];

        [JsonProperty("on_management"), JsonPropertyName("on_management")]
        public Management? OnManagement { get; set; }

        public List<WfReqElement> RemovedElements { get; set; } = [];
        public List<FwoOwner> NewOwners { get; set; } = [];
        public List<FwoOwner> RemovedOwners { get; set; } = [];

        public WfReqTask()
        { }

        public WfReqTask(WfReqTask reqtask) : base(reqtask)
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
            OnManagement = reqtask.OnManagement;
        }

        public string OwnerList()
        {
            List<string> ownerNames = [];
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
            List<NwObjectElement> elements = [];
            foreach(var reqElem in Elements)
            {
                if (reqElem.Field == field.ToString())
                {
                    elements.Add( new NwObjectElement()
                    {
                        ElemId = reqElem.Id,
                        TaskId = reqElem.TaskId,
                        Cidr = new Cidr(reqElem.Cidr != null ? reqElem.Cidr.CidrString : ""),
                        CidrEnd = new Cidr(reqElem.CidrEnd != null ? reqElem.CidrEnd.CidrString : ""),
                        IpString = reqElem.IpString ?? "",
                        NetworkId = reqElem.NetworkId,
                        RequestAction = reqElem.RequestAction,
                        Name = reqElem.Name,
                        GroupName = reqElem.GroupName ?? ""
                    });
                }
            }
            return elements;
        }

        public List<NwServiceElement> GetServiceElements()
        {
            List<NwServiceElement> elements = [];
            foreach(var reqElem in Elements)
            {
                if (reqElem.Field == ElemFieldType.service.ToString())
                {
                    elements.Add( new NwServiceElement()
                    {
                        ElemId = reqElem.Id,
                        TaskId = reqElem.TaskId,
                        Port = reqElem.Port ?? 0,
                        PortEnd = reqElem.PortEnd,
                        ProtoId = reqElem.ProtoId ?? 0,
                        ServiceId = reqElem.ServiceId,
                        Name = reqElem.Name,
                        RequestAction = reqElem.RequestAction
                    });
                }
            }
            return elements;
        }

        public List<NwRuleElement> GetRuleElements()
        {
            List<NwRuleElement> elements = [];
            foreach(var reqElem in Elements)
            {
                if (reqElem.Field == ElemFieldType.rule.ToString())
                {
                    elements.Add( new NwRuleElement()
                    {
                        ElemId = reqElem.Id,
                        TaskId = reqElem.TaskId,
                        RuleUid = reqElem.RuleUid ?? "",
                        Name = reqElem.Name
                    });
                }
            }
            return elements;
        }

        public string GetFirstCommentText()
        {
            if(Comments.Count > 0)
            {
                return Comments.First().Comment.CommentText;
            }
            return "";
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

        public bool IsNetworkFlavor()
        {
            return Elements.FirstOrDefault(e => e.IpString != null) != null;
        }
    }
}
