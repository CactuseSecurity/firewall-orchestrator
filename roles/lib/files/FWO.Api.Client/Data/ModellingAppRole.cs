using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingAppRole : ModellingNwGroup
    {
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

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonProperty("creator"), JsonPropertyName("creator")]
        public string? Creator { get; set; }

        [JsonProperty("creation_date"), JsonPropertyName("creation_date")]
        public DateTime? CreationDate { get; set; }

        [JsonProperty("nwobjects"), JsonPropertyName("nwobjects")]
        public List<ModellingAppServerWrapper> AppServers { get; set; } = new();

        public ModellingNetworkArea? Area { get; set; } = new();
        public int FixedPartLength;


        public void SetFixedPartLength(int fixedPartLength)
        {
            FixedPartLength = fixedPartLength;
        }

        public ModellingNwGroup ToBase()
        {
            return new ModellingNwGroup()
            {
                Id = Id,
                GroupType = GroupType,
                Name = Name,
                AppId = AppId,
                IsDeleted = IsDeleted
            };
        }

        public override string Display()
        {
            return Name + " (" + IdString + ")";
        }

        public override string DisplayWithIcon()
        {
            return $"<span class=\"oi oi-list-rich\"></span> " + Display();
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            IdString = Sanitizer.SanitizeMand(IdString, ref shortened);
            Comment = Sanitizer.SanitizeCommentOpt(Comment, ref shortened);
            return shortened;
        }
    }
    
    public class ModellingAppRoleWrapper
    {
        [JsonProperty("nwgroup"), JsonPropertyName("nwgroup")]
        public ModellingAppRole Content { get; set; } = new();

        public static ModellingAppRole[] Resolve(List<ModellingAppRoleWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }
}
