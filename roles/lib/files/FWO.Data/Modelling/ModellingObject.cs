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

        public virtual string DisplayWithIcon(int iconPaddingStart = 0)
        {
            return $"<span class=\"{(iconPaddingStart > 0 ? $"ps-{iconPaddingStart} " : "")}{Icons.ModObject}\"></span> " + DisplayHtml();
        }

        public virtual string DisplayProblematicWithIcon()
        {
            return $"<span class=\"text-danger {Icons.Warning}\"></span><span class=\"{Icons.ModObject}\"></span> " + DisplayHtml();
        }

        public virtual string DisplayProblematicWithIcon(bool displayGrey)
        {
            return $"<span class=\"{(displayGrey ? "text-secondary" : "")}\">{DisplayProblematicWithIcon()}</span>";
        }

        public virtual string DisplayWithIcon(bool displayGrey, int iconPaddingStart = 0)
        {
            return $"<span class=\"{(displayGrey ? "text-secondary" : "")}\">{DisplayWithIcon(iconPaddingStart)}</span>";
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            return shortened;
        }
    }
}
