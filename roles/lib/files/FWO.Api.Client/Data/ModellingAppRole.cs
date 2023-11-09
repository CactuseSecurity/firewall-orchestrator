using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingAppRole
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("app_id"), JsonPropertyName("app_id")]
        public int AppId { get; set; }

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

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonProperty("creator"), JsonPropertyName("creator")]
        public string? Creator { get; set; }

        [JsonProperty("creation_date"), JsonPropertyName("creation_date")]
        public DateTime? CreationDate { get; set; }

        [JsonProperty("nwobjects"), JsonPropertyName("nwobjects")]
        public List<ModellingAppServerWrapper> AppServers { get; set; } = new();

        public ModellingNetworkArea Area { get; set; }
        public const int FixedPartLength = 4;


        public bool Sanitize()
        {
            bool shortened = false;
            IdString = Sanitizer.SanitizeMand(IdString, ref shortened);
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
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
