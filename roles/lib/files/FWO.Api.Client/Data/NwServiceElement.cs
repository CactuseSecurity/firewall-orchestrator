namespace FWO.Api.Data
{
    public class NwServiceElement
    {
        public int ElemId { get; set; }
        public int TaskId { get; set; }
        public int Port { get; set; } = 1;
        public int? ProtoId { get; set; } = 6;
        public long? ServiceId { get; set; }


        public RequestElement ToReqElement(AccessField field)
        {
            RequestElement element = new RequestElement()
            {
                Id = ElemId,
                TaskId = TaskId,
                Field = field.ToString(),
                Port = Port,
                ProtoId = ProtoId,
                ServiceId = ServiceId
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
                Port = Port,
                ProtoId = ProtoId,
                ServiceId = ServiceId
            };
            return element;
        }
    }
}
