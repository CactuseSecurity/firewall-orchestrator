namespace FWO.Api.Data
{
    public class NwRuleElement
    {
        public long ElemId { get; set; }
        public long TaskId { get; set; }
        public string RuleUid { get; set; } = "";


        public RequestReqElement ToReqElement()
        {
            RequestReqElement element = new RequestReqElement()
            {
                Id = ElemId,
                TaskId = TaskId,
                Field = ElemFieldType.rule.ToString(),
                RuleUid = RuleUid
            };
            return element;
        }

        public RequestImplElement ToImplElement()
        {
            RequestImplElement element = new RequestImplElement()
            {
                Id = ElemId,
                ImplTaskId = TaskId,
                Field = ElemFieldType.rule.ToString(),
                RuleUid = RuleUid
            };
            return element;
        }
    }
}
