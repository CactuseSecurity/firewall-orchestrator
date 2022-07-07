using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class StatefulObject
    {
        [JsonProperty("state_id"), JsonPropertyName("state_id")]
        public int StateId { get; set; }

        private string? optComment; // else we get problems with request_task_arr_rel_insert_input in newTicket

        public string? OptComment()
        {
            return optComment;
        }

        public void SetOptComment(string? comm)
        {
            optComment = comm;
        }

        public StatefulObject()
        { }

        public StatefulObject(StatefulObject obj)
        {
            StateId = obj.StateId;
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            optComment = Sanitizer.SanitizeOpt(optComment, ref shortened);
            return shortened;
        }
    }
}
