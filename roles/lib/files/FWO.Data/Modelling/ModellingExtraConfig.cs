using FWO.Basics;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Modelling
{
    public class ModellingExtraConfig
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("extraConfigType"), JsonPropertyName("extraConfigType")]
        public string ExtraConfigType { get; set; } = "";

        [JsonProperty("extraConfigText"), JsonPropertyName("extraConfigText")]
        public string ExtraConfigText { get; set; } = "";

        public ModellingExtraConfig()
        {}

        public ModellingExtraConfig(ModellingExtraConfig conf)
        {
            Id = conf.Id;
            ExtraConfigType = conf.ExtraConfigType;
            ExtraConfigText = conf.ExtraConfigText;
        }

        public string Display()
        {
            return ExtraConfigType.StartsWith(GlobalConst.kDoku_) ?
                $"{ExtraConfigType[GlobalConst.kDoku_.Length..]}" : $"{ExtraConfigType}: {ExtraConfigText}";
        }

        public bool Sanitize()
        {
            bool shortened = false;
            ExtraConfigType = ExtraConfigType.SanitizeMand(ref shortened);
            ExtraConfigText = ExtraConfigText.SanitizeMand(ref shortened);
            return shortened;
        }
    }
}
