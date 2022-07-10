using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTicket : RequestTicketBase
    {
        [JsonProperty("tasks"), JsonPropertyName("tasks")]
        public List<RequestTask> Tasks { get; set; } = new List<RequestTask>();


        public RequestTicket()
        {}

        public RequestTicket(RequestTicket ticket) : base(ticket)
        {
            Tasks = ticket.Tasks;
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
