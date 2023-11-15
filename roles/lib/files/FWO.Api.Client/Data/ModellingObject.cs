using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingObject
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("app_id"), JsonPropertyName("app_id")]
        public int? AppId { get; set; }

        public virtual string Display()
        {
            return Name;
        }

        public virtual string DisplayWithIcon()
        {
            return $"<span class=\"oi oi-tag\"></span> " + Display();
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            return shortened;
        }
    }
}
