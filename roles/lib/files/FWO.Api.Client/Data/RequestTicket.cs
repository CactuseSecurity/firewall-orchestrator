using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTicket : RequestTicketBase
    {
        [JsonProperty("reqtasks"), JsonPropertyName("reqtasks")]
        public List<RequestReqTask> Tasks { get; set; } = new List<RequestReqTask>();

        [JsonProperty("comments"), JsonPropertyName("comments")]
        public List<RequestCommentDataHelper> Comments { get; set; } = new List<RequestCommentDataHelper>();

        public FwoOwner? RelevantOwner { get; set; }

        public RequestTicket()
        {}

        public RequestTicket(RequestTicket ticket) : base(ticket)
        {
            Tasks = ticket.Tasks;
            Comments = ticket.Comments;
            RelevantOwner = ticket.RelevantOwner;
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
            foreach (RequestReqTask reqtask in Tasks)
            {
                foreach(RequestReqElement elem in reqtask.Elements)
                {
                    elem.IpString = (elem.Cidr != null && elem.Cidr.Valid ? elem.Cidr.CidrString : null) ;
                }
            }
        }

        public void UpdateCidrsInTaskElements()
        {
            foreach (RequestReqTask reqtask in Tasks)
            {
                foreach(RequestReqElement elem in reqtask.Elements)
                {
                    if (elem.IpString != null)
                    {
                        elem.Cidr = new Cidr(elem.IpString);
                    }
                }
                foreach(RequestImplTask implTask in reqtask.ImplementationTasks)
                {
                    foreach(RequestImplElement elem in implTask.ImplElements)
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
