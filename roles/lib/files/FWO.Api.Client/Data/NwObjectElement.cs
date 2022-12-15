using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class NwObjectElement
    {

        [JsonProperty("id"), JsonPropertyName("id")]
        public long ElemId { get; set; }

        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string IpString
        {
            get { return Cidr.CidrString; }
            set { Cidr = new Cidr(value); }
        }
        public Cidr Cidr { get; set; }

        public long TaskId { get; set; }

        public long? NetworkId { get; set; }

        public NwObjectElement()
        {}

        public NwObjectElement(string cidrString, long taskId)
        {
            Cidr = new Cidr(cidrString);
            TaskId = taskId;
        }

        public RequestReqElement ToReqElement(AccessField field)
        {
            RequestReqElement element = new RequestReqElement()
            {
                Id = ElemId,
                TaskId = TaskId,
                Field = field.ToString(),
                Cidr = new Cidr(Cidr.CidrString),
                NetworkId = NetworkId
            };
            return element;
        }

        public RequestImplElement ToImplElement(AccessField field)
        {
            RequestImplElement element = new RequestImplElement()
            {
                Id = ElemId,
                ImplTaskId = TaskId,
                Field = field.ToString(),
                Cidr = new Cidr(Cidr.CidrString),
                NetworkId = NetworkId,
            };
            return element;
        }
    }
}
