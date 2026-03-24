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

        [JsonProperty("active_state"), JsonPropertyName("active_state")]
        public bool ActiveState { get; set; } = true;

        public OwnerLifeCycleState()
        { }

        public OwnerLifeCycleState(OwnerLifeCycleState ownerLifeCycleState)
        {
            Id = ownerLifeCycleState.Id;
            Name = ownerLifeCycleState.Name;
            ActiveState = ownerLifeCycleState.ActiveState;
        }


        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            return shortened;
        }

        public string Display(string inactiveText)
        {
            return ActiveState ? Name : $"{Name} ({inactiveText})";
        }

        public static DateTime? GetDecommDate(DateTime? currentDecommDate, OwnerLifeCycleState? oldState, OwnerLifeCycleState? newState, DateTime nowUtc)
        {
            if (newState == null)
            {
                return currentDecommDate;
            }

            if (!newState.ActiveState && currentDecommDate == null)
            {
                return nowUtc;
            }

            if (oldState == null || oldState.ActiveState == newState.ActiveState)
            {
                return currentDecommDate;
            }

            return newState.ActiveState ? null : nowUtc;
        }
    }
}
