using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingNwGroup : ModellingNwObject
    {
        [JsonProperty("group_type"), JsonPropertyName("group_type")]
        public int GroupType { get; set; }

        [JsonProperty("id_string"), JsonPropertyName("id_string")]
        public string IdString
        {
            get { return ManagedIdString.Whole; }
            set { ManagedIdString = new (value); }
        }
        public ModellingManagedIdString ManagedIdString { get; set; } = new ();


        public override string Display()
        {
            return base.Display() + " (" + IdString + ")";
        }

        public override string DisplayHtml()
        {
            return $"<span><b>{base.DisplayHtml()}</b></span>";
        }

        public override string DisplayWithIcon()
        {
            return $"<span class=\"{Icons.NwGroup}\"></span> " + DisplayHtml();
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            ManagedIdString.FreePart = Sanitizer.SanitizeMand(ManagedIdString.FreePart, ref shortened);
            return shortened;
        }

    }
    
    public class ModellingNwGroupWrapper
    {
        [JsonProperty("nwgroup"), JsonPropertyName("nwgroup")]
        public ModellingNwGroup Content { get; set; } = new();

        public static ModellingNwGroup[] Resolve(List<ModellingNwGroupWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }
}
