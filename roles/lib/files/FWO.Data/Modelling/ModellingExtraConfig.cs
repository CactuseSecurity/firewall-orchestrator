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
        { }

        public ModellingExtraConfig(ModellingExtraConfig conf)
        {
            Id = conf.Id;
            ExtraConfigType = conf.ExtraConfigType;
            ExtraConfigText = conf.ExtraConfigText;
        }

        /// <summary>
        /// Whether the entry names a user object, e.g. a Check Point access role, standing in for a placeholder area of the
        /// "Network Areas for Users": its type ends with "_user" or "-user", ignoring case.
        /// </summary>
        public bool IsSpecialUser()
        {
            return ExtraConfigType.EndsWith(GlobalConst.k_user, StringComparison.OrdinalIgnoreCase)
                || ExtraConfigType.EndsWith(GlobalConst.k_user2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether the entry names an updatable object standing in for a placeholder area of the "Network Areas for
        /// Updatable Objects": its type starts with "updatable", ignoring case.
        /// </summary>
        public bool IsUpdatableObject() => ExtraConfigType.StartsWith(GlobalConst.kUpdatable, StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether the entry marks its connection as for documentation only: its type starts with "Doku_".</summary>
        public bool IsDocumentationOnly() => ExtraConfigType.StartsWith(GlobalConst.kDoku_);

        /// <summary>Whether the entry asks for network address translation: its type is "NAT", ignoring case.</summary>
        public bool IsNat() => string.Equals(ExtraConfigType, GlobalConst.kNAT, StringComparison.OrdinalIgnoreCase);

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
