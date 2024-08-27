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

        public void UpdateCidrStringsInTaskElements()
        {
            foreach (WfReqTask reqtask in Tasks)
            {
                foreach(WfReqElement elem in reqtask.Elements)
                {
                    elem.IpString = elem.Cidr != null && elem.Cidr.Valid ? elem.Cidr.CidrString : null ;
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
                }
                foreach(WfImplTask implTask in reqtask.ImplementationTasks)
                {
                    foreach(WfImplElement elem in implTask.ImplElements)
                    {
                        if (elem.IpString != null)
                        {
                            elem.Cidr = new Cidr(elem.IpString);
                        }
                    }
                }
            }
        }
    }
}
