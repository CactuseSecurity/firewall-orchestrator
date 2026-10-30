using FWO.Data.Workflow;
using FWO.Data.Flow;

namespace FWO.Ui.Shared
{
    public sealed class TargetDatesEditorModel
    {
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }
        public long? FlowTimeObjectId { get; set; }
        public long? TicketElementId { get; set; }
        public string? FlowTimeObjectName { get; set; }
        public List<FlowTimeObject> TicketTimeObjects { get; set; } = [];
        public Dictionary<long, long> TicketTimeObjectElementIds { get; set; } = [];
        public bool ShowBeginDate { get; set; } = true;
        public bool ShowEndDate { get; set; } = true;

        public TargetDatesEditorModel()
        { }

        public TargetDatesEditorModel(TargetDatesEditorModel other)
        {
            BeginDate = other.BeginDate;
            EndDate = other.EndDate;
            FlowTimeObjectId = other.FlowTimeObjectId;
            TicketElementId = other.TicketElementId;
            FlowTimeObjectName = other.FlowTimeObjectName;
            TicketTimeObjects = [.. other.TicketTimeObjects];
            TicketTimeObjectElementIds = new(other.TicketTimeObjectElementIds);
            ShowBeginDate = other.ShowBeginDate;
            ShowEndDate = other.ShowEndDate;
        }
    }
}
