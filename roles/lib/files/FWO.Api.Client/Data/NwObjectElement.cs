namespace FWO.Api.Data
{
    public class NwObjectElement
    {
        public int ElemId { get; set; }
        public int TaskId { get; set; }
        public Cidr Cidr { get; set; }
        public long? NetworkId { get; set; }

        public NwObjectElement()
        {}

        public NwObjectElement(string cidrString, int taskId)
        {
            Cidr = new Cidr(cidrString);
            TaskId = taskId;
        }

        public RequestElement ToReqElement(AccessField field)
        {
            RequestElement element = new RequestElement()
            {
                Id = ElemId,
                TaskId = TaskId,
                Field = field.ToString(),
                Cidr = new Cidr(Cidr.CidrString),
                NetworkId = NetworkId
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
                Cidr = new Cidr(Cidr.CidrString),
                NetworkId = NetworkId,
            };
            return element;
        }
    }
}
