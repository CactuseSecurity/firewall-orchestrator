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

        public string TooltipText = "";

        public virtual string Display()
        {
            return Name;
        }

        public virtual string DisplayHtml()
        {
            return $"<span class=\"\">{Display()}</span>";
        }

        public virtual string DisplayWithIcon()
        {
            return $"<span class=\"oi oi-tag\"></span> " + DisplayHtml();
        }

        public virtual string DisplayWithIcon(bool displayGrey)
        {
            return $"<span class=\"{(displayGrey ? "text-secondary" : "")}\">{DisplayWithIcon()}</span>";
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            return shortened;
        }
    }
}
