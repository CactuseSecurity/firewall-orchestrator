using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class StatefulObject
    {
        [JsonProperty("state_id"), JsonPropertyName("state_id")]
        public int StateId { get; set; }


        public StatefulObject()
        { }

        public StatefulObject(StatefulObject obj)
        {
            StateId = obj.StateId;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            return shortened;
        }
    }
}
