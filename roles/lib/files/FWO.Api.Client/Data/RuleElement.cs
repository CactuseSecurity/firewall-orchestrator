using NetTools;

namespace FWO.Api.Data
{
    public class RuleElement
    {
        public int ElemId { get; set; }
        public int TaskId { get; set; }
        public IPAddressRange Ip { get; set; } = new IPAddressRange();
        public string IpString 
        {
            get => Ip.ToCidrString();
            set => Parse(value);
        }
        public int Port { get; set; }
        public int? ProtoId { get; set; } = 6;
        public long? NetworkId { get; set; }
        public long? ServiceId { get; set; }

        private void Parse(string value)
        {
            try 
            {
                Ip = IPAddressRange.Parse(value);
            } 
            catch(Exception)
            {}
        }

        public RequestElement ToReqElement(RuleField field)
        {
            RequestElement element = new RequestElement()
            {
                Id = ElemId,
                TaskId = TaskId,
                Field = field.ToString(),
                Ip = Ip,
                Port = Port,
                ProtoId = ProtoId,
                NetworkId = NetworkId,
                ServiceId = ServiceId
            };
            return element;
        }

        public ImplementationElement ToImplElement(RuleField field)
        {
            ImplementationElement element = new ImplementationElement()
            {
                Id = ElemId,
                ImplTaskId = TaskId,
                Field = field.ToString(),
                Ip = Ip,
                Port = Port,
                ProtoId = ProtoId,
                NetworkId = NetworkId,
                ServiceId = ServiceId
            };
            return element;
        }
    }
}
