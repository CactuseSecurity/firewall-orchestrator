using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Data
{
    public class OwnerLifeCycleState
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        public OwnerLifeCycleState()
        { }

        public OwnerLifeCycleState(OwnerLifeCycleState ownerLifeCycleState)
        {
            Id = ownerLifeCycleState.Id;
            Name = ownerLifeCycleState.Name;
        }


        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            return shortened;
        }
    }
}
