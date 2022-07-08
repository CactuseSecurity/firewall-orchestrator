using NetTools;

namespace FWO.Api.Data
{
    public class NwObjectElement
    {
        public int ElemId { get; set; }
        public int TaskId { get; set; }
        public IPAddressRange Ip { get; set; } = new IPAddressRange();
        public string IpString 
        {
            get => Ip.ToCidrString();
            set => Parse(value);
        }
        public long? NetworkId { get; set; }

        private void Parse(string value)
        {
            try 
            {
                Ip = IPAddressRange.Parse(value);
            } 
            catch(Exception)
            {}
        }

        public RequestElement ToReqElement(AccessField field)
        {
            RequestElement element = new RequestElement()
            {
                Id = ElemId,
                TaskId = TaskId,
                Field = field.ToString(),
                Ip = Ip,
                NetworkId = NetworkId,
            };
            return element;
        }

        public ImplementationElement ToImplElement(AccessField field)
        {
            ImplementationElement element = new ImplementationElement()
            {
                Id = ElemId,
                ImplTaskId = TaskId,
                Field = field.ToString(),
                Ip = Ip,
                NetworkId = NetworkId,
            };
            return element;
        }
    }
}
