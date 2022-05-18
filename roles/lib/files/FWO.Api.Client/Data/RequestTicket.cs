using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTicket
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("title"), JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonProperty("date_created"), JsonPropertyName("date_created")]
        public DateTime CreationDate { get; set; }

        [JsonProperty("date_completed"), JsonPropertyName("date_completed")]
        public DateTime? CompletionDate { get; set; }

        [JsonProperty("state_id"), JsonPropertyName("state_id")]
        public int StateId { get; set; }

        [JsonProperty("requester"), JsonPropertyName("requester")]
        public string Requester { get; set; } = "";

        [JsonProperty("requester_group"), JsonPropertyName("requester_group")]
        public string? RequesterGroup { get; set; }

        [JsonProperty("tenant_id"), JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonProperty("tasks"), JsonPropertyName("tasks")]
        public List<RequestTask> Tasks { get; set; } = new List<RequestTask>();


        public RequestTicket()
        { }

        public RequestTicket(RequestTicket ticket)
        {
            Id = ticket.Id;
            Title = ticket.Title;
            CreationDate = ticket.CreationDate;
            CompletionDate = ticket.CompletionDate;
            StateId = ticket.StateId;
            Requester = ticket.Requester;
            RequesterGroup = ticket.RequesterGroup;
            TenantId = ticket.TenantId;
            Reason = ticket.Reason;
            Tasks = ticket.Tasks;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Title = Sanitizer.SanitizeMand(Title, ref shortened);
            Requester = Sanitizer.SanitizeMand(Requester, ref shortened);
            RequesterGroup = Sanitizer.SanitizeOpt(RequesterGroup, ref shortened);
            Reason = Sanitizer.SanitizeOpt(Reason, ref shortened);
            return shortened;
        }
    }
}
