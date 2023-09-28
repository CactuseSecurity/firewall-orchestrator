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
        public Cidr Cidr { get; set; } = new Cidr();

        [JsonProperty("ip_end"), JsonPropertyName("ip_end")]
        public string IpEndString
        {
            // get { return CidrEnd?.CidrString ?? ""; }
            get { return CidrEnd?.CidrString ?? Cidr.CidrString; }
            set { CidrEnd = new Cidr(value); }
        }
        public Cidr? CidrEnd { get; set; } = new Cidr();

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

        public string DisplayIp()
        {
            return (Cidr.CidrString + (CidrEnd != null && CidrEnd.CidrString != "" ? " - " + CidrEnd.CidrString : ""));
        }

        public RequestReqElement ToReqElement(ElemFieldType field)
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

        public RequestImplElement ToImplElement(ElemFieldType field)
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
