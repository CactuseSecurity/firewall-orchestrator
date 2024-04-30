namespace FWO.Api.Data
{
    public class NwServiceElement
    {
        public long ElemId { get; set; }
        public long TaskId { get; set; }
        public int Port { get; set; }
        public int ProtoId { get; set; }
        public long? ServiceId { get; set; }


        public RequestReqElement ToReqElement()
        {
            RequestReqElement element = new RequestReqElement()
            {
                Id = ElemId,
                TaskId = TaskId,
                Field = ElemFieldType.service.ToString(),
                Port = Port,
                ProtoId = ProtoId,
                ServiceId = ServiceId
            };
            return element;
        }

        public RequestImplElement ToImplElement()
        {
            RequestImplElement element = new RequestImplElement()
            {
                Id = ElemId,
                ImplTaskId = TaskId,
                Field = ElemFieldType.service.ToString(),
                Port = Port,
                ProtoId = ProtoId,
                ServiceId = ServiceId
            };
            return element;
        }
    }
}
