﻿namespace FWO.Api.Data
{
    public class NwServiceElement
    {
        public long ElemId { get; set; }
        public long TaskId { get; set; }
        public int Port { get; set; }
        public int ProtoId { get; set; }
        public long? ServiceId { get; set; }


        public WfReqElement ToReqElement()
        {
            WfReqElement element = new ()
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

        public WfImplElement ToImplElement()
        {
            WfImplElement element = new ()
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
