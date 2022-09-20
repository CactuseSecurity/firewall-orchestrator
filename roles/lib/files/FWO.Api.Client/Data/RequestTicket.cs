using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTicket : RequestTicketBase
    {
        [JsonProperty("tasks"), JsonPropertyName("tasks")]
        public List<RequestTask> Tasks { get; set; } = new List<RequestTask>();

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
            foreach(var task in Tasks)
            {
                if (task.TaskNumber > highestNumber)
                {
                    highestNumber = task.TaskNumber;
                }
            }
            return highestNumber;
        }

        public int NumberImplTasks()
        {
            int numberImplTasks = 0;
            foreach(var task in Tasks)
            {
                numberImplTasks += task.ImplementationTasks.Count;
            }
            return numberImplTasks;
        }

        public void UpdateCidrStringsInTaskElements()
        {
            foreach (RequestTask task in Tasks)
            {
                foreach(RequestElement elem in task.Elements)
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
            foreach (RequestTask task in Tasks)
            {
                foreach(RequestElement elem in task.Elements)
                {
                    if (elem.CidrString != null)
                    {
                        elem.Cidr = new Cidr(elem.CidrString);
                    }
                }
                foreach(ImplementationTask implTask in task.ImplementationTasks)
                {
                    foreach(ImplementationElement elem in implTask.ImplElements)
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
