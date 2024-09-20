using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class WfTicket : WfTicketBase
    {
        [JsonProperty("reqtasks"), JsonPropertyName("reqtasks")]
        public List<WfReqTask> Tasks { get; set; } = new ();

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public List<WfCommentDataHelper> Comments { get; set; } = new ();


        public WfTicket()
        {}

        public WfTicket(WfTicket ticket) : base(ticket)
        {
            Tasks = ticket.Tasks;
            Comments = ticket.Comments;
        }

        public int HighestTaskNumber()
        {
            int highestNumber = 0;
            foreach(var reqtask in Tasks)
            {
                if (reqtask.TaskNumber > highestNumber)
                {
                    highestNumber = reqtask.TaskNumber;
                }
            }
            return highestNumber;
        }

        public int NumberImplTasks()
        {
            int numberImplTasks = 0;
            foreach(var reqtask in Tasks)
            {
                numberImplTasks += reqtask.ImplementationTasks.Count;
            }
            return numberImplTasks;
        }

        public void UpdateIpStringsFromCidrInTaskElements()
        {
            foreach (WfReqTask reqtask in Tasks)
            {
                foreach(WfReqElement elem in reqtask.Elements)
                {
                    elem.IpString = elem.Cidr != null && elem.Cidr.Valid ? elem.Cidr.CidrString : null;
                    elem.IpEnd = elem.CidrEnd != null && elem.CidrEnd.Valid ? elem.CidrEnd.CidrString : null;
                }
            }
        }

        public void UpdateCidrsInTaskElements()
        {
            foreach (WfReqTask reqtask in Tasks)
            {
                foreach(WfReqElement elem in reqtask.Elements)
                {
                    if (elem.IpString != null)
                    {
                        elem.Cidr = new Cidr(elem.IpString);
                    }
                    if (elem.IpEnd != null)
                    {
                        elem.CidrEnd = new Cidr(elem.IpEnd);
                    }
                }
                foreach(WfImplTask implTask in reqtask.ImplementationTasks)
                {
                    foreach(WfImplElement elem in implTask.ImplElements)
                    {
                        if (elem.IpString != null)
                        {
                            elem.Cidr = new Cidr(elem.IpString);
                        }
                        if (elem.IpEnd != null)
                        {
                            elem.CidrEnd = new Cidr(elem.IpEnd);
                        }
                    }
                }
            }
        }
    }
}
