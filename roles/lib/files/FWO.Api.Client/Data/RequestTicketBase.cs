using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTicketBase: StatefulObject
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("title"), JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonProperty("date_created"), JsonPropertyName("date_created")]
        public DateTime CreationDate { get; set; }

        [JsonProperty("date_completed"), JsonPropertyName("date_completed")]
        public DateTime? CompletionDate { get; set; }

        [JsonProperty("requester"), JsonPropertyName("requester")]
        public UiUser? Requester { get; set; }

        [JsonProperty("requester_dn"), JsonPropertyName("requester_dn")]
        public string? RequesterDn { get; set; } = "";

        [JsonProperty("requester_group"), JsonPropertyName("requester_group")]
        public string? RequesterGroup { get; set; }

        [JsonProperty("tenant_id"), JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        [JsonProperty("reason"), JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonProperty("external_ticket_id"), JsonPropertyName("external_ticket_id")]
        public string? ExternalTicketId { get; set; }

        [JsonProperty("external_ticket_source"), JsonPropertyName("external_ticket_source")]
        public int? ExternalTicketSource { get; set; }


        public RequestTicketBase()
        { }

        public RequestTicketBase(RequestTicketBase ticket) : base(ticket)
        {
            Id = ticket.Id;
            Title = ticket.Title;
            CreationDate = ticket.CreationDate;
            CompletionDate = ticket.CompletionDate;
            Requester = ticket.Requester;
            RequesterDn = ticket.RequesterDn;
            RequesterGroup = ticket.RequesterGroup;
            TenantId = ticket.TenantId;
            Reason = ticket.Reason;
            ExternalTicketId = ticket.ExternalTicketId;
            ExternalTicketSource = ticket.ExternalTicketSource;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Title = Sanitizer.SanitizeMand(Title, ref shortened);
            RequesterDn = Sanitizer.SanitizeLdapPathOpt(RequesterDn, ref shortened);
            RequesterGroup = Sanitizer.SanitizeLdapPathOpt(RequesterGroup, ref shortened);
            Reason = Sanitizer.SanitizeOpt(Reason, ref shortened);
            ExternalTicketId = Sanitizer.SanitizeOpt(ExternalTicketId, ref shortened);
            return shortened;
        }
    }
}
