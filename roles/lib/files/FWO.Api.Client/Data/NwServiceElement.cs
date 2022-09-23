namespace FWO.Api.Data
{
    public class NwServiceElement
    {
        public long ElemId { get; set; }
        public long TaskId { get; set; }
        public int Port { get; set; } = 1;
        public int? ProtoId { get; set; } = 6;
        public long? ServiceId { get; set; }


        public RequestReqElement ToReqElement(AccessField field)
        {
            RequestReqElement element = new RequestReqElement()
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

        public RequestImplElement ToImplElement(AccessField field)
        {
            RequestImplElement element = new RequestImplElement()
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
