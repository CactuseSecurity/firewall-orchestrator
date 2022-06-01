using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTaskWriter : RequestTaskBase
    {

        [JsonProperty("elements"), JsonPropertyName("elements")]
        public RequestElementDataHelper Elements { get; set; } = new RequestElementDataHelper();

        public RequestTaskWriter(RequestTask task) : base(task)
        {
            foreach(var element in task.Elements)
            {
                Elements.RequestElementList.Add(element);
            }
        }
    }
    
    public class RequestElementDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<RequestElementBase> RequestElementList { get; set; } = new List<RequestElementBase>();
    }

}
