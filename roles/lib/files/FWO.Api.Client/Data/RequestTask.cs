using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace FWO.Api.Data
{
    public class RequestTask : RequestTaskBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("ticket_id"), JsonPropertyName("ticket_id")]
        public int TicketId { get; set; }

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public List<RequestElement> Elements { get; set; } = new List<RequestElement>();

        [JsonProperty("implementation_tasks"), JsonPropertyName("implementation_tasks")]
        public List<ImplementationTask> ImplementationTasks { get; set; } = new List<ImplementationTask>();

        [JsonProperty("request_approvals"), JsonPropertyName("request_approvals")]
        public List<RequestApproval> Approvals { get; set; } = new List<RequestApproval>();

        [JsonProperty("owners"), JsonPropertyName("owners")]
        public List<RequestOwnerDataHelper> Owners { get; set; } = new List<RequestOwnerDataHelper>();

        public RequestTask()
        { }

        public RequestTask(RequestTask task) : base(task)
        {
            Id = task.Id;
            TicketId = task.TicketId;
            Elements = task.Elements;
            ImplementationTasks = task.ImplementationTasks;
            Approvals = task.Approvals;
            Owners = task.Owners;
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

        public RuleElement? RuleElement(RuleField field)
        {
            RuleElement? element = null;
            RequestElement? reqElem = Elements.FirstOrDefault(x => x.Field == field.ToString());
            if (reqElem != null)
            {
                element = new RuleElement()
                {
                    ReqElemId = reqElem.Id,
                    Ip = reqElem.Ip,
                    Port = reqElem.Port,
                    ProtoId = reqElem.ProtoId,
                    NetworkId = reqElem.NetworkId
                };
            }
            return element;
        }
    }

    public class RuleElement
    {
        public int ReqElemId { get; set; }
        public string Ip { get; set; } = "";
        // public IPNetwork Ip { get; set; } = new IPNetwork(new IPAddress(0), 32);
        public int Port { get; set; }
        public int? ProtoId { get; set; }
        public long? NetworkId { get; set; }

        public RequestElement ToElement(RuleField field)
        {
            RequestElement element = new RequestElement()
            {
                Id = ReqElemId,
                Field = field.ToString(),
                Ip = Ip,
                Port = Port,
                ProtoId = ProtoId,
                NetworkId = NetworkId
            };
            return element;
        }
    }
}
