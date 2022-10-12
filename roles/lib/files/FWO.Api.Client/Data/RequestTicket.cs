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


        public RequestTicket()
        {}

        public RequestTicket(RequestTicket ticket) : base(ticket)
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
            foreach (RequestReqTask reqtask in Tasks)
            {
                foreach(RequestReqElement elem in reqtask.Elements)
                {
                    if (elem.Cidr != null && elem.Cidr.Valid)
                    {
                        elem.CidrString = elem.Cidr.CidrString;
                    }
                }
            }
        }

        public void UpdateCidrsInTaskElements()
        {
            foreach (RequestReqTask reqtask in Tasks)
            {
                foreach(RequestReqElement elem in reqtask.Elements)
                {
                    if (elem.CidrString != null)
                    {
                        elem.Cidr = new Cidr(elem.CidrString);
                    }
                }
                foreach(RequestImplTask implTask in reqtask.ImplementationTasks)
                {
                    foreach(RequestImplElement elem in implTask.ImplElements)
                    {
                        if (elem.CidrString != null)
                        {
                            elem.Cidr = new Cidr(elem.CidrString);
                        }
                    }
                }
            }
        }
    }
}
