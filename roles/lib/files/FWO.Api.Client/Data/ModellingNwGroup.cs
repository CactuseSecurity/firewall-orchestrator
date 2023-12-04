using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingNwGroup : ModellingNwObject
    {
        [JsonProperty("group_type"), JsonPropertyName("group_type")]
        public int GroupType { get; set; }

        [JsonProperty("id_string"), JsonPropertyName("id_string")]
        public string IdString { get; set; } = "";
        public string IdStringFixedPart
        { 
            get
            { 
                return IdString.Length >= FixedPartLength ? IdString.Substring(0, FixedPartLength) : IdString;
            }
            set
            { 
                if(IdString.Length >= FixedPartLength)
                {
                    IdString = value + IdString.Substring(FixedPartLength);
                }
                else
                {
                    IdString = value;
                }
            }
        }
        public string IdStringFreePart
        {
            get
            {
                return IdString.Length >= FixedPartLength ? IdString.Substring(FixedPartLength) : "";
            }
            set
            {
                if(IdString.Length >= FixedPartLength)
                {
                    IdString = IdString.Substring(0, FixedPartLength) + value;
                }
                else
                {
                    for (int i = 0; i < FixedPartLength - IdString.Length; i++)
                    {
                        IdString += " ";
                    }
                    IdString += value;
                }
            }
        }

        public int FixedPartLength;

        public void SetFixedPartLength(int fixedPartLength)
        {
            FixedPartLength = fixedPartLength;
        }

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
            return $"<span class=\"oi oi-folder\"></span> " + DisplayHtml();
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            IdString = Sanitizer.SanitizeMand(IdString, ref shortened);
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
