using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using NetTools;

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
        public Cidr Cidr { get; set; } = new();

        [JsonProperty("ip_end"), JsonPropertyName("ip_end")]
        public string IpEndString
        {
            get { return CidrEnd.CidrString; } // ?? Cidr.CidrString; }
            set { CidrEnd = new Cidr(value ?? Cidr.CidrString); }   // if End value is not set, asume host and set start ip as end ip
        }
        public Cidr CidrEnd { get; set; } = new();

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        public long TaskId { get; set; }

        public long? NetworkId { get; set; }

        public NwObjectElement()
        {}

        public NwObjectElement(string cidrString, long taskId)
        {
            Cidr = new Cidr(cidrString);
            TaskId = taskId;
        }

        public NwObjectElement(IPAddressRange ipAddressRange, long taskId)
        {
            Cidr = new Cidr(ipAddressRange.Begin.ToString());
            if(ipAddressRange.End != null && ipAddressRange.End != ipAddressRange.Begin)
            {
                CidrEnd = new Cidr(ipAddressRange.End.ToString());
            }
            TaskId = taskId;
        }

        public WfReqElement ToReqElement(ElemFieldType field)
        {
            WfReqElement element = new()
            {
                Id = ElemId,
                TaskId = TaskId,
                Field = field.ToString(),
                Cidr = new Cidr(Cidr.CidrString),
                NetworkId = NetworkId
            };
            return element;
        }

        public WfImplElement ToImplElement(ElemFieldType field)
        {
            WfImplElement element = new()
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
