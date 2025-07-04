﻿using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    public class WfImplTask: WfTaskBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("reqtask_id"), JsonPropertyName("reqtask_id")]
        public long ReqTaskId { get; set; }

        [JsonProperty("device_id"), JsonPropertyName("device_id")]
        public int? DeviceId { get; set; }

        [JsonProperty("implementation_action"), JsonPropertyName("implementation_action")]
        public string ImplAction { get; set; } = RequestAction.create.ToString();

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public List<WfImplElement> ImplElements { get; set; } = [];

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public List<WfCommentDataHelper> Comments { get; set; } = [];


        public List<WfImplElement> RemovedElements { get; set; } = [];
        public long TicketId { get; set; }


        public WfImplTask()
        {}

        public WfImplTask(WfImplTask implTask): base(implTask)
        {
            Id = implTask.Id;
            ReqTaskId = implTask.ReqTaskId;
            ImplAction = implTask.ImplAction;
            DeviceId = implTask.DeviceId;
            ImplElements = implTask.ImplElements;
            Comments = implTask.Comments;
            TicketId = implTask.TicketId;
       }


        public WfImplTask(WfReqTask reqtask, bool copyComments = true)
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
                if(reqtask.TaskType == WfTaskType.rule_delete.ToString())
                {
                    DeviceId = reqtask.Elements[0].DeviceId;
                }
                ImplElements = new List<WfImplElement>();
                foreach(WfReqElement element in reqtask.Elements)
                {
                    ImplElements.Add(new WfImplElement(element));
                }
            }
            if(copyComments)
            {
                foreach(var comm in reqtask.Comments)
                {
                    comm.Comment.Scope = WfObjectScopes.ImplementationTask.ToString();
                    Comments.Add(comm);
                }
            }
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            return shortened;
        }

        public List<NwObjectElement> GetNwObjectElements(ElemFieldType field)
        {
            List<NwObjectElement> elements = [];
            foreach(var implElem in ImplElements)
            {
                if (implElem.Field == field.ToString())
                {
                    elements.Add( new NwObjectElement()
                    {
                        ElemId = implElem.Id,
                        TaskId = implElem.ImplTaskId,
                        Cidr = new Cidr(implElem.Cidr != null ? implElem.Cidr.CidrString : ""),
                        IpString = implElem.IpString ?? "",
                        NetworkId = implElem.NetworkId,
                        Name = implElem.Name
                    });
                }
            }
            return elements;
        }

        public List<NwServiceElement> GetServiceElements()
        {
            List<NwServiceElement> elements = [];
            foreach(var implElem in ImplElements)
            {
                if (implElem.Field == ElemFieldType.service.ToString())
                {
                    elements.Add( new NwServiceElement()
                    {
                        ElemId = implElem.Id,
                        TaskId = implElem.ImplTaskId,
                        Port = implElem.Port ?? 0,
                        PortEnd = implElem.PortEnd,
                        ProtoId = implElem.ProtoId ?? 0,
                        ServiceId = implElem.ServiceId
                    });
                }
            }
            return elements;
        }

        public List<NwRuleElement> GetRuleElements()
        {
            List<NwRuleElement> elements = [];
            foreach(var implElem in ImplElements)
            {
                if (implElem.Field == ElemFieldType.rule.ToString())
                {
                    elements.Add( new NwRuleElement()
                    {
                        ElemId = implElem.Id,
                        TaskId = implElem.ImplTaskId,
                        RuleUid = implElem.RuleUid ?? "",
                        Name = implElem.Name
                    });
                }
            }
            return elements;
        }
    }
}
