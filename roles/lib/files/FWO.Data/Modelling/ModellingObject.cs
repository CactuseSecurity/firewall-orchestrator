using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Basics;

namespace FWO.Data.Modelling
{
    public class ModellingObject
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("app_id"), JsonPropertyName("app_id")]
        public int? AppId { get; set; }

        public string TooltipText = "";
        public long Number;


        public ModellingObject()
        {}

        public ModellingObject(ModellingObject modellingObject)
        {
            Name = modellingObject.Name;
            AppId = modellingObject.AppId;
            TooltipText = modellingObject.TooltipText;
            Number = modellingObject.Number;
        }

        public ModellingObject(NetworkObject nwObj)
        {
            Name = nwObj.Name;
            Number = nwObj.Number;
        }

        public virtual string Display()
        {
            return Name;
        }

        public virtual string DisplayHtml()
        {
            return $"<span class=\"\">{Display()}</span>";
        }

        public virtual string DisplayProblematicWithIcon()
        {
            return $"<span class=\"{Icons.ModObject}\"></span> " + DisplayHtml() + $"<span class=\"ps-1 text-danger {Icons.Warning}\"></span>";
        }

        public virtual string DisplayProblematicWithIcon(bool displayGrey, bool decomm)
        {
            string interfClass = decomm ? "text-danger" : "text-secondary";
            return $"<span class=\"{(displayGrey ? interfClass : "")}\">{DisplayProblematicWithIcon()}</span>";
        }

        public virtual string DisplayWithIcon(bool displayGrey, bool decomm)
        {
            string interfClass = decomm ? "text-danger" : "text-secondary";
            return $"<span class=\"{(displayGrey ? interfClass : "")}\">{DisplayWithIcon()}</span>";
        }
        public virtual string DisplayWithIcon()
        {
            return $"<span class=\"{Icons.ModObject}\"></span> " + DisplayHtml();
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            Name = Name.SanitizeMand(ref shortened);
            return shortened;
        }
    }
}
